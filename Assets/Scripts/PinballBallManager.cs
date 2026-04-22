using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 分裂後 (gen ≥ 1) のボールを NativeArray で一括管理し、
/// C# Jobs + Burst で並列に物理更新・Splitter検知・寿命判定を行うマネージャ。
/// シーンに 1 つだけ存在。初回アクセス時に自動生成される。
/// </summary>
public class PinballBallManager : MonoBehaviour
{
    // ---- 全 Inspector 項目は PinballBallConfig に分離 ----
    // シーンに PinballBallConfig をアタッチすれば Manager が起動時に参照する。
    // 見つからない場合はデフォルト値で動作。
    private PinballBallConfig _config;

    private int _totalGenerated = 0;
    private GUIStyle _moneyStyle;
    private float _nextLogTime = 0f;

    // 所持金ポップ演出
    private long _lastMoneyPopStep = 0;
    private float _popStartTime = -999f;

    private static PinballBallManager _instance;
    public static PinballBallManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindFirstObjectByType<PinballBallManager>();
            if (_instance == null)
            {
                var go = new GameObject("[PinballBallManager]");
                _instance = go.AddComponent<PinballBallManager>();
            }
            return _instance;
        }
    }

    /// <summary>
    /// 初回起動時とその後のシーンロードの両方で Manager を生成しておく (R キー等の入力受付のため)。
    /// [RuntimeInitializeOnLoadMethod] はゲーム起動時の一度しか走らないので、
    /// SceneManager.sceneLoaded に subscribe してリロード後も復活させる。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceOnSceneLoad()
    {
        _ = Instance;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _ = Instance;
    }

    // ボール配列
    private NativeList<float3> _positions;
    private NativeList<float3> _velocities;
    private NativeList<float> _radii;
    private NativeList<int> _generations;
    private NativeList<int> _ignoredSplitterIdx;
    private NativeList<float> _ignoreUntil;
    private NativeList<float> _expireAt;
    private NativeList<byte> _splitFlags;
    private NativeList<int> _splitterHitIdx;
    private TransformAccessArray _transforms;

    // Splitter キャッシュ
    private Transform[] _splitterTransforms;
    private Collider[] _splitterColliders;
    private NativeArray<float3> _splitterPositions;
    private int _splitterCount = 0;

    // 床レベル
    private bool _floorYSet = false;
    private float _floorY = 0f;

    // Controller から一度だけ取り込む設定値
    private bool _configCached = false;
    private GameObject _ballTemplate;
    private float _boundsXMin, _boundsXMax, _boundsZMax;
    private float _bounceFactor;
    private float _lifetime;
    private float _splitScaleRatio;
    private int _splitCount;
    private float _splitSpread;
    private float _spawnXOffset;
    private int _particleGeneration;
    private ParticleSystem _splitParticlePrefab;
    private float _hideParticleXMax;
    private float _hideParticleZMin;
    private string _splitTargetTag = "Splitter";
    private float _ignoreCollisionDuration;
    private bool _enableBallBallCollision;

    private bool _initialized = false;
    private JobHandle _lastJobHandle;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        ResolveConfig();
        Initialize();
    }

    void Start()
    {
        // この時点でシーン上の gen 0 は OnEnable 済み。初期個数を累積カウンタの起点にする。
        _totalGenerated = PinballBallController.AliveGen0Count;
        // 開始時の所持金ではポップさせない (現ステップを起点に設定)
        _lastMoneyPopStep = CurrentMoneyStep();
    }

    long CurrentMoneyStep()
    {
        if (_config == null) return 0;
        long money = (long)_totalGenerated * _config.moneyPerBall;
        int thr = Mathf.Max(1, _config.moneyPopThreshold);
        return money / thr;
    }

    void ResolveConfig()
    {
        _config = FindFirstObjectByType<PinballBallConfig>();
        if (_config == null)
        {
            // 見つからなければデフォルト値の一時 Config を生成
            var go = new GameObject("[PinballBallConfigDefault]");
            go.hideFlags = HideFlags.HideAndDontSave;
            _config = go.AddComponent<PinballBallConfig>();
        }
    }

    void Initialize()
    {
        if (_initialized) return;
        int cap = _config != null ? _config.initialCapacity : 256;
        _positions = new NativeList<float3>(cap, Allocator.Persistent);
        _velocities = new NativeList<float3>(cap, Allocator.Persistent);
        _radii = new NativeList<float>(cap, Allocator.Persistent);
        _generations = new NativeList<int>(cap, Allocator.Persistent);
        _ignoredSplitterIdx = new NativeList<int>(cap, Allocator.Persistent);
        _ignoreUntil = new NativeList<float>(cap, Allocator.Persistent);
        _expireAt = new NativeList<float>(cap, Allocator.Persistent);
        _splitFlags = new NativeList<byte>(cap, Allocator.Persistent);
        _splitterHitIdx = new NativeList<int>(cap, Allocator.Persistent);
        _transforms = new TransformAccessArray(cap);
        _initialized = true;
    }

    /// <summary>Controller から一度だけ設定を取り込み、Splitter キャッシュを構築する。</summary>
    public void EnsureConfigured(PinballBallController c)
    {
        if (_configCached) return;

        // gen 0 は衝突後に Destroy されるので、永続テンプレートとして非アクティブ複製を保持する
        GameObject src = c.gameObject;
        bool wasActive = src.activeSelf;
        src.SetActive(false);
        _ballTemplate = Instantiate(src);
        _ballTemplate.name = "[PinballBallTemplate]";
        _ballTemplate.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(_ballTemplate);
        // テンプレートから Controller → Rigidbody → Collider の順に即時除去
        // ([RequireComponent] 制約があるので Controller を先に、かつ DestroyImmediate で同フレームに反映)
        var tmplCtrl = _ballTemplate.GetComponent<PinballBallController>();
        if (tmplCtrl != null) DestroyImmediate(tmplCtrl);
        var tmplRb = _ballTemplate.GetComponent<Rigidbody>();
        if (tmplRb != null) DestroyImmediate(tmplRb);
        var tmplCol = _ballTemplate.GetComponent<Collider>();
        if (tmplCol != null) DestroyImmediate(tmplCol);
        src.SetActive(wasActive);

        _boundsXMin = c.manualBoundsXMin;
        _boundsXMax = c.manualBoundsXMax;
        _boundsZMax = c.manualBoundsZMax;
        _bounceFactor = c.manualBounceFactor;
        _lifetime = c.manualLifetime;
        _splitScaleRatio = c.splitScaleRatio;
        _splitCount = Mathf.Max(2, c.splitCount);
        _splitSpread = c.splitSpread;
        _spawnXOffset = c.spawnXOffset;
        _particleGeneration = c.particleGeneration;
        _splitParticlePrefab = c.splitParticlePrefab;
        _hideParticleXMax = c.hideParticleXMax;
        _hideParticleZMin = c.hideParticleZMin;
        _splitTargetTag = c.splitTargetTag;
        _ignoreCollisionDuration = c.ignoreCollisionDuration;
        _enableBallBallCollision = c.enableBallBallCollision;

        GameObject[] splitters = GameObject.FindGameObjectsWithTag(_splitTargetTag);
        _splitterCount = splitters.Length;
        _splitterTransforms = new Transform[_splitterCount];
        _splitterColliders = new Collider[_splitterCount];
        _splitterPositions = new NativeArray<float3>(Mathf.Max(1, _splitterCount), Allocator.Persistent);
        for (int i = 0; i < _splitterCount; i++)
        {
            _splitterTransforms[i] = splitters[i].transform;
            _splitterColliders[i] = splitters[i].GetComponent<Collider>();
        }
        _configCached = true;
    }

    public float FloorY => _floorY;
    public bool IsFloorYSet => _floorYSet;

    public void SetFloorY(float y)
    {
        _floorY = y;
        _floorYSet = true;
    }

    /// <summary>Splitter Collider から内部 index を取得。未知なら -1。</summary>
    public int GetSplitterIndex(Collider col)
    {
        if (_splitterColliders == null) return -1;
        for (int i = 0; i < _splitterCount; i++)
            if (_splitterColliders[i] == col) return i;
        return -1;
    }

    /// <summary>
    /// gen 0 Controller から呼ばれる。gen 1 の子ボールを count 個スポーンし、Manager に登録する。
    /// particleGeneration == 1 ならパーティクルバーストへフォールバック。
    /// </summary>
    public void ProduceGen1Children(PinballBallController source, Vector3 collisionPos, Collider splitterCol, float gen0SphereRadius)
    {
        EnsureConfigured(source);
        if (!_floorYSet) SetFloorY(collisionPos.y - gen0SphereRadius);

        int nextGen = 1;
        if (_particleGeneration >= 0 && nextGen >= _particleGeneration && _splitParticlePrefab != null)
        {
            SpawnParticleBurst(collisionPos);
            return;
        }

        int splitterIdx = GetSplitterIndex(splitterCol);
        int count = _splitCount;
        float childRadius = source.initialDetectionRadius;
        Vector3 childScale = source.transform.localScale * _splitScaleRatio;
        float xOffset = _spawnXOffset;

        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0f : ((float)i / (count - 1)) * 2f - 1f;
            Vector3 spawnPos = new Vector3(
                collisionPos.x + xOffset * t,
                _floorY + childRadius,
                collisionPos.z
            );
            Vector3 vel = new Vector3(_splitSpread * t, 0f, 0f);
            InstantiateAndRegister(spawnPos, vel, childRadius, nextGen, splitterIdx, childScale);
        }
    }

    void InstantiateAndRegister(Vector3 pos, Vector3 vel, float radius, int generation, int ignoredSplitter, Vector3 localScale)
    {
        GameObject child = Instantiate(_ballTemplate, pos, Quaternion.identity);
        child.hideFlags = HideFlags.None;
        child.SetActive(true);
        child.transform.localScale = localScale;
        Register(child.transform, (float3)(Vector3)pos, (float3)(Vector3)vel, radius, generation, ignoredSplitter);
    }

    void Register(Transform t, float3 pos, float3 vel, float radius, int generation, int ignoredSplitter)
    {
        if (!_initialized) Initialize();
        _positions.Add(pos);
        _velocities.Add(new float3(vel.x, 0f, vel.z));
        _radii.Add(radius);
        _generations.Add(generation);
        _ignoredSplitterIdx.Add(ignoredSplitter);
        _ignoreUntil.Add(Time.time + _ignoreCollisionDuration);
        _expireAt.Add(_lifetime > 0f ? Time.time + _lifetime : float.PositiveInfinity);
        _splitFlags.Add(0);
        _splitterHitIdx.Add(-1);
        _transforms.Add(t);
        _totalGenerated++;
    }

    void Unregister(int index)
    {
        _positions.RemoveAtSwapBack(index);
        _velocities.RemoveAtSwapBack(index);
        _radii.RemoveAtSwapBack(index);
        _generations.RemoveAtSwapBack(index);
        _ignoredSplitterIdx.RemoveAtSwapBack(index);
        _ignoreUntil.RemoveAtSwapBack(index);
        _expireAt.RemoveAtSwapBack(index);
        _splitFlags.RemoveAtSwapBack(index);
        _splitterHitIdx.RemoveAtSwapBack(index);
        _transforms.RemoveAtSwapBack(index);
    }

    void Update()
    {
        if (!_initialized || !_configCached || !_floorYSet) return;
        int count = _positions.Length;
        if (count == 0) return;

        for (int i = 0; i < _splitterCount; i++)
        {
            if (_splitterTransforms[i] == null) continue;
            _splitterPositions[i] = _splitterTransforms[i].position;
        }
        for (int i = 0; i < count; i++) _splitFlags[i] = 0;

        float dt = Time.deltaTime;
        float gravityMag = Mathf.Abs(Physics.gravity.y);

        var integrateJob = new IntegrateJob
        {
            positions = _positions.AsArray(),
            velocities = _velocities.AsArray(),
            radii = _radii.AsArray(),
            dt = dt,
            gravityZ = gravityMag,
            floorY = _floorY,
        };
        JobHandle h = integrateJob.Schedule(count, 64);

        var boundsJob = new BoundsBounceJob
        {
            positions = _positions.AsArray(),
            velocities = _velocities.AsArray(),
            radii = _radii.AsArray(),
            xmin = _boundsXMin,
            xmax = _boundsXMax,
            zmax = _boundsZMax,
            bounce = _bounceFactor,
        };
        h = boundsJob.Schedule(count, 64, h);

        if (_enableBallBallCollision && count > 1)
        {
            var separateJob = new BallBallSeparateJob
            {
                positions = _positions.AsArray(),
                velocities = _velocities.AsArray(),
                radii = _radii.AsArray(),
                count = count,
                restitution = _config.ballBallRestitution,
                positionSlop = _config.ballBallPositionSlop,
                positionCorrection = _config.ballBallPositionCorrection,
            };
            h = separateJob.Schedule(h);
        }

        var splitterJob = new SplitterDetectJob
        {
            positions = _positions.AsArray(),
            radii = _radii.AsArray(),
            ignoredSplitterIdx = _ignoredSplitterIdx.AsArray(),
            ignoreUntil = _ignoreUntil.AsArray(),
            splitterPositions = _splitterPositions,
            splitterCount = _splitterCount,
            currentTime = Time.time,
            splitFlags = _splitFlags.AsArray(),
            splitterHitIdx = _splitterHitIdx.AsArray(),
        };
        h = splitterJob.Schedule(count, 64, h);

        var lifetimeJob = new LifetimeCheckJob
        {
            expireAt = _expireAt.AsArray(),
            currentTime = Time.time,
            splitFlags = _splitFlags.AsArray(),
        };
        h = lifetimeJob.Schedule(count, 64, h);

        var applyJob = new ApplyTransformsJob
        {
            positions = _positions.AsArray(),
        };
        _lastJobHandle = applyJob.Schedule(_transforms, h);
        _lastJobHandle.Complete();

        // 結果処理 (逆順で swap-remove 安全に)
        for (int i = _positions.Length - 1; i >= 0; i--)
        {
            byte flag = _splitFlags[i];
            if (flag == 1) SpawnChildrenAndDestroy(i);
            else if (flag == 2) DestroyAt(i);
        }
    }

    void LateUpdate()
    {
        if (IsResetKeyPressed())
        {
            ResetGame();
            return;
        }

        int managed = _initialized ? _positions.Length : 0;
        int total = managed + PinballBallController.AliveGen0Count;
        if (_config != null)
        {
            _config.debugManagedCount = managed;
            _config.debugTotalCount = total;
            _config.debugTotalGenerated = _totalGenerated;

            // 所持金ポップ判定: ステップが 1 つ以上進んだら演出開始
            long step = CurrentMoneyStep();
            if (step > _lastMoneyPopStep)
            {
                _lastMoneyPopStep = step;
                _popStartTime = Time.unscaledTime;
            }
        }

        if (_config == null || !_config.logBallCount) return;
        if (Time.unscaledTime < _nextLogTime) return;
        _nextLogTime = Time.unscaledTime + Mathf.Max(0.05f, _config.logInterval);
        Debug.Log($"[PinballBall] total={total} (gen0={PinballBallController.AliveGen0Count}, gen≥1={managed})");
    }

    bool IsResetKeyPressed()
    {
        KeyCode key = _config != null ? _config.resetKey : KeyCode.R;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            var k = KeyCodeToKey(key);
            if (k != Key.None && Keyboard.current[k].wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(key)) return true;
#endif
        return false;
    }

#if ENABLE_INPUT_SYSTEM
    static Key KeyCodeToKey(KeyCode code)
    {
        // 主要キーのみサポート。必要に応じて拡張。
        if (code >= KeyCode.A && code <= KeyCode.Z) return Key.A + (int)(code - KeyCode.A);
        if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9) return Key.Digit0 + (int)(code - KeyCode.Alpha0);
        switch (code)
        {
            case KeyCode.Space: return Key.Space;
            case KeyCode.Return: return Key.Enter;
            case KeyCode.Escape: return Key.Escape;
            case KeyCode.Tab: return Key.Tab;
            default: return Key.None;
        }
    }
#endif

    public void ResetGame()
    {
        _lastJobHandle.Complete();
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    void OnGUI()
    {
        if (_config == null || !_config.showMoneyLabel) return;
        int fontSize = _config.moneyFontSize;
        if (_moneyStyle == null || _moneyStyle.fontSize != fontSize)
        {
            _moneyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.UpperRight,
                fontStyle = FontStyle.Bold,
            };
        }
        _moneyStyle.normal.textColor = _config.moneyColor;

        long money = (long)_totalGenerated * _config.moneyPerBall;
        string text = $"{_config.moneyLabelPrefix}{money:N0}{_config.moneyLabelSuffix}";

        // 文字サイズに応じて矩形サイズも追従させる
        Vector2 size = _moneyStyle.CalcSize(new GUIContent(text));
        float width = size.x + 8f;
        float height = size.y + 8f;
        Rect rect = new Rect(Screen.width - width - _config.moneyPadding.x, _config.moneyPadding.y, width, height);

        // ポップ演出: 経過時間に応じて ease-out で 1.0 → (1 + bonus) → 1.0 に戻る
        float scale = 1f;
        float duration = Mathf.Max(0.01f, _config.moneyPopDuration);
        float elapsed = Time.unscaledTime - _popStartTime;
        if (elapsed >= 0f && elapsed < duration)
        {
            float t = elapsed / duration;
            // 立ち上がりをすばやく (0→0.15) → ゆっくり減衰 させるため二段構え
            float rise = Mathf.Clamp01(t / 0.15f);
            float fall = Mathf.Pow(1f - Mathf.Clamp01((t - 0.15f) / 0.85f), 2f);
            float curve = (t < 0.15f) ? rise : fall;
            scale = 1f + (_config.moneyPopScale - 1f) * curve;
        }

        // 右上を基準にスケール (画面外に出ないよう右端をピボットに)
        Matrix4x4 prevMatrix = GUI.matrix;
        if (!Mathf.Approximately(scale, 1f))
        {
            Vector2 pivot = new Vector2(rect.xMax, rect.y);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot);
        }

        // 影 (視認性向上) — フォントサイズに比例してオフセットを調整
        float shadow = Mathf.Max(2f, fontSize * 0.05f);
        var prevColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(rect.x + shadow, rect.y + shadow, rect.width, rect.height), text, _moneyStyle);
        GUI.color = prevColor;

        GUI.Label(rect, text, _moneyStyle);

        GUI.matrix = prevMatrix;
    }

    void SpawnChildrenAndDestroy(int index)
    {
        int parentGen = _generations[index];
        int nextGen = parentGen + 1;
        float3 parentPos = _positions[index];
        float parentRadius = _radii[index];
        int hitSplitterIdx = _splitterHitIdx[index];

        if (_particleGeneration >= 0 && nextGen >= _particleGeneration && _splitParticlePrefab != null)
        {
            SpawnParticleBurst(new Vector3(parentPos.x, parentPos.y, parentPos.z));
            DestroyAt(index);
            return;
        }

        int count = _splitCount;
        float childRadius = parentRadius * _splitScaleRatio;
        Vector3 parentLocalScale = _transforms[index].localScale;
        Vector3 childScale = parentLocalScale * _splitScaleRatio;
        float xOffset = _spawnXOffset * Mathf.Pow(0.5f, parentGen);

        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0f : ((float)i / (count - 1)) * 2f - 1f;
            Vector3 spawnPos = new Vector3(
                parentPos.x + xOffset * t,
                _floorY + childRadius,
                parentPos.z
            );
            Vector3 vel = new Vector3(_splitSpread * t, 0f, 0f);
            InstantiateAndRegister(spawnPos, vel, childRadius, nextGen, hitSplitterIdx, childScale);
        }

        DestroyAt(index);
    }

    void DestroyAt(int index)
    {
        GameObject go = _transforms[index].gameObject;
        Unregister(index);
        Destroy(go);
    }

    public void SpawnParticleBurst(Vector3 position)
    {
        if (_splitParticlePrefab == null) return;
        if (position.x <= _hideParticleXMax && position.z >= _hideParticleZMin) return;
        ParticleSystem ps = Instantiate(_splitParticlePrefab, position, _splitParticlePrefab.transform.rotation);
        var culler = ps.gameObject.AddComponent<ParticleRegionCuller>();
        culler.hideXMax = _hideParticleXMax;
        culler.hideZMin = _hideParticleZMin;
        ps.Play();
    }

    void OnDestroy()
    {
        _lastJobHandle.Complete();
        if (_ballTemplate != null) Destroy(_ballTemplate);
        if (_positions.IsCreated) _positions.Dispose();
        if (_velocities.IsCreated) _velocities.Dispose();
        if (_radii.IsCreated) _radii.Dispose();
        if (_generations.IsCreated) _generations.Dispose();
        if (_ignoredSplitterIdx.IsCreated) _ignoredSplitterIdx.Dispose();
        if (_ignoreUntil.IsCreated) _ignoreUntil.Dispose();
        if (_expireAt.IsCreated) _expireAt.Dispose();
        if (_splitFlags.IsCreated) _splitFlags.Dispose();
        if (_splitterHitIdx.IsCreated) _splitterHitIdx.Dispose();
        if (_transforms.isCreated) _transforms.Dispose();
        if (_splitterPositions.IsCreated) _splitterPositions.Dispose();
        if (_instance == this) _instance = null;
    }

    // ---------------- Jobs ----------------

    [BurstCompile]
    struct IntegrateJob : IJobParallelFor
    {
        public NativeArray<float3> positions;
        public NativeArray<float3> velocities;
        [ReadOnly] public NativeArray<float> radii;
        public float dt;
        public float gravityZ;
        public float floorY;

        public void Execute(int i)
        {
            float3 v = velocities[i];
            v.y = 0f;
            v.z += gravityZ * dt;
            velocities[i] = v;

            float3 p = positions[i];
            p.x += v.x * dt;
            p.z += v.z * dt;
            p.y = floorY + radii[i];
            positions[i] = p;
        }
    }

    [BurstCompile]
    struct BoundsBounceJob : IJobParallelFor
    {
        public NativeArray<float3> positions;
        public NativeArray<float3> velocities;
        [ReadOnly] public NativeArray<float> radii;
        public float xmin, xmax, zmax, bounce;

        public void Execute(int i)
        {
            float3 p = positions[i];
            float3 v = velocities[i];
            float r = radii[i];

            // 球面が境界に触れる位置 (中心 = 境界 ± 半径) でクランプ
            float xLo = xmin + r;
            float xHi = xmax - r;
            float zHi = zmax - r;

            if (p.x < xLo) { p.x = xLo; if (v.x < 0f) v.x = -v.x * bounce; }
            else if (p.x > xHi) { p.x = xHi; if (v.x > 0f) v.x = -v.x * bounce; }
            if (p.z > zHi) { p.z = zHi; if (v.z > 0f) v.z = -v.z * bounce; }

            positions[i] = p;
            velocities[i] = v;
        }
    }

    /// <summary>
    /// O(N²) で重なりを位置分離 + 法線方向の相対速度を弱い反発で打ち消す。
    /// 位置のみの分離だと互いに押し込み合って振動するため、法線方向の近づく速度成分を
    /// 反発係数 (restitution) で反転させつつ、接触位置スラックで微小重なりは無視する。
    /// 並列化しない (read/write 同時アクセスで race を避けるため)。
    /// </summary>
    [BurstCompile]
    struct BallBallSeparateJob : IJob
    {
        public NativeArray<float3> positions;
        public NativeArray<float3> velocities;
        [ReadOnly] public NativeArray<float> radii;
        public int count;
        public float restitution;        // 0 = 完全非弾性、0.2 程度で落ち着いた反発
        public float positionSlop;       // この量までの重なりは位置補正しない (ジッタ防止)
        public float positionCorrection; // 位置補正の割合 (0.2〜0.5 推奨、1.0 は即時解消で振動しやすい)

        public void Execute()
        {
            for (int i = 0; i < count - 1; i++)
            {
                float3 pi = positions[i];
                float3 vi = velocities[i];
                float ri = radii[i];
                for (int j = i + 1; j < count; j++)
                {
                    float3 pj = positions[j];
                    float3 vj = velocities[j];
                    float rj = radii[j];
                    float dx = pj.x - pi.x;
                    float dz = pj.z - pi.z;
                    float distSq = dx * dx + dz * dz;
                    float minDist = ri + rj;
                    if (distSq >= minDist * minDist || distSq < 1e-8f) continue;

                    float dist = math.sqrt(distSq);
                    float nx = dx / dist;
                    float nz = dz / dist;

                    // --- 位置補正 (スラックを超えた分のみ、緩和係数で少しずつ)
                    float penetration = minDist - dist;
                    float corr = math.max(0f, penetration - positionSlop) * positionCorrection * 0.5f;
                    if (corr > 0f)
                    {
                        pi.x -= nx * corr;
                        pi.z -= nz * corr;
                        pj.x += nx * corr;
                        pj.z += nz * corr;
                    }

                    // --- 速度補正 (接近成分だけ反発で反転)
                    float rvx = vj.x - vi.x;
                    float rvz = vj.z - vi.z;
                    float velAlongNormal = rvx * nx + rvz * nz; // >0 なら離れつつある、<0 なら接近中
                    if (velAlongNormal < 0f)
                    {
                        float jImpulse = -(1f + restitution) * velAlongNormal * 0.5f; // 等質量
                        float ix = jImpulse * nx;
                        float iz = jImpulse * nz;
                        vi.x -= ix;
                        vi.z -= iz;
                        vj.x += ix;
                        vj.z += iz;
                    }

                    positions[j] = pj;
                    velocities[j] = vj;
                }
                positions[i] = pi;
                velocities[i] = vi;
            }
        }
    }

    [BurstCompile]
    struct SplitterDetectJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<float> radii;
        [ReadOnly] public NativeArray<int> ignoredSplitterIdx;
        [ReadOnly] public NativeArray<float> ignoreUntil;
        [ReadOnly] public NativeArray<float3> splitterPositions;
        public int splitterCount;
        public float currentTime;
        [NativeDisableParallelForRestriction] public NativeArray<byte> splitFlags;
        [NativeDisableParallelForRestriction] public NativeArray<int> splitterHitIdx;

        public void Execute(int i)
        {
            if (splitFlags[i] != 0) return;
            float3 p = positions[i];
            float r = radii[i];
            float r2 = r * r;
            int ignored = ignoredSplitterIdx[i];
            float ignoreEnd = ignoreUntil[i];
            bool stillIgnoring = currentTime < ignoreEnd;

            for (int j = 0; j < splitterCount; j++)
            {
                if (stillIgnoring && j == ignored) continue;
                float3 s = splitterPositions[j];
                float dx = s.x - p.x;
                float dz = s.z - p.z;
                if (dx * dx + dz * dz < r2)
                {
                    splitFlags[i] = 1;
                    splitterHitIdx[i] = j;
                    return;
                }
            }
        }
    }

    [BurstCompile]
    struct LifetimeCheckJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> expireAt;
        public float currentTime;
        [NativeDisableParallelForRestriction] public NativeArray<byte> splitFlags;

        public void Execute(int i)
        {
            if (splitFlags[i] != 0) return;
            if (currentTime >= expireAt[i]) splitFlags[i] = 2;
        }
    }

    [BurstCompile]
    struct ApplyTransformsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float3> positions;

        public void Execute(int i, TransformAccess t)
        {
            float3 p = positions[i];
            t.position = new Vector3(p.x, p.y, p.z);
        }
    }
}
