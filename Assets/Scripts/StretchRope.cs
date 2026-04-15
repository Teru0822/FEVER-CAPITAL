using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ロープ（オブジェクト6）の伸縮を担う。
/// Spaceキーによる手動操作と、UFOArmController からの外部制御の両方に対応。
/// </summary>
public class StretchRope : MonoBehaviour
{
    public enum Axis { X, Y, Z, None }

    [Header("Animation Settings")]
    [Tooltip("伸縮スピード（手動操作時）")]
    public float stretchSpeed = 2f;

    [Tooltip("伸縮の強さ（例: 35 に設定するとスケールが35伸びます）")]
    public float stretchIntensity = 35f;

    [Header("Axis Settings")]
    [Tooltip("どの軸方向にスケール(長さ)を伸ばすか")]
    public Axis scaleAxis = Axis.Z;

    [Tooltip("ロープ本体をどの方向に動かすか（中心補正用）")]
    public Axis moveAxis = Axis.Y;
    public bool moveNegative = true;

    [Tooltip("ロープ本体の位置補正比率（元の動作は 0.01）")]
    public float moveRatio = 0.01f;

    [Header("附属オブジェクト（finger/爪）")]
    [Tooltip("ロープ先端に連動させるオブジェクトをInspectorからドラッグ＆ドロップで設定してください")]
    public Transform attachedObject;

    [Tooltip("finger の移動比率（元の動作は 0.0475）")]
    public float fingerRatio = 0.0475f;

    [Header("Spaceキー操作")]
    [Tooltip("Spaceキーによる手動伸縮を許可するか")]
    public bool allowSpaceKey = true;

    // ─────────────────────────────────────
    // 内部状態
    private Vector3 _originalScale;
    private Vector3 _originalPosition;
    private float   _stretchTime;       // 0(縮み) 〜 1(最大)

    private Vector3 _originalAttachedWorldPos;

    // 外部制御
    private bool  _externalControl  = false;  // true = UFOArmController が制御中
    private float _externalDir      = 0f;     // +1:伸びる  -1:縮む
    private float _externalSpeedMul = 1f;

    // ─────────────────────────────────────
    void Start()
    {
        _originalScale    = transform.localScale;
        _originalPosition = transform.localPosition;

        if (attachedObject != null)
            _originalAttachedWorldPos = attachedObject.position;
    }

    // ─────────────────────────────────────
    // 外部制御 API（UFOArmController から呼ぶ）

    /// <summary>自動下降を開始（ stretchTime を 1 に向けて動かす）</summary>
    public void StartExternalDescent(float speedMultiplier = 1f)
    {
        _externalControl  = true;
        _externalDir      = 1f;
        _externalSpeedMul = speedMultiplier;
    }

    /// <summary>自動上昇を開始（ stretchTime を 0 に向けて動かす）</summary>
    public void StartExternalAscent(float speedMultiplier = 1f)
    {
        _externalControl  = true;
        _externalDir      = -1f;
        _externalSpeedMul = speedMultiplier;
    }

    /// <summary>外部制御を解除（Space キー操作に戻る）</summary>
    public void StopExternalControl()
    {
        _externalControl = false;
        _externalDir     = 0f;
    }

    /// <summary>最大まで伸びているか</summary>
    public bool IsAtMax() => _stretchTime >= 1f;

    /// <summary>完全に縮んでいるか</summary>
    public bool IsAtMin() => _stretchTime <= 0f;

    // ─────────────────────────────────────
    void LateUpdate()
    {
        // ── 伸縮量の更新 ──
        if (_externalControl)
        {
            // 外部制御（自動昇降）
            _stretchTime += _externalDir * Time.deltaTime * stretchSpeed * _externalSpeedMul;
            _stretchTime  = Mathf.Clamp01(_stretchTime);

            // 目標に到達したら外部制御を自動解除
            if ((_externalDir > 0f && _stretchTime >= 1f) ||
                (_externalDir < 0f && _stretchTime <= 0f))
            {
                StopExternalControl();
            }
        }
        else if (allowSpaceKey)
        {
            // 手動制御（Space キー）
            bool spaceDown = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            _stretchTime += (spaceDown ? 1f : -1f) * Time.deltaTime * stretchSpeed;
            _stretchTime  = Mathf.Clamp01(_stretchTime);
        }

        float t        = Mathf.SmoothStep(0f, 1f, _stretchTime);
        float scaleAdd = stretchIntensity * t;

        // ── ロープ本体のスケール ──
        Vector3 newScale = _originalScale;
        switch (scaleAxis)
        {
            case Axis.X: newScale.x += scaleAdd; break;
            case Axis.Y: newScale.y += scaleAdd; break;
            case Axis.Z: newScale.z += scaleAdd; break;
        }
        transform.localScale = newScale;

        // ── ロープ本体の位置（中心補正） ──
        if (moveAxis != Axis.None)
        {
            float   dir    = moveNegative ? -1f : 1f;
            float   move   = scaleAdd * moveRatio;
            Vector3 newPos = _originalPosition;
            switch (moveAxis)
            {
                case Axis.X: newPos.x += move * dir; break;
                case Axis.Y: newPos.y += move * dir; break;
                case Axis.Z: newPos.z += move * dir; break;
            }
            transform.localPosition = newPos;
        }

        // ── finger の追従（ワールド座標ベース） ──
        if (attachedObject != null)
        {
            float   dir       = moveNegative ? -1f : 1f;
            Vector3 targetPos = _originalAttachedWorldPos;
            switch (moveAxis)
            {
                case Axis.X: targetPos.x += scaleAdd * fingerRatio * dir; break;
                case Axis.Y: targetPos.y += scaleAdd * fingerRatio * dir; break;
                case Axis.Z: targetPos.z += scaleAdd * fingerRatio * dir; break;
            }
            attachedObject.position = targetPos;
        }
    }
}
