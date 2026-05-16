using UnityEngine;

/// <summary>
/// Trigger Collider に侵入したボールを「消滅」させ、グローバル静的カウンタ
/// <see cref="PartsBallCount"/> をその分インクリメントするゾーン。
///
/// セットアップ:
///   1. 空 GameObject に BoxCollider 等を付け、Is Trigger を ON
///   2. 本コンポーネントをアタッチ
///   3. (任意) onSinkVFXPrefab に消滅エフェクトをアサイン
///   4. ボール (PinballBallController) が侵入すると Destroy + Count++ される
///
/// 他スクリプトから読みたい場合は <c>PinballBallSinkZone.PartsBallCount</c> を参照。
/// </summary>
[RequireComponent(typeof(Collider))]
public class PinballBallSinkZone : MonoBehaviour
{
    /// <summary>このゾーンに吸収されて消えたボールの累計数 (全インスタンス合計、シーン跨ぎで保持)。</summary>
    public static int PartsBallCount { get; private set; }

    [Header("消滅時 VFX (任意)")]
    [Tooltip("ボール消滅位置で再生する VFX プレハブ。null なら何もしない")]
    [SerializeField] private GameObject onSinkVFXPrefab;

    [Tooltip("消滅 VFX の自動破棄時間 (秒)。0 以下で無効")]
    [SerializeField] private float onSinkVFXLifetime = 2f;

    // 消滅 SFX (コロンコロン等) は PinballBottleSFXZone に分離。
    // 必要な場合は同じ GameObject に PinballBottleSFXZone を併設すること。

    [Header("適用対象")]
    [Tooltip("対象タグ (空なら PinballBallController を持つ全衝突物)")]
    [SerializeField] private string targetTag = "";

    [Header("オプション")]
    [Tooltip("Awake 時に Collider.isTrigger を強制 ON にする")]
    [SerializeField] private bool forceTriggerOnAwake = true;

    [Header("ランタイム表示 (読み取り専用)")]
    [Tooltip("現在の PartsBallCount (Inspector 確認用、静的カウンタの読み取り)")]
    [SerializeField] private int debugPartsBallCount;

    void Awake()
    {
        if (forceTriggerOnAwake)
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger) col.isTrigger = true;
        }
    }

    void LateUpdate()
    {
        debugPartsBallCount = PartsBallCount;
    }

    void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        if (!string.IsNullOrEmpty(targetTag) && !rb.gameObject.CompareTag(targetTag)) return;
        var ctrl = rb.GetComponent<PinballBallController>();
        if (ctrl == null) return;

        PartsBallCount++;

        if (onSinkVFXPrefab != null)
        {
            var vfx = Instantiate(onSinkVFXPrefab, rb.transform.position, onSinkVFXPrefab.transform.rotation);
            if (onSinkVFXLifetime > 0f) Destroy(vfx, onSinkVFXLifetime);
        }

        Destroy(rb.gameObject);
    }

    /// <summary>PartsBallCount を 0 に戻す (R リセット時等で使用)。</summary>
    public static void ResetCount() => PartsBallCount = 0;

    [ContextMenu("Reset PartsBallCount")]
    private void ContextResetCount() => ResetCount();
}
