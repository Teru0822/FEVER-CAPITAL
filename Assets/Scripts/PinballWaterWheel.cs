using UnityEngine;

/// <summary>
/// 水車型のベルトコンベア。複数の「皿」を回転軸まわりにぐるぐる回し、
/// 下から上に物体を運び上げる。皿は Kinematic Rigidbody として動くので
/// Unity 物理が自動的に玉を皿の上に載せて運搬する。
///
/// 形状:
///   - Circle:  純円形。皿は配置した初期位置を保ったまま回転する (従来挙動)
///   - Stadium: 真ん中だけ伸ばした円 (パターノスター形)。皿はパス上を等距離に
///              自動配置されてループする。stretchLength で伸ばす長さを指定
/// </summary>
public class PinballWaterWheel : MonoBehaviour
{
    public enum WheelShape
    {
        Circle,
        Stadium,
    }

    [Header("形状")]
    [Tooltip("Circle: 純円 (皿の初期位置保持)\nStadium: 真ん中だけ伸ばした円 (皿はパス上に自動均等配置)")]
    [SerializeField] private WheelShape shape = WheelShape.Circle;

    [Tooltip("Stadium 時の半径。Circle 時は皿の初期距離が半径")]
    [Min(0.01f)]
    [SerializeField] private float radius = 1f;

    [Tooltip("Stadium 時の真ん中ストレート部分の長さ (m)。0 で実質円形。+ で「真ん中だけ伸ばした」形に")]
    [Min(0f)]
    [SerializeField] private float stretchLength = 0f;

    [Tooltip("Stadium 時のストレート方向 (ローカル軸)。rotationAxis と直交させること。例: (0,1,0)")]
    [SerializeField] private Vector3 stretchAxis = Vector3.up;

    [Header("皿")]
    [Tooltip("回転させる皿 Transform 群。手動配置する場合に使用。\n下の Plate Prefab + Auto Spawn Plate Count を指定すると自動配置に切替 (この配列は上書き)")]
    [SerializeField] private Transform[] plates;

    [Tooltip("各皿に Kinematic Rigidbody を Awake で自動付与する")]
    [SerializeField] private bool autoAddKinematicRigidbody = true;

    [Header("自動スポーン (オプション)")]
    [Tooltip("プレハブを設定 + Auto Spawn Plate Count > 0 で、Awake 時にパス上へ等間隔自動配置する。手動 plates は上書きされる。")]
    [SerializeField] private GameObject platePrefab;

    [Tooltip("自動スポーンする皿の枚数 (0 = 自動スポーン無効、上の手動 plates を使用)")]
    [Min(0)]
    [SerializeField] private int autoSpawnPlateCount = 0;

    [Header("回転軸・速度")]
    [Tooltip("回転軸 (このオブジェクトのローカル空間)")]
    [SerializeField] private Vector3 rotationAxis = Vector3.right;

    [Tooltip("回転速度 (deg/s)。Stadium 時はリム上の同等線速度 (= radius * deg2rad * angularSpeed) に変換される")]
    [SerializeField] private float angularSpeed = 60f;

    [Header("皿の姿勢")]
    [Tooltip("OFF (既定): 皿はパス接線+外向きで向く (水車式、半円を進むと皿も自然に反転する)。設置時の姿勢オフセットは維持される。\nON: 皿は常に初期姿勢を維持 (パターノスター式、半円でも姿勢不変、玉が落ちにくい)")]
    [SerializeField] private bool plateAlwaysUpright = false;

    [Header("皿の物理")]
    [Tooltip("Awake 時に皿 Collider へ高摩擦の PhysicsMaterial を付与 (玉が滑り落ちない)")]
    [SerializeField] private bool autoApplyHighFrictionMaterial = true;

    [Header("カタカタ揺れ + SFX")]
    [Tooltip("Y軸まわりの微振動の最大角度 (deg)。0 で無効")]
    [SerializeField, Min(0f)] private float rattleAmplitudeDegrees = 1.5f;

    [Tooltip("カタカタの細かさ (Hz 相当)。大きいほど細かい震え")]
    [SerializeField, Min(0.1f)] private float rattleFrequency = 10f;

    [Tooltip("カタカタ音 AudioClip。null なら無音")]
    [SerializeField] private AudioClip rattleSfxClip;

    [Tooltip("カタカタ音を鳴らす間隔 (秒)。短いほど忙しいリズム")]
    [SerializeField, Min(0.02f)] private float rattleSfxInterval = 0.18f;

    [Range(0f, 1f)]
    [Tooltip("カタカタ音の音量")]
    [SerializeField] private float rattleSfxVolume = 0.3f;

    [Range(0f, 0.5f)]
    [Tooltip("カタカタ音のピッチ揺らぎ (±幅)")]
    [SerializeField] private float rattleSfxPitchVariance = 0.2f;

    [Header("デバッグ")]
    [Tooltip("OnDrawGizmosSelected で各皿の +Y (上面) と +Z (前方) を矢印で表示")]
    [SerializeField] private bool drawPlateAxesGizmo = false;

    [Tooltip("Awake で設定内容を Console に出力する")]
    [SerializeField] private bool logSettingsOnAwake = false;

    [Tooltip("Update のたびに plate[0] の phase / worldRot 等を Console に出力 (回転していない原因切り分け用)")]
    [SerializeField] private bool logPlate0EachFrame = false;

    private float _logTimer = 0f;

    // --- 内部状態 ---
    // Circle 用
    private Vector3[] _initialLocalOffsets;
    private float _currentAngle = 0f;

    // Stadium 用
    private float[] _phases;
    private float _pathLength;
    // 初期姿勢オフセット (皿の設置回転 = pathRot(初期phase) × offset)
    private Quaternion[] _pathRotationOffsets;

    // 共通
    private Quaternion[] _initialLocalRotations;
    private Rigidbody[] _plateRigidbodies;

    // カタカタ揺れ + SFX
    private float _currentWobbleAngle = 0f;
    private float _rattleSfxTimer = 0f;
    private AudioSource _rattleAudioSource;

    void Awake()
    {
        if (logSettingsOnAwake)
        {
            Debug.Log($"[PinballWaterWheel] shape={shape}, plateAlwaysUpright={plateAlwaysUpright}, plates={(plates != null ? plates.Length : 0)}, radius={radius}, stretchLength={stretchLength}, rotationAxis={rotationAxis}, angularSpeed={angularSpeed}", this);
        }

        // カタカタ用 AudioSource (このオブジェクト自身に追加)
        if (rattleSfxClip != null)
        {
            _rattleAudioSource = gameObject.AddComponent<AudioSource>();
            _rattleAudioSource.playOnAwake = false;
            _rattleAudioSource.spatialBlend = 0f; // 2D 再生
        }

        // 自動スポーン: プレハブが指定されていれば等間隔配置
        if (platePrefab != null && autoSpawnPlateCount > 0)
        {
            AutoSpawnPlates();
        }

        if (plates == null || plates.Length == 0)
        {
            Debug.LogWarning("[PinballWaterWheel] plates 配列が空です。手動配置するか Plate Prefab + Auto Spawn Plate Count を設定してください。", this);
            return;
        }
        int n = plates.Length;
        _initialLocalOffsets = new Vector3[n];
        _initialLocalRotations = new Quaternion[n];
        _plateRigidbodies = new Rigidbody[n];
        _phases = new float[n];
        _pathRotationOffsets = new Quaternion[n];

        // Stadium 形状用に path length を先に確定 (Awake 序盤で計算)
        _pathLength = 2f * stretchLength + 2f * Mathf.PI * Mathf.Max(0.01f, radius);
        Vector3 axisN0 = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;

        for (int i = 0; i < n; i++)
        {
            if (plates[i] == null) continue;
            _initialLocalOffsets[i] = transform.InverseTransformPoint(plates[i].position);
            _initialLocalRotations[i] = Quaternion.Inverse(transform.rotation) * plates[i].rotation;

            if (shape == WheelShape.Stadium)
            {
                // 初期 phase は「設置位置に最も近いパス上の点」から逆引き
                _phases[i] = FindClosestPhase(_initialLocalOffsets[i]);

                // 初期姿勢オフセット: pathRot(初期phase) × offset = plates[i].rotation
                Vector3 t0 = ComputeTangent(_phases[i]);
                Vector3 o0 = Vector3.Cross(axisN0, t0);
                if (o0.sqrMagnitude < 0.0001f) o0 = Vector3.up;
                else o0 = o0.normalized;
                Vector3 wt0 = transform.TransformDirection(t0);
                Vector3 wo0 = transform.TransformDirection(o0);
                if (wt0.sqrMagnitude > 0.0001f && wo0.sqrMagnitude > 0.0001f)
                {
                    Quaternion initialPathRot = Quaternion.LookRotation(wt0, wo0);
                    _pathRotationOffsets[i] = Quaternion.Inverse(initialPathRot) * plates[i].rotation;
                }
                else
                {
                    _pathRotationOffsets[i] = Quaternion.identity;
                }
            }
            else
            {
                _phases[i] = (float)i / n; // Circle モードでは未使用
                _pathRotationOffsets[i] = Quaternion.identity;
            }

            if (autoAddKinematicRigidbody)
            {
                var rb = plates[i].GetComponent<Rigidbody>();
                if (rb == null) rb = plates[i].gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                _plateRigidbodies[i] = rb;
            }
            else
            {
                _plateRigidbodies[i] = plates[i].GetComponent<Rigidbody>();
            }

            if (autoApplyHighFrictionMaterial)
            {
                foreach (var col in plates[i].GetComponentsInChildren<Collider>())
                {
                    if (col.sharedMaterial == null)
                    {
                        col.sharedMaterial = new PhysicsMaterial("WaterWheelPlate")
                        {
                            bounciness = 0f,
                            dynamicFriction = 1f,
                            staticFriction = 1f,
                            bounceCombine = PhysicsMaterialCombine.Multiply,
                            frictionCombine = PhysicsMaterialCombine.Maximum,
                        };
                    }
                }
            }
        }

    }

    /// <summary>
    /// platePrefab を autoSpawnPlateCount 個 instantiate し、パス上に等間隔配置する。
    /// 各皿の初期 rotation も path tangent + outward に合わせて正しく設定する。
    /// </summary>
    void AutoSpawnPlates()
    {
        plates = new Transform[autoSpawnPlateCount];
        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;
        Quaternion prefabRot = platePrefab.transform.rotation;

        for (int i = 0; i < autoSpawnPlateCount; i++)
        {
            float phase = (float)i / autoSpawnPlateCount;

            GameObject go = Instantiate(platePrefab, transform);
            go.name = $"{platePrefab.name}_{i}";
            plates[i] = go.transform;

            // 等間隔配置: パス上の phase 位置 (Stadium / Circle 両対応; Circle は L=0 で同式)
            Vector3 localPos = SampleStadiumPath(phase);
            plates[i].position = transform.TransformPoint(localPos);

            // 初期回転: 水車式なら path 由来 × プレハブ姿勢、パターノスター式なら wheel × プレハブ姿勢
            Quaternion initialRot;
            if (plateAlwaysUpright)
            {
                initialRot = transform.rotation * prefabRot;
            }
            else
            {
                Quaternion pathRot = ComputePathRotationWorld(phase, axisN);
                initialRot = pathRot * prefabRot;
            }
            plates[i].rotation = initialRot;
        }
    }

    /// <summary>phase 位置における path 由来の world rotation (LookRotation(tangent, outward))</summary>
    Quaternion ComputePathRotationWorld(float phase, Vector3 axisN)
    {
        Vector3 tangent = ComputeTangent(phase);
        Vector3 outward = Vector3.Cross(axisN, tangent);
        if (outward.sqrMagnitude < 0.0001f) outward = Vector3.up;
        else outward = outward.normalized;

        Vector3 worldTangent = transform.TransformDirection(tangent);
        Vector3 worldOutward = transform.TransformDirection(outward);

        if (worldTangent.sqrMagnitude > 0.0001f && worldOutward.sqrMagnitude > 0.0001f)
        {
            return Quaternion.LookRotation(worldTangent, worldOutward);
        }
        return Quaternion.identity;
    }

    /// <summary>ローカル座標で渡された点に最も近い path 上の phase (0~1) を返す。</summary>
    float FindClosestPhase(Vector3 localPos)
    {
        const int samples = 360;
        float bestPhase = 0f;
        float bestDist = float.MaxValue;
        for (int i = 0; i < samples; i++)
        {
            float p = (float)i / samples;
            Vector3 pp = SampleStadiumPath(p);
            float d = (pp - localPos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                bestPhase = p;
            }
        }
        return bestPhase;
    }

    void FixedUpdate()
    {
        if (plates == null || plates.Length == 0) return;
        if (rotationAxis.sqrMagnitude < 0.0001f) return;

        if (shape == WheelShape.Circle)
        {
            FixedUpdateCircle();
        }
        else
        {
            FixedUpdateStadium();
        }
    }

    void Update()
    {
        // カタカタ揺れ: Y軸まわりに小さなランダム振動を追加 (主回転とは別レイヤ)
        if (rattleAmplitudeDegrees > 0f)
        {
            float t = Time.time * rattleFrequency;
            float targetWobble = (Mathf.PerlinNoise(t, 7.123f) - 0.5f) * 2f * rattleAmplitudeDegrees;
            float deltaWobble = targetWobble - _currentWobbleAngle;
            transform.Rotate(Vector3.up, deltaWobble, Space.Self);
            _currentWobbleAngle = targetWobble;
        }

        // カタカタ SFX: 一定間隔で「カタッ」を鳴らす
        if (_rattleAudioSource != null && rattleSfxClip != null && rattleSfxInterval > 0f)
        {
            _rattleSfxTimer += Time.deltaTime;
            if (_rattleSfxTimer >= rattleSfxInterval)
            {
                _rattleSfxTimer -= rattleSfxInterval;
                float v = rattleSfxPitchVariance;
                _rattleAudioSource.pitch = (v > 0f) ? 1f + Random.Range(-v, v) : 1f;
                _rattleAudioSource.PlayOneShot(rattleSfxClip, rattleSfxVolume);
            }
        }
    }

    void FixedUpdateCircle()
    {
        _currentAngle += angularSpeed * Time.fixedDeltaTime;
        Quaternion totalRot = Quaternion.AngleAxis(_currentAngle, rotationAxis.normalized);

        for (int i = 0; i < plates.Length; i++)
        {
            if (plates[i] == null) continue;
            Vector3 localPos = totalRot * _initialLocalOffsets[i];
            Vector3 worldPos = transform.TransformPoint(localPos);

            Quaternion localRot = plateAlwaysUpright
                ? _initialLocalRotations[i]
                : totalRot * _initialLocalRotations[i];
            Quaternion worldRot = transform.rotation * localRot;

            ApplyToPlate(i, worldPos, worldRot);
        }
    }

    void FixedUpdateStadium()
    {
        // angularSpeed (deg/s) をリム上の線速度 (m/s) に換算
        float r = Mathf.Max(0.01f, radius);
        float linearSpeed = angularSpeed * Mathf.Deg2Rad * r;
        float dPhase = (linearSpeed * Time.fixedDeltaTime) / Mathf.Max(0.001f, _pathLength);

        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;

        for (int i = 0; i < plates.Length; i++)
        {
            if (plates[i] == null) continue;
            _phases[i] = Mathf.Repeat(_phases[i] + dPhase, 1f);

            Vector3 localPos = SampleStadiumPath(_phases[i]);
            Vector3 worldPos = transform.TransformPoint(localPos);

            Quaternion worldRot;
            if (plateAlwaysUpright)
            {
                worldRot = transform.rotation * _initialLocalRotations[i];
            }
            else
            {
                // パス接線 (Z+) と外向き (Y+) で皿を向ける (水車式: 半円を進むと皿も回転する)
                Vector3 tangent = ComputeTangent(_phases[i]);
                // 外向き = rotationAxis × tangent → 直線では perp 方向、半円ではパス中心から外への方向
                Vector3 outward = Vector3.Cross(axisN, tangent);
                if (outward.sqrMagnitude < 0.0001f) outward = Vector3.up;
                else outward = outward.normalized;

                Vector3 worldTangent = transform.TransformDirection(tangent);
                Vector3 worldOutward = transform.TransformDirection(outward);

                if (worldTangent.sqrMagnitude > 0.0001f && worldOutward.sqrMagnitude > 0.0001f)
                {
                    Quaternion pathRot = Quaternion.LookRotation(worldTangent, worldOutward);
                    // 設置時のオフセットを掛けて、設置時の姿勢関係をパス進行方向に対して維持
                    worldRot = pathRot * _pathRotationOffsets[i];
                }
                else
                {
                    worldRot = transform.rotation * _initialLocalRotations[i];
                }
            }

            ApplyToPlate(i, worldPos, worldRot);

            // Debug log (plate0 のみ 0.5 秒間隔)
            if (logPlate0EachFrame && i == 0)
            {
                _logTimer += Time.fixedDeltaTime;
                if (_logTimer >= 0.5f)
                {
                    _logTimer = 0f;
                    Vector3 t = ComputeTangent(_phases[0]);
                    Vector3 o = Vector3.Cross(axisN, t);
                    if (o.sqrMagnitude < 0.0001f) o = Vector3.up; else o = o.normalized;
                    Debug.Log(
                        $"[WaterWheel] plate0 phase={_phases[0]:F3} alwaysUpright={plateAlwaysUpright}\n" +
                        $"  tangent(local)={t} outward(local)={o} axisN={axisN}\n" +
                        $"  worldTangent={transform.TransformDirection(t)} worldOutward={transform.TransformDirection(o)}\n" +
                        $"  worldRotEuler={worldRot.eulerAngles}\n" +
                        $"  pathOffsetEuler={_pathRotationOffsets[0].eulerAngles}\n" +
                        $"  shape={shape} pathLength={_pathLength:F3} radius={radius} stretchLength={stretchLength}",
                        this);
                }
            }
        }
    }

    void ApplyToPlate(int i, Vector3 worldPos, Quaternion worldRot)
    {
        var rb = _plateRigidbodies[i];
        if (rb != null && rb.isKinematic)
        {
            rb.MovePosition(worldPos);
            rb.MoveRotation(worldRot);
        }
        else
        {
            plates[i].position = worldPos;
            plates[i].rotation = worldRot;
        }
    }

    /// <summary>
    /// Stadium パス上 phase (0~1) の位置をローカル空間で返す。
    ///   Phase 0: +perp 側ストレート下端から開始
    ///   経路: 右ストレート上昇 → 上半円 → 左ストレート下降 → 下半円 → ループ
    /// </summary>
    Vector3 SampleStadiumPath(float phase01)
    {
        Vector3 stretchN = stretchAxis.sqrMagnitude > 0.0001f ? stretchAxis.normalized : Vector3.up;
        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;
        Vector3 perp = Vector3.Cross(axisN, stretchN);
        if (perp.sqrMagnitude < 0.0001f) perp = Vector3.right;
        else perp = perp.normalized;

        float L = stretchLength;
        float r = Mathf.Max(0.01f, radius);
        float total = 2f * L + 2f * Mathf.PI * r;
        float t = phase01 * total;

        if (t < L)
        {
            return perp * r + stretchN * (-L / 2f + t);
        }
        t -= L;
        if (t < Mathf.PI * r)
        {
            float theta = t / r;
            return stretchN * (L / 2f) + r * (Mathf.Cos(theta) * perp + Mathf.Sin(theta) * stretchN);
        }
        t -= Mathf.PI * r;
        if (t < L)
        {
            return -perp * r + stretchN * (L / 2f - t);
        }
        t -= L;
        float theta2 = t / r;
        return stretchN * (-L / 2f) + r * (-Mathf.Cos(theta2) * perp - Mathf.Sin(theta2) * stretchN);
    }

    /// <summary>
    /// Stadium パス上 phase の解析的タンジェント (進行方向の単位ベクトル) をローカル空間で返す。
    /// 数値微分は eps が小さすぎると差分が浮動小数点誤差に埋もれて 0 になるため使わない。
    /// </summary>
    Vector3 ComputeTangent(float phase01)
    {
        Vector3 stretchN = stretchAxis.sqrMagnitude > 0.0001f ? stretchAxis.normalized : Vector3.up;
        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;
        Vector3 perp = Vector3.Cross(axisN, stretchN);
        if (perp.sqrMagnitude < 0.0001f) perp = Vector3.right;
        else perp = perp.normalized;

        float L = stretchLength;
        float r = Mathf.Max(0.01f, radius);
        float total = 2f * L + 2f * Mathf.PI * r;
        float t = Mathf.Repeat(phase01, 1f) * total;

        // 右ストレート (上昇): 位置 = perp*r + stretchN*(-L/2 + t) → 接線 = stretchN
        if (t < L) return stretchN;
        t -= L;
        // 上半円: 位置 = stretchN*(L/2) + r*(cos(θ)*perp + sin(θ)*stretchN), θ = t/r
        //         接線 = -sin(θ)*perp + cos(θ)*stretchN
        if (t < Mathf.PI * r)
        {
            float theta = t / r;
            return (-Mathf.Sin(theta) * perp + Mathf.Cos(theta) * stretchN).normalized;
        }
        t -= Mathf.PI * r;
        // 左ストレート (下降): 位置 = -perp*r + stretchN*(L/2 - t) → 接線 = -stretchN
        if (t < L) return -stretchN;
        t -= L;
        // 下半円: 位置 = -stretchN*(L/2) + r*(-cos(θ)*perp - sin(θ)*stretchN), θ = t/r
        //         接線 = sin(θ)*perp - cos(θ)*stretchN
        float theta2 = t / r;
        return (Mathf.Sin(theta2) * perp - Mathf.Cos(theta2) * stretchN).normalized;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 worldAxis = transform.TransformDirection(rotationAxis.normalized);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position - worldAxis * 0.5f, transform.position + worldAxis * 0.5f);

        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        if (shape == WheelShape.Circle)
        {
            // 円の軌道
            if (plates != null)
            {
                foreach (var plate in plates)
                {
                    if (plate == null) continue;
                    float r = Vector3.Distance(plate.position, transform.position);
                    DrawCircleGizmo(transform.position, worldAxis, r, 36);
                }
            }
        }
        else
        {
            // Stadium の軌道をサンプリングして描画
            const int seg = 80;
            Vector3 prev = transform.TransformPoint(SampleStadiumPath(0f));
            for (int i = 1; i <= seg; i++)
            {
                Vector3 cur = transform.TransformPoint(SampleStadiumPath((float)i / seg));
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }

        // 各皿の +Y (緑) / +Z (青) 矢印を描画 (回転が適用されているか目視確認)
        if (drawPlateAxesGizmo && plates != null)
        {
            float arrowLen = 0.15f;
            foreach (var plate in plates)
            {
                if (plate == null) continue;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(plate.position, plate.position + plate.up * arrowLen);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(plate.position, plate.position + plate.forward * arrowLen);
            }
        }
    }

    static void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius, int segments)
    {
        Vector3 reference = Vector3.Cross(normal, Vector3.up);
        if (reference.sqrMagnitude < 0.001f) reference = Vector3.Cross(normal, Vector3.right);
        reference = reference.normalized * radius;

        Vector3 prev = center + reference;
        for (int i = 1; i <= segments; i++)
        {
            float ang = 360f / segments * i;
            Vector3 next = center + Quaternion.AngleAxis(ang, normal) * reference;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
