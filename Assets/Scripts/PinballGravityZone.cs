using UnityEngine;

/// <summary>
/// Trigger Collider に侵入した PinballBallController.gravity を targetGravity に上書きするシンプルなゾーン。
///
/// セットアップ:
///   1. 空 GameObject に Collider (Box / Sphere / Mesh など) を付け、Is Trigger を ON
///   2. 本コンポーネントをアタッチ
///   3. targetGravity を変えたい値に設定 (既定 (0, -9.81, 0) で純垂直落下)
///   4. ボールがトリガー領域に入ると、その玉の gravity が永続的に書き換わる
///
/// PinballConveyor の gravity 上書き機能と同じ仕組みだが、こちらは「重力切替だけ」したい
/// 場面 (落下エリア、垂直シャフト等) 用のスタンドアロン版。
/// </summary>
[RequireComponent(typeof(Collider))]
public class PinballGravityZone : MonoBehaviour
{
    [Tooltip("ゾーンに入ったボールに設定する重力ベクトル (m/s²)。既定 (0, -9.81, 0) で純粋な垂直落下。")]
    [SerializeField] private Vector3 targetGravity = new Vector3(0f, -9.81f, 0f);

    [Tooltip("対象タグ (空なら PinballBallController を持つ全オブジェクトに適用)")]
    [SerializeField] private string targetTag = "";

    [Tooltip("OnTriggerEnter (1 回) ではなく OnTriggerStay (滞在中毎フレーム) で適用するか")]
    [SerializeField] private bool applyEveryFrame = false;

    [Tooltip("Awake で Collider.isTrigger を強制 ON にする")]
    [SerializeField] private bool forceTriggerOnAwake = true;

    void Awake()
    {
        if (forceTriggerOnAwake)
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger) col.isTrigger = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!applyEveryFrame) Apply(other);
    }

    void OnTriggerStay(Collider other)
    {
        if (applyEveryFrame) Apply(other);
    }

    void Apply(Collider other)
    {
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag)) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        var ctrl = rb.GetComponent<PinballBallController>();
        if (ctrl == null) return;
        ctrl.gravity = targetGravity;
    }

    void OnDrawGizmosSelected()
    {
        // ゾーン位置に重力ベクトルを矢印描画
        Gizmos.color = Color.magenta;
        Vector3 origin = transform.position;
        Vector3 dir = targetGravity.normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.down;
        float len = Mathf.Min(0.5f, targetGravity.magnitude * 0.05f);
        Gizmos.DrawLine(origin, origin + dir * len);
        Gizmos.DrawSphere(origin + dir * len, 0.04f);
    }
}
