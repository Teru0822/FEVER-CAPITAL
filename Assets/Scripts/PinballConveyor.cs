using UnityEngine;

/// <summary>
/// ピンボール盤面用ベルトコンベア。
/// このオブジェクトの Collider に接触/侵入した Rigidbody を、指定方向 (既定: ローカル +Y) へ
/// 一定速度で押し出す。pinballRoot のスケール倍率に追従する。
///
/// バウンス対策:
///   - 同 Collider に bounciness=0 の PhysicsMaterial を Awake で自動付与する
///   - ベルト方向の速度成分は常に beltSpeed に「上書き」する (弾かれた瞬間の余剰も即座に修正)
///   - 垂直方向に持ち上げたい場合は Collider を Trigger にすると確実 (バウンスが完全消去される)
///
/// 使い方:
///   1. 空 GameObject に BoxCollider 等を付け、ベルト面の形状にスケール
///   2. 本コンポーネントをアタッチ
///   3. direction (ローカル軸) を押し出したい向きに、speed を m/s で設定
///   4. **垂直に持ち上げたいなら Collider の Is Trigger を ON 推奨**
/// </summary>
[RequireComponent(typeof(Collider))]
public class PinballConveyor : MonoBehaviour
{
    [Header("ベルト方向・速度")]
    [Tooltip("ベルトの押し出し方向。既定 (0, 1, 0) でローカル +Y (上方向)")]
    [SerializeField] private Vector3 direction = Vector3.up;

    [Tooltip("方向をローカル軸で解釈する (true) / ワールド軸で解釈する (false)")]
    [SerializeField] private bool useLocalDirection = true;

    [Tooltip("ベルト速度 (m/s)。pinballRoot のスケール倍率に追従する")]
    [SerializeField, Min(0f)] private float speed = 2f;

    [Header("挙動オプション")]
    [Tooltip("接触中のボールに掛かる重力を打ち消す追加上向き加速度 (m/s²)。垂直リフトで沈み込みを防ぐ。0 で無効。")]
    [SerializeField, Min(0f)] private float gravityCancelBoost = 0f;

    [Range(0f, 1f)]
    [Tooltip("垂直方向 (ベルト方向以外) の速度を毎フレーム何割減らすか (バウンス収束用)。0=無、1=完全停止")]
    [SerializeField] private float perpDamping = 0.3f;

    [Tooltip("Awake 時にこの Collider へ bounciness=0 の PhysicsMaterial を自動付与する")]
    [SerializeField] private bool autoApplyNoBounceMaterial = true;

    [Header("適用対象")]
    [Tooltip("対象タグ (空なら Rigidbody を持つ全衝突物に適用)")]
    [SerializeField] private string targetTag = "";

    private PinballBallConfig _config;

    void Awake()
    {
        _config = FindFirstObjectByType<PinballBallConfig>();

        if (autoApplyNoBounceMaterial)
        {
            var col = GetComponent<Collider>();
            if (col != null && col.sharedMaterial == null)
            {
                var mat = new PhysicsMaterial("ConveyorNoBounce")
                {
                    bounciness = 0f,
                    dynamicFriction = 0.3f,
                    staticFriction = 0.3f,
                    bounceCombine = PhysicsMaterialCombine.Multiply, // 双方の bounce を掛け合わせ → 0
                    frictionCombine = PhysicsMaterialCombine.Average,
                };
                col.sharedMaterial = mat;
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        Apply(collision.rigidbody, collision.gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        Apply(other.attachedRigidbody, other.gameObject);
    }

    void Apply(Rigidbody rb, GameObject go)
    {
        if (rb == null || rb.isKinematic) return;
        if (!string.IsNullOrEmpty(targetTag) && go != null && !go.CompareTag(targetTag)) return;

        Vector3 worldDir = useLocalDirection
            ? transform.TransformDirection(direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up)
            : (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up);

        float scale = _config != null ? _config.CurrentScaleFactor : 1f;
        float beltSpeed = speed * scale;

        Vector3 v = rb.linearVelocity;
        float curAlong = Vector3.Dot(v, worldDir);
        Vector3 vPerp = v - worldDir * curAlong;

        // ベルト方向成分を「常に」beltSpeed に上書き (弾かれた瞬間の正方向余剰も含めて修正)
        // → 不足分加算ではなく上限/下限ともに beltSpeed にクランプする挙動
        vPerp *= (1f - perpDamping); // 横方向の振動を減衰
        rb.linearVelocity = vPerp + worldDir * beltSpeed;

        // 重力打ち消し用の追加加速度 (重い玉が沈みやすい時)
        if (gravityCancelBoost > 0f)
        {
            rb.AddForce(worldDir * gravityCancelBoost, ForceMode.Acceleration);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 worldDir = useLocalDirection ? transform.TransformDirection(direction.normalized) : direction.normalized;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + worldDir * Mathf.Max(0.5f, speed * 0.3f));
        Gizmos.DrawSphere(transform.position + worldDir * Mathf.Max(0.5f, speed * 0.3f), 0.05f);
    }
}
