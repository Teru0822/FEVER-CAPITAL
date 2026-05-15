using UnityEngine;

/// <summary>
/// ピンボール盤面用ベルトコンベア。
/// このオブジェクトの Collider に接触した Rigidbody を、指定方向 (既定: ローカル +Y) へ
/// 一定速度で押し出す。pinballRoot のスケール倍率に追従する。
///
/// 使い方:
///   1. 空 GameObject に BoxCollider 等を付け、ベルト面の形状にスケール
///   2. 本コンポーネントをアタッチ
///   3. direction (ローカル軸) を押し出したい向きに、speed を m/s で設定
///   4. Collider は Is Trigger どちらでも OK (両方ハンドリングする)
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

    [Header("適用対象")]
    [Tooltip("対象タグ (空なら Rigidbody を持つ全衝突物に適用)")]
    [SerializeField] private string targetTag = "";

    [Tooltip("既存速度を完全置換する (true) / ベルト方向の不足分のみ加える (false 推奨)")]
    [SerializeField] private bool overrideVelocity = false;

    private PinballBallConfig _config;

    void Awake()
    {
        _config = FindFirstObjectByType<PinballBallConfig>();
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
        if (rb == null) return;
        if (rb.isKinematic) return;
        if (!string.IsNullOrEmpty(targetTag) && go != null && !go.CompareTag(targetTag)) return;

        Vector3 worldDir = useLocalDirection
            ? transform.TransformDirection(direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up)
            : (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up);

        float scale = _config != null ? _config.CurrentScaleFactor : 1f;
        float beltSpeed = speed * scale;

        if (overrideVelocity)
        {
            rb.linearVelocity = worldDir * beltSpeed;
            return;
        }

        // ベルト方向成分を beltSpeed に揃える (それ以外の成分は維持)
        Vector3 v = rb.linearVelocity;
        float curComp = Vector3.Dot(v, worldDir);
        if (curComp < beltSpeed)
        {
            v += worldDir * (beltSpeed - curComp);
            rb.linearVelocity = v;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Inspector でベルト方向を可視化
        Vector3 worldDir = useLocalDirection ? transform.TransformDirection(direction.normalized) : direction.normalized;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + worldDir * Mathf.Max(0.5f, speed * 0.3f));
        Gizmos.DrawSphere(transform.position + worldDir * Mathf.Max(0.5f, speed * 0.3f), 0.05f);
    }
}
