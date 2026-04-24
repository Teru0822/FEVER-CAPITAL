using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ピンボールのプランジャー（「4」オブジェクト）制御スクリプト。
/// - Z軸のみ移動可能
/// - 現在位置をばねの自然長とする
/// - SPACEキー：Z軸正方向へ引っ張り（最大Z座標 maxZ まで）
/// - SPACEキーを離す：単振動（ばねの復元力）
/// - 衝突判定：「4」自身は「5」のみ、子オブジェクト(col1等)は「1」とも衝突
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PinballPlungerController : MonoBehaviour
{
    [Header("ばね設定")]
    [Tooltip("ばね定数（大きいほど振動が速い）")]
    [SerializeField] private float springConstant = 800f;

    [Tooltip("減衰係数（0にすると純粋な単振動）")]
    [SerializeField] private float damping = 1f;

    [Header("操作設定")]
    [Tooltip("SPACEキー押下時のZ軸移動速度")]
    [SerializeField] private float pullSpeed = 2f;

    [Tooltip("Z軸の最大座標（引っ張れる上限）")]
    [SerializeField] private float maxZ = 4.804f;

    [Header("衝突設定")]
    [Tooltip("当たり判定を持つボールオブジェクトの名前")]
    [SerializeField] private string ballObjectName = "5";

    [Tooltip("col1が衝突するレールオブジェクトの名前")]
    [SerializeField] private string railObjectName = "1";

    private Rigidbody rb;
    private float naturalZ;    // ばねの自然長（初期Z座標）
    private PinballBallConfig _config;
    private float _scaleFactor = 1f;
    // 引っ張り目標 Z (gravity.z の符号で naturalZ から +/- に反転)
    private float _targetZ;
    // 引っ張り方向 (+1 で Z+ / -1 で Z- / 0 はガード)
    private float _pullDir = 1f;
    private float _worldPullSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        _config = FindFirstObjectByType<PinballBallConfig>();

        // 自然長として初期Z座標を記録
        naturalZ = transform.position.z;

        // pinballRoot のスケール/位置に追従: authored world maxZ を現在 root ポーズへ変換
        _scaleFactor = _config != null ? _config.CurrentScaleFactor : 1f;
        float authoredWorldMaxZ;
        if (_config != null)
        {
            Vector3 worldMax = _config.TransformAuthoredPoint(new Vector3(0f, 0f, maxZ));
            authoredWorldMaxZ = worldMax.z;
        }
        else
        {
            authoredWorldMaxZ = maxZ;
        }
        _worldPullSpeed = pullSpeed * _scaleFactor;

        // 引っ張り方向は gravity.z の符号に追従
        // gravity.z > 0 → naturalZ から +Z へ引っ張る (authored maxZ 側)
        // gravity.z < 0 → naturalZ から -Z へ引っ張る (naturalZ を挟んで反対側)
        float gz = (_config != null) ? _config.EffectiveGravity.z : 1f;
        _pullDir = (gz < 0f) ? -1f : 1f;
        float pullDistance = Mathf.Abs(authoredWorldMaxZ - naturalZ);
        _targetZ = naturalZ + _pullDir * pullDistance;

        // Z軸のみ移動可能に制約
        rb.constraints = RigidbodyConstraints.FreezePositionX
                       | RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotation;
        rb.useGravity = false;

        // すり抜け防止：高速移動時も連続的に衝突検出
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 衝突ルールを設定
        SetupCollisionIgnore();
    }

    /// <summary>
    /// 衝突ルール：
    /// - 「4」自身のコライダー → 「5」のみ衝突、他は無視
    /// - 子オブジェクト（col1等）のコライダー → 「5」と「1」に衝突、他は無視
    /// </summary>
    void SetupCollisionIgnore()
    {
        // 「5」（ボール）のコライダーを取得
        GameObject ball = GameObject.Find(ballObjectName);
        Collider[] ballColliders = ball != null
            ? ball.GetComponentsInChildren<Collider>()
            : new Collider[0];

        // 「1」（レール）のコライダーを取得
        GameObject rail = GameObject.Find(railObjectName);
        Collider[] railColliders = rail != null
            ? rail.GetComponentsInChildren<Collider>()
            : new Collider[0];

        // シーン内の全コライダー
        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);

        // 「4」自身のコライダー（直接アタッチ分）
        Collider myCollider = GetComponent<Collider>();

        // 「4」の子コライダー（col1等）
        Collider[] childColliders = GetComponentsInChildren<Collider>();

        foreach (Collider mine in childColliders)
        {
            bool isSelf     = (mine == myCollider);
            bool isChild    = !isSelf;   // col1などの子コライダー

            foreach (Collider other in allColliders)
            {
                // 自分自身はスキップ
                bool isMyCollider = System.Array.IndexOf(childColliders, other) >= 0;
                if (isMyCollider) continue;

                bool isBall = System.Array.IndexOf(ballColliders, other) >= 0;
                bool isRail = System.Array.IndexOf(railColliders, other) >= 0;

                if (isSelf)
                {
                    // 「4」本体：「5」以外を無視
                    if (!isBall)
                        Physics.IgnoreCollision(mine, other);
                }
                else
                {
                    // 子（col1等）：「5」と「1」以外を無視
                    if (!isBall && !isRail)
                        Physics.IgnoreCollision(mine, other);
                }
            }
        }
    }

    void FixedUpdate()
    {
        float currentZ = rb.position.z;

        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
        {
            // SPACEキー押下中：gravity.z の符号方向へ引っ張る
            // _pullDir > 0 なら currentZ < _targetZ の間は加速、到達で停止
            // _pullDir < 0 なら currentZ > _targetZ の間は加速、到達で停止
            bool stillPulling = (_pullDir > 0f) ? (currentZ < _targetZ) : (currentZ > _targetZ);
            if (stillPulling)
            {
                Vector3 vel = rb.linearVelocity;
                vel.z = _worldPullSpeed * _pullDir;
                rb.linearVelocity = vel;
            }
            else
            {
                // 目標到達で停止 + クランプ
                Vector3 vel = rb.linearVelocity;
                vel.z = 0f;
                rb.linearVelocity = vel;
                Vector3 pos = rb.position;
                pos.z = _targetZ;
                rb.MovePosition(pos);
            }
        }
        else
        {
            // SPACEキー離し：ばねの復元力で単振動
            float displacement = currentZ - naturalZ;
            float springForce = -springConstant * displacement;
            float dampForce   = -damping * rb.linearVelocity.z;
            rb.AddForce(new Vector3(0f, 0f, springForce + dampForce), ForceMode.Force);
        }
    }
}
