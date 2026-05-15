using UnityEngine;

/// <summary>
/// ピンボールのピン (Enforce / Split) を表すコンポーネント。
/// 子オブジェクト pin_top の Renderer に発光色を設定し、玉が触れたら一度だけ効果を発動する。
/// 消費後は pin_top を黒色 (発光無し) にして、以降の衝突では効果なしになる。
///
/// 設置方法:
///   1. ピンの GameObject に本コンポーネントをアタッチ
///   2. type を Enforce か Split に設定
///   3. 子に "pin_top" GameObject (Renderer 持ち) があれば自動検出。
///      別名にする場合は pinTopRenderer を直接アサインするか pinTopChildName を変更
///   4. ピンの Collider のタグを「Enforce Pin」「Split Pin」と合わせるのが分かりやすい
///      (PinballBallController は本コンポーネントを直接見るのでタグ自体は必須ではない)
/// </summary>
public class PinballPin : MonoBehaviour
{
    public enum PinType { Split, Enforce }

    [Header("ピン種別")]
    [Tooltip("Split: 玉が分裂する従来のピン\nEnforce: 玉の発色レベルを 1 段階上げるピン")]
    [SerializeField] private PinType type = PinType.Split;

    [Header("発光対象")]
    [Tooltip("発光させる Renderer。未設定なら pinTopChildName で子を検索する")]
    [SerializeField] private Renderer pinTopRenderer;

    [Tooltip("pinTopRenderer 未設定時に検索する子オブジェクト名")]
    [SerializeField] private string pinTopChildName = "pin_top";

    [Header("発光色")]
    [Tooltip("Enforce Pin の初期発光色")]
    [SerializeField] private Color enforceEmissionColor = Color.red;

    [Tooltip("Split Pin の初期発光色")]
    [SerializeField] private Color splitEmissionColor = Color.blue;

    [Tooltip("消費後 (玉が触れた後) の発光色")]
    [SerializeField] private Color consumedEmissionColor = Color.black;

    [Tooltip("発光強度倍率")]
    [Min(0f)]
    [SerializeField] private float emissionIntensity = 2f;

    private bool _consumed = false;
    private Material _pinTopMaterialInstance;

    public bool IsConsumed => _consumed;
    public PinType Type => type;

    void Awake()
    {
        if (pinTopRenderer == null && !string.IsNullOrEmpty(pinTopChildName))
        {
            var t = FindChildRecursive(transform, pinTopChildName);
            if (t != null) pinTopRenderer = t.GetComponent<Renderer>();
        }
        if (pinTopRenderer == null)
        {
            pinTopRenderer = GetComponentInChildren<Renderer>();
        }

        if (pinTopRenderer != null)
        {
            // material を読み取ると自動でインスタンス化される (sharedMaterial を汚染しない)
            _pinTopMaterialInstance = pinTopRenderer.material;
        }

        ApplyEmission(type == PinType.Enforce ? enforceEmissionColor : splitEmissionColor);
    }

    /// <summary>玉が触れた時に呼ぶ。一度だけ効果発動 → pin_top を黒に → 以降無効。</summary>
    public bool TryConsume()
    {
        if (_consumed) return false;
        _consumed = true;
        ApplyEmission(consumedEmissionColor);
        return true;
    }

    void ApplyEmission(Color c)
    {
        if (_pinTopMaterialInstance == null) return;
        _pinTopMaterialInstance.EnableKeyword("_EMISSION");
        _pinTopMaterialInstance.SetColor("_EmissionColor", c * emissionIntensity);
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
            var found = FindChildRecursive(c, name);
            if (found != null) return found;
        }
        return null;
    }
}
