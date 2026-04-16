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

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 自然長として初期Z座標を記録
        naturalZ = transform.position.z;

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
            // SPACEキー押下中：Z軸正方向へ引っ張る
            if (currentZ < maxZ)
            {
                // pullSpeedの速度でZ+方向へ移動
                Vector3 vel = rb.linearVelocity;
                vel.z = pullSpeed;
                rb.linearVelocity = vel;
            }
            else
            {
                // maxZに達したら停止
                Vector3 vel = rb.linearVelocity;
                vel.z = 0f;
                rb.linearVelocity = vel;
                // maxZを超えないようにクランプ
                Vector3 pos = rb.position;
                pos.z = maxZ;
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
