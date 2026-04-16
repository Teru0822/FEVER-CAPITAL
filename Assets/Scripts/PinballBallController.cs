using System.Collections;
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
    [SerializeField] private float splitSpread = 1f;

    [Tooltip("分裂後の速度の上限（m/s）")]
    [SerializeField] private float maxSplitSpeed = 3f;

    // 分裂済みフラグ（分裂したボールが再分裂しないようにする）
    private bool hasSplit = false;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

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
        if (hasSplit) return;
        if (!collision.gameObject.name.Equals(splitTargetName)) return;

        hasSplit = true;

        // ★ 衝突直後に速度と法線を記録（コルーチン後だと反発速度になって大きすぎる）
        Vector3 velAtCollision = rb.linearVelocity;
        float   cappedSpeed   = Mathf.Min(velAtCollision.magnitude, maxSplitSpeed);
        Vector3 safeVelocity  = (cappedSpeed > 0.01f)
            ? velAtCollision.normalized * cappedSpeed
            : Vector3.zero;

        // 衝突面の法線（「2」から離れる方向）
        Vector3 contactNormal  = collision.contacts[0].normal;
        Collider targetCollider = collision.collider;

        StartCoroutine(SplitNextFrame(safeVelocity, contactNormal, targetCollider));
    }

    /// <summary>
    /// 1フレーム待ってから分裂する。
    /// </summary>
    IEnumerator SplitNextFrame(Vector3 safeVelocity, Vector3 normal, Collider splitTargetCollider)
    {
        yield return new WaitForFixedUpdate();

        Vector3 halfScale = transform.localScale * 0.5f;

        // ★ 衝突面の法線方向に十分離してスポーン（コライダー重複を回避）
        Vector3 spawnBase = transform.position + normal * halfScale.x * 3f;
        Vector3 spreadDir = Vector3.right * splitSpread;

        SpawnSplitBall(spawnBase + Vector3.right * halfScale.x, safeVelocity + spreadDir, halfScale, splitTargetCollider);
        SpawnSplitBall(spawnBase + Vector3.left  * halfScale.x, safeVelocity - spreadDir, halfScale, splitTargetCollider);

        Destroy(gameObject);
    }

    /// <summary>
    /// 分裂後のボールを生成する。
    /// </summary>
    void SpawnSplitBall(Vector3 position, Vector3 velocity, Vector3 scale, Collider ignoreCollider)
    {
        GameObject child = Instantiate(gameObject, position, transform.rotation);
        child.transform.localScale = scale;

        PinballBallController ctrl = child.GetComponent<PinballBallController>();
        if (ctrl != null) ctrl.hasSplit = true;

        Rigidbody childRb = child.GetComponent<Rigidbody>();
        if (childRb != null)
            childRb.linearVelocity = velocity;

        // 生成直後0.4秒間は「2」との衝突を無視（重なりによる吹き飛びを防止）
        if (ignoreCollider != null)
        {
            Collider childCol = child.GetComponent<Collider>();
            if (childCol != null)
            {
                Physics.IgnoreCollision(childCol, ignoreCollider, true);
                ctrl?.StartCoroutine(RestoreCollision(childCol, ignoreCollider, 0.4f));
            }
        }
    }

    IEnumerator RestoreCollision(Collider col, Collider other, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (col != null && other != null)
            Physics.IgnoreCollision(col, other, false);
    }
}
