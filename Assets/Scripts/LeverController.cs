using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// レバーメッシュにアタッチ。
/// カメラのスクリーン空間を基準に、マウスの動き方向へ単一軸で傾ける。
/// 複数軸の組み合わせによる干渉をなくした実装。
/// </summary>
public class LeverController : MonoBehaviour
{
    [Header("連携")]
    [Tooltip("UFOArmController が付いているオブジェクト")]
    public UFOArmController armController;

    [Header("レバー設定")]
    [Tooltip("レバーの最大傾き角度（度）")]
    public float leverMaxAngle = 30f;
    [Tooltip("マウス移動量に対する感度")]
    public float sensitivity = 120f;
    [Tooltip("離したときに中央へ戻る速さ")]
    public float returnSpeed = 8f;

    [Header("方向反転（見た目が逆の場合にチェック）")]
    public bool invertHorizontal = false;
    public bool invertVertical   = false;

    // 内部状態
    private bool       _isDragging      = false;
    private float      _angleH          = 0f;   // マウスX方向の累積角度
    private float      _angleV          = 0f;   // マウスY方向の累積角度
    private Quaternion _initialWorldRot;         // レバーの初期ワールド回転
    private Vector3    _leverInitialUp;          // レバー棒の初期ワールド上方向
    private Collider   _collider;

    void Start()
    {
        _initialWorldRot = transform.rotation;
        _leverInitialUp  = transform.up;          // レバーが +Y 以外を向いていても対応
        _collider        = GetComponent<Collider>();
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // クリック検出
        if (mouse.leftButton.wasPressedThisFrame && IsMouseOverThis(mouse.position.ReadValue()))
            _isDragging = true;
        if (mouse.leftButton.wasReleasedThisFrame)
            _isDragging = false;

        if (_isDragging)
        {
            Vector2 delta = mouse.delta.ReadValue();
            float dirH = invertHorizontal ? -1f : 1f;
            float dirV = invertVertical   ? -1f : 1f;

            _angleH += delta.x * dirH * sensitivity * Time.deltaTime;
            _angleV += delta.y * dirV * sensitivity * Time.deltaTime;
            _angleH  = Mathf.Clamp(_angleH, -leverMaxAngle, leverMaxAngle);
            _angleV  = Mathf.Clamp(_angleV, -leverMaxAngle, leverMaxAngle);
        }
        else
        {
            _angleH = Mathf.Lerp(_angleH, 0f, Time.deltaTime * returnSpeed);
            _angleV = Mathf.Lerp(_angleV, 0f, Time.deltaTime * returnSpeed);
        }

        ApplyLeverRotation();
        UpdateArmInput();
    }

    /// <summary>
    /// レバーの回転を適用。
    /// マウスの (dx, dy) を画面空間の傾き方向として解釈し、
    /// レバーをその方向へ単一軸で傾ける。複数軸合成による歪みがない。
    /// </summary>
    void ApplyLeverRotation()
    {
        if (Camera.main == null) return;
        var cam = Camera.main.transform;

        // カメラのスクリーン空間の右/上方向（ワールド座標）
        Vector3 screenRight = cam.right;
        Vector3 screenUp    = cam.up;

        // マウスの (H, V) から画面上の「傾き方向」ベクトルを生成
        Vector3 tiltDir   = screenRight * _angleH + screenUp * _angleV;
        float   tiltAngle = tiltDir.magnitude;

        if (tiltAngle < 0.001f)
        {
            // 完全に中立: 初期回転に戻す
            ApplyWorldRotation(_initialWorldRot);
            return;
        }

        tiltDir   = tiltDir.normalized;
        tiltAngle = Mathf.Min(tiltAngle, leverMaxAngle);

        // 回転軸 = レバーの軸 × 傾き方向（どちら向きに倒すかを決定）
        Vector3    rotAxis       = Vector3.Cross(_leverInitialUp, tiltDir).normalized;
        Quaternion additionalRot = Quaternion.AngleAxis(tiltAngle, rotAxis);
        Quaternion targetWorld   = additionalRot * _initialWorldRot;

        ApplyWorldRotation(targetWorld);
    }

    void ApplyWorldRotation(Quaternion worldRot)
    {
        if (transform.parent != null)
            transform.localRotation = Quaternion.Inverse(transform.parent.rotation) * worldRot;
        else
            transform.rotation = worldRot;
    }

    /// <summary>
    /// アームの XZ 移動用の入力を UFOArmController に送る。
    /// カメラの向きを考慮した方向に変換する。
    /// </summary>
    void UpdateArmInput()
    {
        if (Camera.main == null || armController == null) return;
        var cam = Camera.main.transform;

        // カメラの右/前方向を XZ 平面（地面）に投影 → アームの移動方向
        Vector3 camXZRight   = Vector3.ProjectOnPlane(cam.right,   Vector3.up).normalized;
        Vector3 camXZForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;

        float normH = _angleH / leverMaxAngle;
        float normV = _angleV / leverMaxAngle;

        Vector3 moveDir = camXZRight * normH + camXZForward * normV;
        armController.SetLeverInput(moveDir.x, moveDir.z);
    }

    bool IsMouseOverThis(Vector2 screenPos)
    {
        if (_collider == null || Camera.main == null) return false;
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        return _collider.Raycast(ray, out _, 1000f);
    }
}
