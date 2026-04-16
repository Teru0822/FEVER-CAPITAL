using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ピンボールの「5」オブジェクト（ボール）制御スクリプト。
/// - 全オブジェクトに対して当たり判定を持つ
/// - 「4」オブジェクトに押されて飛び出す
/// - Z軸正方向に重力と同じ大きさの力を常に受ける
/// - splitTargetTag のオブジェクトに衝突するたびに分裂する
///   - 分裂数・サイズ・X方向オフセットは Inspector で設定可
///   - 世代ごとにXオフセットは自動的に半分になる
/// - 最適化:
///   - ボール同士の衝突を Layer Collision Matrix で無効化
///   - Object Pool による Instantiate/Destroy 削減
///   - particleGeneration 以降はパーティクルバーストに切り替え
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class PinballBallController : MonoBehaviour
{
    [Header("ボール設定")]
    public float mass = 0.5f;

    public float bounciness = 0.5f;

    [Header("分裂設定")]
    [Tooltip("衝突時に分裂するオブジェクトのタグ（Unity の Tag を指定）")]
    public string splitTargetTag = "Splitter";

    [Tooltip("分裂後に左右へ広がる速度（m/s）")]
    public float splitSpread = 1f;

    [Tooltip("分裂数（2以上）")]
    [Min(2)]
    public int splitCount = 2;

    [Tooltip("分裂後のスケール倍率（0〜1）。例：0.5なら半分のサイズ")]
    [Range(0.01f, 1f)]
    public float splitScaleRatio = 0.5f;

    [Tooltip("分裂後のスポーン位置をY軸上方向にずらす量")]
    public float spawnUpOffset = 0.5f;

    [Tooltip("一回目の分裂時の左右Xオフセット（以降の世代は自動的に半分になる）")]
    public float spawnXOffset = 0.5f;

    [Tooltip("splitTargetTagとの衝突を無視する時間（秒）")]
    public float ignoreCollisionDuration = 0.4f;

    [Header("最適化設定")]
    [Tooltip("分裂ボールが配置されるレイヤー名（同レイヤー同士の衝突は自動で無効化される）")]
    public string ballLayerName = "Ball";

    [Tooltip("この世代以降は Rigidbody ボールではなく ParticleSystem で表現する（負の値で無効）")]
    public int particleGeneration = 4;

    [Tooltip("particleGeneration 以降に使用するパーティクルプレハブ（ParticleSystem 付き）。粒子数や方向はプレハブ側のEmission/Shape/Max Particlesで設定")]
    public ParticleSystem splitParticlePrefab;

    [Header("手動物理設定")]
    [Tooltip("(particleGeneration - 1) と (particleGeneration - 2) 世代のボールを Rigidbody ではなく手動物理で動かす")]
    public bool useManualPhysicsForHighGen = true;

    [Tooltip("手動物理ボールが動ける範囲のXmin")]
    public float manualBoundsXMin = 0.812f;

    [Tooltip("手動物理ボールが動ける範囲のXmax")]
    public float manualBoundsXMax = 2.425f;

    [Tooltip("手動物理ボールが動ける範囲のYmin（地面）")]
    public float manualBoundsYMin = 0.008f;

    [Tooltip("手動物理ボールが動ける範囲のYmax（地面）")]
    public float manualBoundsYMax = 0.113f;

    [Tooltip("手動物理ボールが動ける範囲のZmax（奥側の壁）")]
    public float manualBoundsZMax = 4.468f;

    [Tooltip("手動物理ボールの跳ね返り係数（0〜1）")]
    [Range(0f, 1f)]
    public float manualBounceFactor = 0.6f;

    [Tooltip("手動物理ボールが Splitter を検知する球判定の半径")]
    public float manualSplitterCheckRadius = 0.05f;

    [Tooltip("手動物理ボールの寿命（秒）。経過するとプールへ自動返却される。0以下で無効")]
    public float manualLifetime = 5f;

    [Header("パーティクル非表示領域")]
    [Tooltip("X座標がこの値以下かつZ座標がhideParticleZMin以上の領域ではパーティクルを生成しない")]
    public float hideParticleXMax = 0.75f;

    [Tooltip("Z座標がこの値以上かつX座標がhideParticleXMax以下の領域ではパーティクルを生成しない")]
    public float hideParticleZMin = 4.515f;

    // 実際にこのボールが分裂するときのXオフセット（世代ごとに半減）
    private float _currentXOffset;

    // 同一フレームでの二重分裂を防ぐフラグ（FixedUpdate冒頭でリセット）
    private bool _isSplitting = false;

    // このボールの世代（0=初期、1=1回分裂後、…）
    private int _generation = 0;

    // 手動物理モードフラグ（Rigidbodyを使わず Update で位置・速度を計算）
    private bool _isManualPhysics = false;
    private Vector3 _manualVelocity = Vector3.zero;
    private Collider _ignoredSplitter = null;
    private float _ignoreSplitterUntil = 0f;
    private float _manualExpireAt = 0f;

    private Rigidbody rb;
    private SphereCollider sphereCol;

    // 静的Object Pool（全ボールで共有）
    private static Stack<GameObject> _pool = new Stack<GameObject>();
    private static int _ballLayer = -1;
    private static bool _ballLayerCollisionDisabled = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        sphereCol = GetComponent<SphereCollider>();
        if (sphereCol.material == null)
        {
            PhysicsMaterial mat = new PhysicsMaterial("BallPhysics");
            mat.bounciness = bounciness;
            mat.dynamicFriction = 0.3f;
            mat.staticFriction  = 0.3f;
            mat.frictionCombine  = PhysicsMaterialCombine.Average;
            mat.bounceCombine    = PhysicsMaterialCombine.Maximum;
            sphereCol.material = mat;
        }

        // 初期値（子ボールではSpawnSplitBallがOnEnable後に上書きする）
        _currentXOffset = spawnXOffset;

        SetupBallLayer();
    }

    /// <summary>
    /// 自分自身を ballLayerName のレイヤーに割り当て、
    /// 同レイヤー同士の衝突を一度だけ無効化する。
    /// </summary>
    void SetupBallLayer()
    {
        if (string.IsNullOrEmpty(ballLayerName)) return;

        if (_ballLayer < 0)
            _ballLayer = LayerMask.NameToLayer(ballLayerName);

        if (_ballLayer < 0)
        {
            Debug.LogWarning($"[PinballBallController] Layer '{ballLayerName}' が見つかりません。Tag/Layer に追加してください。");
            return;
        }

        gameObject.layer = _ballLayer;

        if (!_ballLayerCollisionDisabled)
        {
            Physics.IgnoreLayerCollision(_ballLayer, _ballLayer, true);
            _ballLayerCollisionDisabled = true;
        }
    }

    void OnEnable()
    {
        // プールから再利用された時の初期化
        _isSplitting = false;
        _isManualPhysics = false;
        _manualVelocity = Vector3.zero;
        _ignoredSplitter = null;
        _ignoreSplitterUntil = 0f;
        _manualExpireAt = 0f;
    }

    void FixedUpdate()
    {
        // フレーム開始時に分裂フラグをリセット（次フレームでの衝突を許可）
        _isSplitting = false;

        if (_isManualPhysics) return;

        rb.AddForce(new Vector3(0f, 0f, rb.mass * Mathf.Abs(Physics.gravity.y)), ForceMode.Force);
    }

    void Update()
    {
        if (!_isManualPhysics) return;

        // 寿命到達でヒエラルキーから完全削除（プール返却ではなく Destroy）
        if (manualLifetime > 0f && Time.time >= _manualExpireAt)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        float gravityMag = Mathf.Abs(Physics.gravity.y);

        // 重力（Y軸負方向）+ Z軸正方向の加速度
        _manualVelocity.y += Physics.gravity.y * dt;
        _manualVelocity.z += gravityMag * dt;

        Vector3 pos = transform.position + _manualVelocity * dt;

        // 境界 + 跳ね返り
        if (pos.x < manualBoundsXMin)
        {
            pos.x = manualBoundsXMin;
            if (_manualVelocity.x < 0f) _manualVelocity.x = -_manualVelocity.x * manualBounceFactor;
        }
        else if (pos.x > manualBoundsXMax)
        {
            pos.x = manualBoundsXMax;
            if (_manualVelocity.x > 0f) _manualVelocity.x = -_manualVelocity.x * manualBounceFactor;
        }
        if (pos.y < manualBoundsYMin)
        {
            pos.y = manualBoundsYMin;
            if (_manualVelocity.y < 0f) _manualVelocity.y = -_manualVelocity.y * manualBounceFactor;
        }
        if (pos.y > manualBoundsYMax)
        {
            pos.y = manualBoundsYMax;
            if (_manualVelocity.y < 0f) _manualVelocity.y = -_manualVelocity.y * manualBounceFactor;
        }
        if (pos.z > manualBoundsZMax)
        {
            pos.z = manualBoundsZMax;
            if (_manualVelocity.z > 0f) _manualVelocity.z = -_manualVelocity.z * manualBounceFactor;
        }

        transform.position = pos;

        // Splitter 検知
        if (_isSplitting) return;

        Collider[] hits = Physics.OverlapSphere(pos, manualSplitterCheckRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;
            if (hit == _ignoredSplitter && Time.time < _ignoreSplitterUntil) continue;
            if (!hit.CompareTag(splitTargetTag)) continue;

            _isSplitting = true;
            StartCoroutine(SplitNextFrame(pos, hit));
            break;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_isSplitting) return;
        if (_isManualPhysics) return;
        if (!collision.gameObject.CompareTag(splitTargetTag)) return;

        _isSplitting = true;

        // 衝突した瞬間の位置を記録（コルーチン待機中のズレを防ぐ）
        Vector3 posAtCollision = transform.position;

        StartCoroutine(SplitNextFrame(posAtCollision, collision.collider));
    }

    /// <summary>
    /// 世代に応じて Rigidbody / 手動物理の切り替えと初期速度・無視Splitter設定をまとめて行う。
    /// </summary>
    public void ConfigurePhysicsMode(Vector3 initialVelocity, Collider ignoreSplitter, int generation)
    {
        _generation = generation;

        bool useManual = useManualPhysicsForHighGen
                      && particleGeneration > 0
                      && (generation == particleGeneration - 1);

        _isManualPhysics = useManual;

        if (useManual)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;

            _manualVelocity = initialVelocity;
            _ignoredSplitter = ignoreSplitter;
            _ignoreSplitterUntil = Time.time + ignoreCollisionDuration;
            _manualExpireAt = Time.time + manualLifetime;
        }
        else
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = initialVelocity;
            rb.angularVelocity = Vector3.zero;

            _ignoredSplitter = null;
            _ignoreSplitterUntil = 0f;

            // 通常Rigidbodyモードでは Splitter 物理衝突を一定時間無視
            Collider childCol = GetComponent<Collider>();
            if (childCol != null && ignoreSplitter != null)
            {
                Physics.IgnoreCollision(childCol, ignoreSplitter, true);
                StartCoroutine(RestoreCollision(childCol, ignoreSplitter, ignoreCollisionDuration));
            }
        }
    }

    IEnumerator SplitNextFrame(Vector3 posAtCollision, Collider splitTargetCollider)
    {
        yield return new WaitForFixedUpdate();

        int nextGen = _generation + 1;

        // particleGeneration 以降はパーティクルバーストに切り替えて Rigidbody は生成しない
        if (particleGeneration >= 0 && nextGen >= particleGeneration && splitParticlePrefab != null)
        {
            SpawnParticleBurst(posAtCollision);
            ReturnToPool();
            yield break;
        }

        int count = Mathf.Max(2, splitCount);
        Vector3 nextScale = transform.localScale * splitScaleRatio;
        Vector3 spawnBase = posAtCollision + Vector3.up * spawnUpOffset;

        // -_currentXOffset 〜 +_currentXOffset の範囲に count 個を等間隔配置
        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0f : ((float)i / (count - 1)) * 2f - 1f; // -1 〜 +1
            Vector3 spawnPos = spawnBase + Vector3.right * (_currentXOffset * t);
            Vector3 vel = Vector3.right * (splitSpread * t);
            SpawnSplitBall(spawnPos, vel, nextScale, splitTargetCollider, nextGen);
        }

        ReturnToPool();
    }

    void SpawnSplitBall(Vector3 position, Vector3 velocity, Vector3 scale, Collider ignoreCollider, int generation)
    {
        GameObject child = GetFromPool(position);
        child.transform.localScale = scale;

        PinballBallController ctrl = child.GetComponent<PinballBallController>();
        if (ctrl != null)
        {
            ctrl._currentXOffset = _currentXOffset / 2f;
            ctrl.ConfigurePhysicsMode(velocity, ignoreCollider, generation);
        }
    }

    void SpawnParticleBurst(Vector3 position)
    {
        // 非表示領域内ならパーティクルを生成しない
        if (position.x <= hideParticleXMax && position.z >= hideParticleZMin)
            return;

        // プレハブの回転を引き継ぐ（Shape モジュールの方向設定を活かすため）
        ParticleSystem ps = Instantiate(splitParticlePrefab, position, splitParticlePrefab.transform.rotation);

        // 領域に侵入した粒子を毎フレーム消滅させる Culler を追加
        ParticleRegionCuller culler = ps.gameObject.AddComponent<ParticleRegionCuller>();
        culler.hideXMax = hideParticleXMax;
        culler.hideZMin = hideParticleZMin;

        // プレハブ側の Emission / Max Particles / Bursts 設定を尊重する
        ps.Play();
        // プレハブ側で StopAction = Destroy を設定しておくと自動消滅
    }

    /// <summary>
    /// プールから1個取り出す。空ならInstantiateで新規作成。
    /// </summary>
    GameObject GetFromPool(Vector3 position)
    {
        while (_pool.Count > 0)
        {
            GameObject obj = _pool.Pop();
            if (obj == null) continue; // シーン遷移などで破棄済みの参照はスキップ
            obj.transform.position = position;
            obj.transform.rotation = transform.rotation;
            obj.SetActive(true);
            return obj;
        }
        return Instantiate(gameObject, position, transform.rotation);
    }

    /// <summary>
    /// このボールを非アクティブ化してプールへ返す。
    /// </summary>
    void ReturnToPool()
    {
        // 手動物理モードのままだと kinematic Rigidbody への velocity 代入で警告が出るため通常モードへ戻す
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        _isManualPhysics = false;
        _manualVelocity = Vector3.zero;

        gameObject.SetActive(false);
        _pool.Push(gameObject);
    }

    IEnumerator RestoreCollision(Collider col, Collider other, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (col != null && other != null)
            Physics.IgnoreCollision(col, other, false);
    }
}
