using UnityEngine;

/// <summary>
/// Trigger Collider に侵入したボールに VFX を「纏わせる」ゾーン。
/// VFX プレハブを Instantiate し、ボールの子としてアタッチする (位置・回転がボールに追従)。
///
/// セットアップ:
///   1. 空 GameObject に BoxCollider 等を付け、Is Trigger を ON (forceTriggerOnAwake が
///      ON ならスクリプト側でも強制 ON にする)
///   2. 本コンポーネントをアタッチ
///   3. vfxPrefab に Particle System 等の VFX プレハブをドラッグ
///   4. ボール (PinballBallController を持つ Rigidbody) が侵入すると VFX が球の子になる
/// </summary>
[RequireComponent(typeof(Collider))]
public class PinballBallVFXZone : MonoBehaviour
{
    [Header("VFX")]
    [Tooltip("ボールに纏わせる VFX プレハブ (Particle System 等)")]
    [SerializeField] private GameObject vfxPrefab;

    [Tooltip("ON: VFX をボールの子にして位置追従させる / OFF: 侵入位置に置きっぱなし")]
    [SerializeField] private bool attachToBall = true;

    [Tooltip("VFX の自動破棄時間 (秒)。0 以下で無効 (VFX 側の Stop Action 等に委ねる)")]
    [SerializeField] private float vfxLifetime = 3f;

    [Tooltip("ON: 既に同名 VFX が球についていれば二重に付けない")]
    [SerializeField] private bool preventDuplicate = true;

    [Header("適用対象")]
    [Tooltip("対象タグ (空なら PinballBallController を持つ全衝突物)")]
    [SerializeField] private string targetTag = "";

    [Header("オプション")]
    [Tooltip("Awake 時に Collider.isTrigger を強制 ON にする")]
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
        if (vfxPrefab == null) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        if (!string.IsNullOrEmpty(targetTag) && !rb.gameObject.CompareTag(targetTag)) return;
        var ctrl = rb.GetComponent<PinballBallController>();
        if (ctrl == null) return;

        if (preventDuplicate)
        {
            string cloneName = vfxPrefab.name + "(Clone)";
            for (int i = 0; i < rb.transform.childCount; i++)
            {
                if (rb.transform.GetChild(i).name == cloneName) return;
            }
        }

        GameObject vfx = Instantiate(vfxPrefab, rb.transform.position, vfxPrefab.transform.rotation);
        if (attachToBall) vfx.transform.SetParent(rb.transform, true);
        if (vfxLifetime > 0f) Destroy(vfx, vfxLifetime);
    }
}
