using UnityEngine;

/// <summary>
/// ピンボールのプランジャー（「4」オブジェクト）制御スクリプト。
/// - Z軸のみ移動可能
/// - 現在位置をばねの自然長とする
/// - SPACEキー：Z軸正方向へ引っ張り（最大Z座標 maxZ まで）
/// - SPACEキーを離す：単振動（ばねの復元力）
/// - 衝突判定は「5」オブジェクトのみ
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PinballPlungerController : MonoBehaviour
{
    [Header("ばね設定")]
    [Tooltip("ばね定数（大きいほど振動が速い）")]
    [SerializeField] private float springConstant = 200f;

    [Tooltip("減衰係数（0にすると純粋な単振動）")]
    [SerializeField] private float damping = 2f;

    [Header("操作設定")]
    [Tooltip("SPACEキー押下時のZ軸移動速度")]
    [SerializeField] private float pullSpeed = 2f;

    [Tooltip("Z軸の最大座標（引っ張れる上限）")]
    [SerializeField] private float maxZ = 4.804f;

    [Header("衝突設定")]
    [Tooltip("当たり判定を持つオブジェクトの名前（「5」オブジェクト）")]
    [SerializeField] private string ballObjectName = "5";

    private Rigidbody rb;
    private float naturalZ;    // ばねの自然長（初期Z座標）
    private Collider myCollider;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        myCollider = GetComponent<Collider>();

        // 自然長として初期Z座標を記録
        naturalZ = transform.position.z;

        // Z軸のみ移動可能に制約
        rb.constraints = RigidbodyConstraints.FreezePositionX
                       | RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotation;
        rb.useGravity = false;

        // 「5」オブジェクト以外との衝突を無視
        SetupCollisionIgnore();
    }

    /// <summary>
    /// シーン内の全コライダーを検索し、「5」オブジェクト以外との衝突を無視する。
    /// </summary>
    void SetupCollisionIgnore()
    {
        if (myCollider == null) return;

        GameObject ball = GameObject.Find(ballObjectName);
        Collider ballCollider = ball != null ? ball.GetComponentInChildren<Collider>() : null;

        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider col in allColliders)
        {
            if (col == myCollider) continue;
            if (col == ballCollider) continue;
            Physics.IgnoreCollision(myCollider, col);
        }
    }

    void FixedUpdate()
    {
        float currentZ = rb.position.z;

        if (Input.GetKey(KeyCode.Space))
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
