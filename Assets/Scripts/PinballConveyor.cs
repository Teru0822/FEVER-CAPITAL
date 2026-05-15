using UnityEngine;

/// <summary>
/// ピンボール盤面用ベルトコンベア。
/// 「スイーッ」と滑らかに玉を運ぶため、接触中のボールについては
///   - PinballBallController.IsOnConveyor を立てて重力適用をスキップさせる
///   - 速度を毎フレーム ベルト方向 × beltSpeed に上書き
/// する。重力もバウンスも介入しないので等速で吸い付くように移動する。
///
/// 推奨セットアップ:
///   - Collider を Box などにしてベルト面の体積をカバーする
///   - **Is Trigger を ON** にすると最も滑らか (バウンス完全消去)
///   - direction = (0, 1, 0) で上方向にリフト
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

    [Header("重力切替 (ベルト退出後)")]
    [Tooltip("ベルトに触れたボールの PinballBallController.gravity をこの値に上書きする (退出後にも残る)")]
    [SerializeField] private bool changeGravityOnTouch = true;

    [Tooltip("接触時に設定する重力ベクトル (m/s²)。既定 (0, -9.81, 0) で純粋な垂直落下に切り替わる。")]
    [SerializeField] private Vector3 onTouchGravity = new Vector3(0f, -9.81f, 0f);

    [Header("適用対象")]
    [Tooltip("対象タグ (空なら PinballBallController を持つ全衝突物に適用)")]
    [SerializeField] private string targetTag = "";

    [Header("オプション")]
    [Tooltip("Awake 時に Collider へ bounciness=0 の PhysicsMaterial を自動付与 (非 Trigger 時の保険)")]
    [SerializeField] private bool autoApplyNoBounceMaterial = true;

    private PinballBallConfig _config;

    void Awake()
    {
        _config = FindFirstObjectByType<PinballBallConfig>();

        if (autoApplyNoBounceMaterial)
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger && col.sharedMaterial == null)
            {
                col.sharedMaterial = new PhysicsMaterial("ConveyorNoBounce")
                {
                    bounciness = 0f,
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounceCombine = PhysicsMaterialCombine.Multiply,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                };
            }
        }
    }

    // ---------- Trigger / Collision 共通ハンドリング ----------
    void OnTriggerEnter(Collider other) { OnEnterBelt(other.attachedRigidbody, other.gameObject); }
    void OnTriggerExit(Collider other) { OnExitBelt(other.attachedRigidbody, other.gameObject); }
    void OnTriggerStay(Collider other) { OnStayBelt(other.attachedRigidbody, other.gameObject); }

    void OnCollisionEnter(Collision c) { OnEnterBelt(c.rigidbody, c.gameObject); }
    void OnCollisionExit(Collision c) { OnExitBelt(c.rigidbody, c.gameObject); }
    void OnCollisionStay(Collision c) { OnStayBelt(c.rigidbody, c.gameObject); }

    void OnEnterBelt(Rigidbody rb, GameObject go)
    {
        var ctrl = ResolveTargetController(rb, go);
        if (ctrl == null) return;
        ctrl.onConveyorCount++;
        if (changeGravityOnTouch) ctrl.gravity = onTouchGravity;
    }

    void OnExitBelt(Rigidbody rb, GameObject go)
    {
        var ctrl = ResolveTargetController(rb, go);
        if (ctrl == null) return;
        if (ctrl.onConveyorCount > 0) ctrl.onConveyorCount--;
    }

    void OnStayBelt(Rigidbody rb, GameObject go)
    {
        var ctrl = ResolveTargetController(rb, go);
        if (ctrl == null || rb == null || rb.isKinematic) return;

        Vector3 worldDir = useLocalDirection
            ? transform.TransformDirection(direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up)
            : (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up);

        float scale = _config != null ? _config.CurrentScaleFactor : 1f;
        float beltSpeed = speed * scale;

        // 重力スキップ中なので、速度を完全置換しても他要素と衝突しない
        rb.linearVelocity = worldDir * beltSpeed;
    }

    PinballBallController ResolveTargetController(Rigidbody rb, GameObject go)
    {
        if (rb == null) return null;
        if (!string.IsNullOrEmpty(targetTag) && go != null && !go.CompareTag(targetTag)) return null;
        return rb.GetComponent<PinballBallController>();
    }

    void OnDrawGizmosSelected()
    {
        Vector3 worldDir = useLocalDirection ? transform.TransformDirection(direction.normalized) : direction.normalized;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + worldDir * Mathf.Max(0.5f, speed * 0.3f));
        Gizmos.DrawSphere(transform.position + worldDir * Mathf.Max(0.5f, speed * 0.3f), 0.05f);
    }
}
