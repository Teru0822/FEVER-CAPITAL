using UnityEngine;

/// <summary>
/// ピンボールの「5」オブジェクト（ボール）制御スクリプト。
/// - 全オブジェクトに対して当たり判定を持つ
/// - 「4」オブジェクトに押されて飛び出す
/// - Z軸正方向に重力と同じ大きさの力を常に受ける
/// - 「2」オブジェクトに衝突すると二分の一サイズに縮小して二つに分裂する
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class PinballBallController : MonoBehaviour
{
    [Header("ボール設定")]
    [Tooltip("ボールの質量")]
    [SerializeField] private float mass = 0.5f;

    [Tooltip("弾性係数（0:完全非弾性 〜 1:完全弾性）")]
    [SerializeField] private float bounciness = 0.5f;

    [Header("分裂設定")]
    [Tooltip("衝突時に分裂するオブジェクトの名前")]
    [SerializeField] private string splitTargetName = "2";

    [Tooltip("分裂後に左右へ広がる速度（m/s）")]
    [SerializeField] private float splitSpread = 2f;

    // 分裂済みフラグ（分裂したボールが再分裂しないようにする）
    private bool hasSplit = false;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;

        // Physicマテリアルで弾性設定
        SphereCollider col = GetComponent<SphereCollider>();
        PhysicsMaterial mat = new PhysicsMaterial("BallPhysics");
        mat.bounciness = bounciness;
        mat.dynamicFriction = 0.3f;
        mat.staticFriction  = 0.3f;
        mat.frictionCombine  = PhysicsMaterialCombine.Average;
        mat.bounceCombine    = PhysicsMaterialCombine.Maximum;
        col.material = mat;
    }

    void FixedUpdate()
    {
        // Z軸正方向に重力と同じ大きさの力を常に加える
        rb.AddForce(new Vector3(0f, 0f, rb.mass * Mathf.Abs(Physics.gravity.y)), ForceMode.Force);
    }

    void OnCollisionEnter(Collision collision)
    {
        // 分裂済み、または衝突相手が「2」でなければ何もしない
        if (hasSplit) return;
        if (!collision.gameObject.name.Equals(splitTargetName)) return;

        Split();
    }

    /// <summary>
    /// 自身を二分の一サイズの二つのボールに分裂させる。
    /// </summary>
    void Split()
    {
        hasSplit = true;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentPos      = transform.position;
        Vector3 halfScale       = transform.localScale * 0.5f;

        // 分裂方向：現在の速度にX軸方向のオフセットを加えて左右に広げる
        Vector3 spreadDir = Vector3.right * splitSpread;

        SpawnSplitBall(currentPos + Vector3.right * halfScale.x, currentVelocity + spreadDir, halfScale);
        SpawnSplitBall(currentPos + Vector3.left  * halfScale.x, currentVelocity - spreadDir, halfScale);

        Destroy(gameObject);
    }

    /// <summary>
    /// 分裂後のボールを生成する。
    /// </summary>
    void SpawnSplitBall(Vector3 position, Vector3 velocity, Vector3 scale)
    {
        GameObject child = Instantiate(gameObject, position, transform.rotation);
        child.transform.localScale = scale;

        // 分裂済みフラグを設定して再分裂を防止
        PinballBallController ctrl = child.GetComponent<PinballBallController>();
        if (ctrl != null) ctrl.hasSplit = true;

        Rigidbody childRb = child.GetComponent<Rigidbody>();
        if (childRb != null) childRb.linearVelocity = velocity;
    }
}
