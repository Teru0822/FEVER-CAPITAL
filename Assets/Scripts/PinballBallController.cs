using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// ピンボールの「5」オブジェクト（ボール）制御スクリプト。
/// - Z軸正方向に重力と同じ大きさの力を常に受ける
/// - splitTargetTag のオブジェクトに衝突したら VFX Graph でバーストし、自分自身は消滅する
/// - 子ボールGameObjectは生成しない（VFX Graphの粒子のみで表現）
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

    [Header("VFX設定")]
    [Tooltip("分裂時に再生する VFX Graph プレハブ（VisualEffect コンポーネント付き）")]
    public VisualEffect splitVfxPrefab;

    [Tooltip("VFX Graph の Exposed int Property 名（粒子数を渡す）。空欄なら設定しない")]
    public string vfxSpawnCountProperty = "SpawnCount";

    [Tooltip("VFX を自動破棄するまでの秒数（VFX Graph には StopAction が無いため手動破棄）")]
    public float vfxLifetime = 3f;

    [Tooltip("バースト1回あたりの粒子数")]
    public int particleBurstCount = 100;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        SphereCollider col = GetComponent<SphereCollider>();
        if (col.material == null)
        {
            PhysicsMaterial mat = new PhysicsMaterial("BallPhysics");
            mat.bounciness = bounciness;
            mat.dynamicFriction = 0.3f;
            mat.staticFriction  = 0.3f;
            mat.frictionCombine  = PhysicsMaterialCombine.Average;
            mat.bounceCombine    = PhysicsMaterialCombine.Maximum;
            col.material = mat;
        }
    }

    void FixedUpdate()
    {
        rb.AddForce(new Vector3(0f, 0f, rb.mass * Mathf.Abs(Physics.gravity.y)), ForceMode.Force);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(splitTargetTag)) return;

        SpawnVfxBurst(transform.position);
        Destroy(gameObject);
    }

    void SpawnVfxBurst(Vector3 position)
    {
        if (splitVfxPrefab == null) return;

        VisualEffect vfx = Instantiate(splitVfxPrefab, position, Quaternion.identity);

        // 粒子数を Exposed Property 経由で渡す（プロパティが定義されている場合のみ）
        if (!string.IsNullOrEmpty(vfxSpawnCountProperty) && vfx.HasInt(vfxSpawnCountProperty))
            vfx.SetInt(vfxSpawnCountProperty, particleBurstCount);

        vfx.Play();

        // VFX Graph には ParticleSystem.StopAction.Destroy 相当が無いため明示的に破棄
        if (vfxLifetime > 0f)
            Destroy(vfx.gameObject, vfxLifetime);
    }
}
