using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Y キー押下で PinBallRoot を以下のタイムラインで上下に移動させる:
///   フレーム 0  →  moveDownEndFrame   : Y を 0 から -yOffset へリニア補間 (下げる)
///   moveDownEndFrame → moveUpStartFrame : -yOffset で停止 (待機)
///   moveUpStartFrame → moveUpEndFrame  : Y を -yOffset から 0 へリニア補間 (戻す)
/// frameRate でフレーム→秒に換算するため、Blender の 24/30 FPS にも合わせられる。
/// </summary>
public class PinBallRootCutsceneMover : MonoBehaviour
{
    [Header("対象")]
    [Tooltip("移動させる PinBallRoot Transform。未設定なら起動時に \"PinBallRoot\" を名前検索する。")]
    [SerializeField] private Transform pinBallRoot;

    [Header("トリガー")]
    [SerializeField] private KeyCode triggerKey = KeyCode.Y;

    [Header("タイミング (フレーム単位)")]
    [Tooltip("フレーム→秒 換算の FPS (Blender 既定 = 24, Unity 一般 = 30)")]
    [SerializeField, Min(1f)] private float frameRate = 24f;

    [Tooltip("下降フェーズの終了フレーム")]
    [SerializeField, Min(1)] private int moveDownEndFrame = 24;

    [Tooltip("上昇フェーズの開始フレーム")]
    [SerializeField, Min(1)] private int moveUpStartFrame = 103;

    [Tooltip("上昇フェーズの終了フレーム (= 全体のクリップ長)")]
    [SerializeField, Min(1)] private int moveUpEndFrame = 128;

    [Header("移動量")]
    [Tooltip("Y 軸負方向にこの距離だけ下げ、そのあと同じだけ正方向に戻す")]
    [SerializeField] private float yOffset = 0.94f;

    [Header("補間カーブ")]
    [Tooltip("0→1 の進行度を実際の補間値に変換するカーブ。既定は EaseInOut で Blender の Bezier 既定とほぼ一致する。リニアにしたい場合は AnimationCurve.Linear(0,0,1,1) を割り当てる。")]
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool _isPlaying;
    private Vector3 _originalPosition;

    public bool IsPlaying => _isPlaying;

    void Awake()
    {
        if (pinBallRoot == null)
        {
            GameObject go = GameObject.Find("PinBallRoot");
            if (go != null) pinBallRoot = go.transform;
        }
    }

    void Update()
    {
        if (_isPlaying) return;
        if (IsTriggerPressed()) PlayMove();
    }

    /// <summary>外部からも呼べる開始メソッド。再生中の再トリガーは無視。</summary>
    public void PlayMove()
    {
        if (_isPlaying) return;
        if (pinBallRoot == null)
        {
            Debug.LogWarning("[PinBallRootCutsceneMover] pinBallRoot が未設定 (PinBallRoot 名のオブジェクトも見つからず)。");
            return;
        }
        StartCoroutine(MoveSequence());
    }

    IEnumerator MoveSequence()
    {
        _isPlaying = true;
        _originalPosition = pinBallRoot.position;
        Vector3 downPosition = _originalPosition + Vector3.down * yOffset;

        float tDownEnd = moveDownEndFrame / frameRate;
        float tUpStart = moveUpStartFrame / frameRate;
        float tUpEnd = moveUpEndFrame / frameRate;

        // フェーズ 1: 0 → tDownEnd で下降 (easing カーブを通す)
        float elapsed = 0f;
        while (elapsed < tDownEnd)
        {
            float p = Mathf.Clamp01(elapsed / tDownEnd);
            float eased = easing.Evaluate(p);
            pinBallRoot.position = Vector3.LerpUnclamped(_originalPosition, downPosition, eased);
            elapsed += Time.deltaTime;
            yield return null;
        }
        pinBallRoot.position = downPosition;

        // フェーズ 2: 待機
        float holdTime = tUpStart - tDownEnd;
        if (holdTime > 0f) yield return new WaitForSeconds(holdTime);

        // フェーズ 3: tUpStart → tUpEnd で元位置へ (easing カーブを通す)
        float upDuration = tUpEnd - tUpStart;
        elapsed = 0f;
        while (elapsed < upDuration)
        {
            float p = Mathf.Clamp01(elapsed / upDuration);
            float eased = easing.Evaluate(p);
            pinBallRoot.position = Vector3.LerpUnclamped(downPosition, _originalPosition, eased);
            elapsed += Time.deltaTime;
            yield return null;
        }
        pinBallRoot.position = _originalPosition;

        _isPlaying = false;
    }

    bool IsTriggerPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            Key k = KeyCodeToKey(triggerKey);
            if (k != Key.None && Keyboard.current[k].wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(triggerKey)) return true;
#endif
        return false;
    }

#if ENABLE_INPUT_SYSTEM
    static Key KeyCodeToKey(KeyCode code)
    {
        if (code >= KeyCode.A && code <= KeyCode.Z) return Key.A + (int)(code - KeyCode.A);
        if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9) return Key.Digit0 + (int)(code - KeyCode.Alpha0);
        switch (code)
        {
            case KeyCode.Space: return Key.Space;
            case KeyCode.Return: return Key.Enter;
            case KeyCode.Escape: return Key.Escape;
            case KeyCode.Tab: return Key.Tab;
            default: return Key.None;
        }
    }
#endif
}
