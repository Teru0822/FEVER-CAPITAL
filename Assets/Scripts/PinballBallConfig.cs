using UnityEngine;

/// <summary>
/// PinballBallManager の実行時パラメータを Inspector で編集するための設定コンポーネント。
/// シーン内の任意の GameObject にアタッチすれば Manager が起動時に参照する。
/// 1 シーンに 1 つのみ有効。見つからない場合は全項目のデフォルト値で Manager が動作する。
/// </summary>
public class PinballBallConfig : MonoBehaviour
{
    [Header("位置・スケール追従")]
    [Tooltip("ピンボール全体のルート Transform。これを動かす/スケールすると境界・半径・速度が自動追従する。null なら従来の絶対世界座標で動作。")]
    public Transform pinballRoot;

    [Tooltip("authored 時点での pinballRoot.position。Context Menu 『Capture Current Root Pose』 で自動取得可。")]
    public Vector3 authoredRootPosition = Vector3.zero;

    [Tooltip("authored 時点での pinballRoot.lossyScale.x。")]
    public float authoredRootScale = 1f;

    /// <summary>現在の pinballRoot と authored 値の比から求まる一様スケール倍率 (root が null なら 1)。</summary>
    public float CurrentScaleFactor
    {
        get
        {
            if (pinballRoot == null) return 1f;
            return pinballRoot.lossyScale.x / Mathf.Max(0.0001f, authoredRootScale);
        }
    }

    /// <summary>
    /// authored world 座標値を現在の pinballRoot ポーズに合わせて変換する。
    /// = currentRoot.position + (authored - authoredRootPosition) * scaleFactor
    /// </summary>
    public Vector3 TransformAuthoredPoint(Vector3 authored)
    {
        if (pinballRoot == null) return authored;
        float s = CurrentScaleFactor;
        return pinballRoot.position + (authored - authoredRootPosition) * s;
    }

    [ContextMenu("Capture Current Root Pose")]
    void CaptureCurrentRootPose()
    {
        if (pinballRoot == null)
        {
            Debug.LogWarning("[PinballBallConfig] pinballRoot が未設定のため Capture できません。");
            return;
        }
        authoredRootPosition = pinballRoot.position;
        authoredRootScale = pinballRoot.lossyScale.x;
        Debug.Log($"[PinballBallConfig] Captured authoredRootPosition={authoredRootPosition}, authoredRootScale={authoredRootScale}");
    }

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

    [Header("所持金ポップ演出")]
    [Tooltip("この金額を跨ぐたびに「どぅん！」とスケールアップする閾値")]
    public int moneyPopThreshold = 100000;

    [Tooltip("ポップ時の最大スケール倍率")]
    [Range(1f, 3f)]
    public float moneyPopScale = 1.6f;

    [Tooltip("ポップ演出の長さ (秒)")]
    public float moneyPopDuration = 0.35f;

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
