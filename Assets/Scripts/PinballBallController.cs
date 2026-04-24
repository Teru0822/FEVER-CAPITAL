using System.Collections;
using UnityEngine;

/// <summary>
/// ピンボールの「5」オブジェクト (gen 0) 専用コントローラ。
/// - Rigidbody + SphereCollider + OnCollisionEnter で動作
/// - Splitter と衝突すると PinballBallManager に gen 1 の分裂子を生成依頼し、自身を破棄
/// - gen ≥ 1 の挙動はすべて PinballBallManager が Jobs + Burst で一括管理する
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class PinballBallController : MonoBehaviour
{
    [Header("ボール設定")]
    public float mass = 0.5f;

    public float bounciness = 0.5f;

    [Header("分裂設定")]
    [Tooltip("衝突時に分裂するオブジェクトのタグ")]
    public string splitTargetTag = "Splitter";

    [Tooltip("分裂後に左右へ広がる速度（m/s）")]
    public float splitSpread = 1f;

    [Tooltip("分裂数（2以上）")]
    [Min(2)]
    public int splitCount = 2;

    [Tooltip("分裂後のスケール倍率（0〜1）")]
    [Range(0.01f, 1f)]
    public float splitScaleRatio = 0.5f;

    [Tooltip("分裂後のスポーン位置をY軸上方向にずらす量（互換用。Managerは floorY+radius を使用）")]
    public float spawnUpOffset = 0.5f;

    [Tooltip("一回目の分裂時の左右Xオフセット（以降の世代は自動的に半減）")]
    public float spawnXOffset = 0.5f;

    [Tooltip("Splitter との衝突を無視する時間（秒）")]
    public float ignoreCollisionDuration = 0.4f;

    [Header("検知半径")]
    [Tooltip("gen 1 分裂子の検知半径。以降の世代は splitScaleRatio で縮小")]
    public float initialDetectionRadius = 0.08f;

    [Header("移動境界 (gen ≥ 1)")]
    public float manualBoundsXMin = 0.81f;
    public float manualBoundsXMax = 2.4257f;
    public float manualBoundsZMax = 4.468f;

    [Tooltip("空間ハッシュの範囲下限 Z (壁としては作用せずグリッド最小値のみ)")]
    public float manualBoundsZMin = -2f;

    [Header("ボール同士の衝突")]
    [Tooltip("gen ≥ 1 同士の位置分離 + 法線方向の弱い反発を行うか")]
    public bool enableBallBallCollision = true;

    [Tooltip("gen ≥ 1 ボールの跳ね返り係数（0〜1）")]
    [Range(0f, 1f)]
    public float manualBounceFactor = 0.6f;

    [Tooltip("gen ≥ 1 ボールの寿命（秒）。0以下で無効")]
    public float manualLifetime = 5f;

    [Header("最適化設定")]
    [Tooltip("分裂ボールが配置されるレイヤー名（同レイヤー同士の衝突は自動で無効化される）")]
    public string ballLayerName = "Ball";

    [Tooltip("この世代以降は ParticleSystem バーストで表現する（負の値で無効）")]
    public int particleGeneration = 4;

    [Tooltip("particleGeneration 以降に使用するパーティクルプレハブ")]
    public ParticleSystem splitParticlePrefab;

    [Header("パーティクル非表示領域")]
    public float hideParticleXMax = 0.75f;
    public float hideParticleZMin = 4.515f;

    private bool _isSplitting = false;

    private Rigidbody rb;
    private SphereCollider sphereCol;
    private PinballBallConfig _config;
    // pinballRoot でスケール追従する時は Unity 重力を切って手動で Y/Z 双方に scaled gravity を掛ける
    private bool _useManualGravity = false;

    private static int _ballLayer = -1;
    private static bool _ballLayerCollisionDisabled = false;

    /// <summary>現在シーンに存在する gen 0 ボールの数 (デバッグ用)</summary>
    public static int AliveGen0Count { get; private set; }

    void OnEnable() { AliveGen0Count++; }
    void OnDisable() { AliveGen0Count--; }

    void Awake()
    {
        _config = FindFirstObjectByType<PinballBallConfig>();
        // pinballRoot が設定されている時は Unity 重力を切って手動適用
        // (Unity のグローバル gravity は scaleFactor で変更できないため)
        _useManualGravity = _config != null && _config.pinballRoot != null;

        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = !_useManualGravity;
        rb.constraints = RigidbodyConstraints.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        sphereCol = GetComponent<SphereCollider>();
        if (sphereCol.material == null)
        {
            PhysicsMaterial mat = new PhysicsMaterial("BallPhysics");
            mat.bounciness = bounciness;
            mat.dynamicFriction = 0.3f;
            mat.staticFriction = 0.3f;
            mat.frictionCombine = PhysicsMaterialCombine.Average;
            mat.bounceCombine = PhysicsMaterialCombine.Maximum;
            sphereCol.material = mat;
        }

        SetupBallLayer();
    }

    void SetupBallLayer()
    {
        if (string.IsNullOrEmpty(ballLayerName)) return;

        if (_ballLayer < 0)
            _ballLayer = LayerMask.NameToLayer(ballLayerName);

        if (_ballLayer < 0)
        {
            Debug.LogWarning($"[PinballBallController] Layer '{ballLayerName}' が見つかりません。");
            return;
        }

        gameObject.layer = _ballLayer;

        if (!_ballLayerCollisionDisabled)
        {
            Physics.IgnoreLayerCollision(_ballLayer, _ballLayer, true);
            _ballLayerCollisionDisabled = true;
        }
    }

    void FixedUpdate()
    {
        _isSplitting = false;
        if (_useManualGravity && _config != null)
        {
            // Config.gravity を scale 倍して手動適用 (Unity 重力は OFF)
            float s = _config.CurrentScaleFactor;
            Vector3 grav = _config.EffectiveGravity;
            rb.AddForce(rb.mass * s * grav, ForceMode.Force);
        }
        else
        {
            // 従来互換: Unity 重力 (-Y) + Z+ 方向の相殺力
            float g = Mathf.Abs(Physics.gravity.y);
            rb.AddForce(new Vector3(0f, 0f, rb.mass * g), ForceMode.Force);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_isSplitting) return;
        if (!collision.gameObject.CompareTag(splitTargetTag)) return;

        _isSplitting = true;
        Vector3 posAtCollision = transform.position;
        StartCoroutine(SplitNextFrame(posAtCollision, collision.collider));
    }

    IEnumerator SplitNextFrame(Vector3 posAtCollision, Collider splitTargetCollider)
    {
        yield return new WaitForFixedUpdate();

        float sphereRadiusWorld = sphereCol.bounds.extents.y;
        PinballBallManager.Instance.ProduceGen1Children(this, posAtCollision, splitTargetCollider, sphereRadiusWorld);

        Destroy(gameObject);
    }
}
