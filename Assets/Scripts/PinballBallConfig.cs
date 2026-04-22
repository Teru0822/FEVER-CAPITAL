using UnityEngine;

/// <summary>
/// PinballBallManager の実行時パラメータを Inspector で編集するための設定コンポーネント。
/// シーン内の任意の GameObject にアタッチすれば Manager が起動時に参照する。
/// 1 シーンに 1 つのみ有効。見つからない場合は全項目のデフォルト値で Manager が動作する。
/// </summary>
public class PinballBallConfig : MonoBehaviour
{
    [Tooltip("NativeList 初期容量")]
    public int initialCapacity = 256;

    [Header("ボール同士の衝突応答")]
    [Tooltip("法線方向の反発係数 (0 = 完全非弾性, 0.2 前後で落ち着いた挙動)")]
    [Range(0f, 1f)]
    public float ballBallRestitution = 0.1f;

    [Tooltip("この量までの重なりは位置補正しない (ジッタ防止の接触スラック)")]
    public float ballBallPositionSlop = 0.005f;

    [Tooltip("位置補正の緩和係数 (1 = 即時解消で振動しやすい。0.2〜0.5 推奨)")]
    [Range(0.05f, 1f)]
    public float ballBallPositionCorrection = 0.2f;

    [Header("所持金 UI")]
    [Tooltip("ボール 1 個あたりの金額")]
    public int moneyPerBall = 100;

    [Tooltip("表示ラベルの接頭辞")]
    public string moneyLabelPrefix = "所持金：";

    [Tooltip("表示ラベルの接尾辞 (円、G、pt など)")]
    public string moneyLabelSuffix = "";

    [Tooltip("所持金ラベルを表示するか")]
    public bool showMoneyLabel = true;

    [Tooltip("フォントサイズ")]
    public int moneyFontSize = 72;

    [Tooltip("文字色")]
    public Color moneyColor = Color.white;

    [Tooltip("右上からの余白 (px)")]
    public Vector2 moneyPadding = new Vector2(20, 16);

    [Header("リセット")]
    [Tooltip("このキーを押すと現在のシーンを再ロードしてゲームを初期化する")]
    public KeyCode resetKey = KeyCode.R;

    [Header("デバッグ")]
    [Tooltip("現在のボール総数 (gen 0 + gen ≥ 1) を Console にログ出力する")]
    public bool logBallCount = true;

    [Tooltip("ログ出力間隔 (秒)")]
    public float logInterval = 0.5f;

    [Header("ランタイム表示 (読み取り専用)")]
    [Tooltip("現在管理中の gen ≥ 1 数")]
    public int debugManagedCount;

    [Tooltip("現在の総数 (gen 0 + gen ≥ 1)")]
    public int debugTotalCount;

    [Tooltip("累積生成数 (所持金の根拠)")]
    public int debugTotalGenerated;
}
