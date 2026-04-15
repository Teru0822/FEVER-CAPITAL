using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ボタンメッシュにアタッチ。
/// New Input System 対応：クリックで視覚的に押し込み、UFOArmController の下降サイクルを起動する。
/// </summary>
public class ButtonController : MonoBehaviour
{
    [Header("連携")]
    [Tooltip("UFOArmController が付いているオブジェクト")]
    public UFOArmController armController;

    [Header("ボタン演出")]
    [Tooltip("ボタンが押し込まれるローカルY方向の量")]
    public float pressDepth = 0.05f;

    [Tooltip("押し込み／戻りのスピード")]
    public float pressSpeed = 20f;

    private Vector3  _originalLocalPos;
    private bool     _isPressed = false;
    private Collider _collider;

    void Start()
    {
        _originalLocalPos = transform.localPosition;
        _collider         = GetComponent<Collider>();
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // クリック開始：マウスがこのオブジェクト上にあるとき
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (IsMouseOverThis(mouse.position.ReadValue()))
            {
                _isPressed = true;
                armController?.StartDescentCycle();
            }
        }

        // クリック終了
        if (mouse.leftButton.wasReleasedThisFrame)
            _isPressed = false;

        // 押し込み演出
        float   targetY = _isPressed ? _originalLocalPos.y - pressDepth : _originalLocalPos.y;
        Vector3 pos     = transform.localPosition;
        pos.y                 = Mathf.Lerp(pos.y, targetY, Time.deltaTime * pressSpeed);
        transform.localPosition = pos;
    }

    /// <summary>マウス座標がこのオブジェクトのCollider上にあるか判定</summary>
    bool IsMouseOverThis(Vector2 screenPos)
    {
        if (_collider == null || Camera.main == null) return false;
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        return _collider.Raycast(ray, out _, 1000f);
    }
}
