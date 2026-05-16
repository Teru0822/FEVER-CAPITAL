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

    [Header("重力")]
    [Tooltip("ピンボール盤面の重力ベクトル (m/s²)。既定 (0, -9.81, 9.81) は Y下 + Z前 (斜面)。X を増やすと左右にも流れる。")]
    public Vector3 gravity = new Vector3(0f, -9.81f, 9.81f);

    [Tooltip("ON にすると gravity を pinballRoot のローカル軸で解釈する (root を回転すると重力方向も回る)")]
    public bool gravityInLocalSpace = false;

    /// <summary>gravity と gravityInLocalSpace / pinballRoot から算出した実効重力ベクトル。</summary>
    public Vector3 EffectiveGravity
    {
        get
        {
            if (gravityInLocalSpace && pinballRoot != null)
                return pinballRoot.TransformDirection(gravity);
            return gravity;
        }
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

    [Header("分裂エフェクト (火花 + 効果音)")]
    [Tooltip("分裂時に短く再生する火花用 ParticleSystem プレハブ (小さめ・短命推奨)。null なら火花なし。")]
    public ParticleSystem sparkParticlePrefab;

    [Tooltip("分裂時に鳴らす効果音 (シャン)。null なら無音。")]
    public AudioClip splitSfxClip;

    [Range(0f, 1f)]
    [Tooltip("効果音の音量")]
    public float splitSfxVolume = 0.4f;

    [Range(0f, 0.5f)]
    [Tooltip("再生ピッチのランダム変動幅 (0 で固定)。±この値で揺らがせて単調感を消す。")]
    public float splitSfxPitchVariance = 0.15f;

    [Min(1)]
    [Tooltip("AudioSource プールサイズ (同時発音数の上限)")]
    public int sfxPoolSize = 16;

    [Min(0)]
    [Tooltip("1 フレームあたりの最大発音数 (0 で無制限)。多すぎる時はランダムに間引かれる。")]
    public int maxSfxPerFrame = 6;

    [Min(0)]
    [Tooltip("1 フレームあたりの最大火花生成数 (0 で無制限)")]
    public int maxSparksPerFrame = 12;

    [Header("ボール衝突 SFX プール上限")]
    [Min(0)]
    [Tooltip("1 フレームあたりの最大衝突 SFX 数 (0 で無制限)。BallSurfaceAudio の impact one-shot にも適用される")]
    public int maxImpactSfxPerFrame = 4;

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
