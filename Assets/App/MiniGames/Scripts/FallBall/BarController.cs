using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 鉄球落としにおける2本の棒（レール）の操作を担うクラス。
    /// - マウス移動: 棒全体を左右に移動
    /// - スペースキー: 押下中は棒の間隔が広がり、離すと狭まる
    /// </summary>
    public class BarController : MonoBehaviour
    {
        [Header("Bars")]
        [Tooltip("左側の棒のTransform")]
        [SerializeField] private Transform leftBar;
        [Tooltip("右側の棒のTransform")]
        [SerializeField] private Transform rightBar;

        [Header("Movement Settings (Mouse)")]
        [Tooltip("マウス移動の感度")]
        [SerializeField] private float mouseSensitivity = 0.05f;
        [Tooltip("移動可能なX座標の最小値")]
        [SerializeField] private float minX = -5f;
        [Tooltip("移動可能なX座標の最大値")]
        [SerializeField] private float maxX = 5f;

        [Header("Opening Settings (Space Key)")]
        [Tooltip("棒の最小間隔（スペースキーを離している時）")]
        [SerializeField] private float minGap = 1.0f;
        [Tooltip("棒の最大間隔（スペースキーを押し切った時）")]
        [SerializeField] private float maxGap = 3.0f;
        [Tooltip("間隔が開く速度")]
        [SerializeField] private float openingSpeed = 5.0f;
        [Tooltip("間隔が閉じる速度")]
        [SerializeField] private float closingSpeed = 5.0f;

        private float currentX;
        private float currentGap;
        private bool isGameActive = true; 

        void Start()
        {
            currentX = transform.position.x;
            currentGap = minGap;
            UpdateBarPositions();
        }

        public void SetActive(bool active)
        {
            isGameActive = active;
        }

        void Update()
        {
            if (!isGameActive) return;

            HandleMouseMovement();
            HandleSpaceKeyOpening();
            UpdateBarPositions();
        }

        private void HandleMouseMovement()
        {
            if (Mouse.current != null)
            {
                // マウスのデルタ移動量を取得
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                
                // 感度をかけてX座標を更新し、範囲内に収める
                currentX += mouseDelta.x * mouseSensitivity;
                currentX = Mathf.Clamp(currentX, minX, maxX);
                
                // 親オブジェクト自身のX座標を更新
                Vector3 pos = transform.position;
                pos.x = currentX;
                transform.position = pos;
            }
        }

        private void HandleSpaceKeyOpening()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            {
                // スペースキー押下中：間隔を広げる
                currentGap = Mathf.MoveTowards(currentGap, maxGap, openingSpeed * Time.deltaTime);
            }
            else
            {
                // スペースキー離上時：間隔を狭める
                currentGap = Mathf.MoveTowards(currentGap, minGap, closingSpeed * Time.deltaTime);
            }
        }

        private void UpdateBarPositions()
        {
            if (leftBar != null && rightBar != null)
            {
                // 中心からのオフセット距離（Gapの半分）
                float halfGap = currentGap / 2f;

                // 左棒はローカルで -X 方向、右棒は +X 方向に配置
                leftBar.localPosition = new Vector3(-halfGap, leftBar.localPosition.y, leftBar.localPosition.z);
                rightBar.localPosition = new Vector3(halfGap, rightBar.localPosition.y, rightBar.localPosition.z);
            }
        }
    }
}
