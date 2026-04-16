using UnityEngine;

/// <summary>
/// ピンボールの「5」オブジェクト（ボール）制御スクリプト。
/// - 全オブジェクトに対して当たり判定を持つ（デフォルト動作）
/// - 「4」オブジェクトに押されて飛び出す
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

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true;
        // 全軸の移動・回転を許可（デフォルト）
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
}
