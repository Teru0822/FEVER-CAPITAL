using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

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

    [Tooltip("particleGeneration 以降に使用する VFX Graph プレハブ（VisualEffect 付き）。割り当てられている場合はこちらが優先される")]
    public VisualEffect splitVfxPrefab;

    [Tooltip("VFX Graph の Exposed Property 名（int）。粒子数を渡す。空欄なら設定しない")]
    public string vfxSpawnCountProperty = "SpawnCount";

    [Tooltip("VFX Graph の自動破棄までの秒数（VFX Graph には StopAction が無いため手動破棄）")]
    public float vfxLifetime = 3f;

    [Tooltip("particleGeneration 以降に使用するパーティクルプレハブ（VFX未割当時のフォールバック）")]
    public ParticleSystem splitParticlePrefab;

    [Tooltip("パーティクルバースト1回あたりの粒子数")]
    public int particleBurstCount = 30;

    // 実際にこのボールが分裂するときのXオフセット（世代ごとに半減）
    private float _currentXOffset;

    // 同一フレームでの二重分裂を防ぐフラグ（FixedUpdate冒頭でリセット）
    private bool _isSplitting = false;

    // このボールの世代（0=初期、1=1回分裂後、…）
    private int _generation = 0;

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
    }

    void FixedUpdate()
    {
        // フレーム開始時に分裂フラグをリセット（次フレームでの衝突を許可）
        _isSplitting = false;

        rb.AddForce(new Vector3(0f, 0f, rb.mass * Mathf.Abs(Physics.gravity.y)), ForceMode.Force);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_isSplitting) return;
        if (!collision.gameObject.CompareTag(splitTargetTag)) return;

        _isSplitting = true;

        // 衝突した瞬間の位置を記録（コルーチン待機中のズレを防ぐ）
        Vector3 posAtCollision = transform.position;

        StartCoroutine(SplitNextFrame(posAtCollision, collision.collider));
    }

    IEnumerator SplitNextFrame(Vector3 posAtCollision, Collider splitTargetCollider)
    {
        yield return new WaitForFixedUpdate();

        int nextGen = _generation + 1;

        // particleGeneration 以降はパーティクルバーストに切り替えて Rigidbody は生成しない
        // VFX Graph が割り当てられていれば優先、なければ ParticleSystem
        if (particleGeneration >= 0 && nextGen >= particleGeneration)
        {
            if (splitVfxPrefab != null)
            {
                SpawnVfxBurst(posAtCollision);
                ReturnToPool();
                yield break;
            }
            if (splitParticlePrefab != null)
            {
                SpawnParticleBurst(posAtCollision);
                ReturnToPool();
                yield break;
            }
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
            ctrl._generation = generation;
        }

        Rigidbody childRb = child.GetComponent<Rigidbody>();
        if (childRb != null)
        {
            childRb.linearVelocity = velocity;
            childRb.angularVelocity = Vector3.zero;
        }

        // splitTargetTag との衝突を一定時間無視
        Collider childCol = child.GetComponent<Collider>();
        if (childCol != null && ignoreCollider != null)
        {
            Physics.IgnoreCollision(childCol, ignoreCollider, true);
            ctrl?.StartCoroutine(RestoreCollision(childCol, ignoreCollider, ignoreCollisionDuration));
        }
    }

    void SpawnParticleBurst(Vector3 position)
    {
        ParticleSystem ps = Instantiate(splitParticlePrefab, position, Quaternion.identity);
        ps.Emit(particleBurstCount);
        // プレハブ側で StopAction = Destroy を設定しておくと自動消滅
    }

    void SpawnVfxBurst(Vector3 position)
    {
        VisualEffect vfx = Instantiate(splitVfxPrefab, position, Quaternion.identity);

        // 粒子数を Exposed Property 経由で渡す（プロパティが定義されている場合のみ）
        if (!string.IsNullOrEmpty(vfxSpawnCountProperty) && vfx.HasInt(vfxSpawnCountProperty))
            vfx.SetInt(vfxSpawnCountProperty, particleBurstCount);

        vfx.Play();

        // VFX Graph には ParticleSystem.StopAction.Destroy 相当が無いため明示的に破棄
        if (vfxLifetime > 0f)
            Destroy(vfx.gameObject, vfxLifetime);
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
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
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
