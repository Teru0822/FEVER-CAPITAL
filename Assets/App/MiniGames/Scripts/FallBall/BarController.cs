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

        [Header("Physics Settings")]
        [Tooltip("棒全体の移動を物理的に行うためのRigidbody（IsKinematic推奨）")]
        [SerializeField] private Rigidbody parentRigidbody;

        [Header("Movement Settings (Mouse)")]
        [Tooltip("マウス移動の感度")]
        [SerializeField] private float mouseSensitivity = 0.05f;
        [Tooltip("移動可能なX座標の最小値")]
        [SerializeField] private float minX = -5f;
        [Tooltip("移動可能なX座標の最大値")]
        [SerializeField] private float maxX = 5f;

        [Header("Opening Settings (V-Shape Space Key)")]
        [Tooltip("根元の固定された隙間の広さ（ボールが転がる幅）")]
        [SerializeField] private float baseGap = 1.0f;
        [Tooltip("棒の最小角度（閉じた時）")]
        [SerializeField] private float minAngle = 0.0f;
        [Tooltip("棒の最大角度（最大に開いた時）")]
        [SerializeField] private float maxAngle = 30.0f;
        [Tooltip("角度が開く速度")]
        [SerializeField] private float openingSpeed = 50.0f;
        [Tooltip("角度が閉じる速度")]
        [SerializeField] private float closingSpeed = 50.0f;

        private float currentX;
        private float currentAngle;
        private bool isGameActive = true; 
        private bool isDragging = false;

        void Start()
        {
            if (parentRigidbody == null) parentRigidbody = GetComponent<Rigidbody>();
            
            // Auto check
            if (parentRigidbody != null && !parentRigidbody.isKinematic)
            {
                Debug.LogWarning("BarController: parentRigidbody は isKinematic を true にすることを推奨します。");
            }

            currentX = transform.position.x;
            currentAngle = minAngle;
            UpdateBarPositionsAndRotations();
        }

        public void SetActive(bool active)
        {
            isGameActive = active;
            if (!active) isDragging = false; // 非アクティブ時はドラッグ解除
        }

        void Update()
        {
            if (!isGameActive) return;

            HandleMouseInput();
            HandleSpaceKeyOpening();
            UpdateBarPositionsAndRotations();
        }

        private void HandleMouseInput()
        {
            if (Mouse.current == null || Camera.main == null) return;

            // マウスクリックの瞬間
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Ray ray = Camera.main.ScreenPointToRay(mousePos);

                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // クリックしたのが左棒・右棒、あるいは自身のいずれかだったらドラッグ開始
                    if (hit.transform == leftBar || hit.transform == rightBar || hit.transform == transform || hit.transform.IsChildOf(transform))
                    {
                        isDragging = true;
                    }
                }
            }

            // マウス離した瞬間
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }
        }

        private void FixedUpdate()
        {
            // 物理演算（移動）はFixedUpdateで行う
            if (isDragging && Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                
                currentX += mouseDelta.x * mouseSensitivity;
                currentX = Mathf.Clamp(currentX, minX, maxX);
                
                Vector3 newPos = transform.position;
                newPos.x = currentX;

                if (parentRigidbody != null)
                {
                    // 物理に即した移動
                    parentRigidbody.MovePosition(newPos);
                }
                else
                {
                    // Rigidbodyがない場合のフォールバック
                    transform.position = newPos;
                }
            }
        }

        private void HandleSpaceKeyOpening()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            {
                // スペースキー押下中：角度を開く
                currentAngle = Mathf.MoveTowards(currentAngle, maxAngle, openingSpeed * Time.deltaTime);
            }
            else
            {
                // スペースキー離上時：角度を狭める
                currentAngle = Mathf.MoveTowards(currentAngle, minAngle, closingSpeed * Time.deltaTime);
            }
        }

        private void UpdateBarPositionsAndRotations()
        {
            if (leftBar != null && rightBar != null)
            {
                float halfBaseGap = baseGap / 2f;

                // 根元の隙間（localPosition）は常に固定する
                leftBar.localPosition = new Vector3(-halfBaseGap, leftBar.localPosition.y, leftBar.localPosition.z);
                rightBar.localPosition = new Vector3(halfBaseGap, rightBar.localPosition.y, rightBar.localPosition.z);

                // Y軸（またはZ軸）を中心にローカル回転させてV字にする
                // ※棒が前(Z方向)に伸びている前提（Y軸中心で回転）
                leftBar.localRotation = Quaternion.Euler(0, -currentAngle, 0);
                rightBar.localRotation = Quaternion.Euler(0, currentAngle, 0);
            }
        }
    }
}
