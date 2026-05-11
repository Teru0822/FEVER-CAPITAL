using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 鉄球落としの棒（レール）と半透明操作オブジェクトの制御クラス。
    /// - マウスで操作ハンドルをドラッグする
    /// - ハンドルを奥（支点）へ押すとV字に開き、手前へ引くと閉じる
    /// - ハンドルを左右へ動かすと、全体（棒とハンドル）が左右へ移動する
    /// </summary>
    public class BarController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("左側の棒のTransform（原点が上部の支点にあること）")]
        [SerializeField] private Transform leftBar;
        [Tooltip("右側の棒のTransform（原点が上部の支点にあること）")]
        [SerializeField] private Transform rightBar;
        [Tooltip("操作用の半透明のオブジェクト（ハンドルのTransform）")]
        [SerializeField] private Transform operationHandle;

        [Header("Physics Settings")]
        [Tooltip("棒全体の移動を物理的に行うためのRigidbody（IsKinematic推奨）")]
        [SerializeField] private Rigidbody parentRigidbody;

        [Header("Settings (Horizontal Movement)")]
        [Tooltip("移動可能なX座標の最小値（左端）")]
        [SerializeField] private float minX = -5f;
        [Tooltip("移動可能なX座標の最大値（右端）")]
        [SerializeField] private float maxX = 5f;

        [Header("Settings (Opening Angle & Z Depth)")]
        [Tooltip("支点（奥）に一番近いときのハンドルのローカルZ座標")]
        [SerializeField] private float handleMinZ = 0.0f;
        [Tooltip("一番手前にあるときのハンドルのローカルZ座標")]
        [SerializeField] private float handleMaxZ = 5.0f;
        [Tooltip("ハンドルが一番奥にある時（支点に近い時）のV字開き角度")]
        [SerializeField] private float maxAngle = 30.0f;
        [Tooltip("ハンドルが一番手前にある時のV字開き角度（平行なら0）")]
        [SerializeField] private float minAngle = 0.0f;
        
        [Header("Settings (Base Gap)")]
        [Tooltip("支点での固定された隙間の広さ（ボールが転がる幅）")]
        [SerializeField] private float baseGap = 1.0f;

        private float currentBaseX;
        private bool isGameActive = true; 
        private bool isDragging = false;
        
        // ドラッグ開始時のベースとマウス位置とのズレを記憶
        private float dragOffsetX; 
        private float dragOffsetZ;

        void Start()
        {
            if (parentRigidbody == null) parentRigidbody = GetComponent<Rigidbody>();
            
            if (parentRigidbody != null && !parentRigidbody.isKinematic)
            {
                Debug.LogWarning("BarController: parentRigidbody は IsKinematic=true に設定することを推奨します。");
            }

            currentBaseX = transform.position.x;
            UpdateBarsState();
        }

        public void SetActive(bool active)
        {
            isGameActive = active;
            if (!active) isDragging = false;
        }

        void Update()
        {
            if (!isGameActive) return;

            HandleMouseInput();
            UpdateBarsState();
        }

        private void HandleMouseInput()
        {
            if (Mouse.current == null || Camera.main == null) return;

            // マウスクリック開始
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // クリックした対象が半透明ハンドル（operationHandle）か判定
                    if (operationHandle != null && (hit.transform == operationHandle || hit.transform.IsChildOf(operationHandle)))
                    {
                        isDragging = true;

                        // 傾斜面に対応した仮想平面（Plane）を作成してマウスの3D座標を取得
                        Plane dragPlane = new Plane(transform.up, transform.position);
                        if (dragPlane.Raycast(ray, out float enter))
                        {
                            Vector3 worldHitPoint = ray.GetPoint(enter);
                            
                            // クリックした位置と現在のベース位置(X)の差分を記録
                            dragOffsetX = transform.position.x - worldHitPoint.x;
                            
                            // ハンドルの現在のZ位置と、クリックされたローカルZ位置の差分を記録
                            Vector3 localHitPoint = transform.InverseTransformPoint(worldHitPoint);
                            dragOffsetZ = operationHandle.localPosition.z - localHitPoint.z;
                        }
                    }
                }
            }

            // マウス離上
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }
        }

        private void FixedUpdate()
        {
            // 物理演算（移動）はFixedUpdateで行う
            if (isDragging && Mouse.current != null && Camera.main != null && operationHandle != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                Plane dragPlane = new Plane(transform.up, transform.position);

                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 worldHitPoint = ray.GetPoint(enter);
                    
                    // --- 左右移動（ベース全体）の計算 ---
                    // クリック時のオフセットを加味して、滑らかに追従させる
                    currentBaseX = Mathf.Clamp(worldHitPoint.x + dragOffsetX, minX, maxX);
                    
                    Vector3 newPos = transform.position;
                    newPos.x = currentBaseX;

                    if (parentRigidbody != null)
                    {
                        parentRigidbody.MovePosition(newPos);
                    }
                    else
                    {
                        transform.position = newPos;
                    }

                    // --- 縦移動（ハンドルのZ軸移動）の計算 ---
                    // マウス座標をベースのローカル座標系に変換
                    Vector3 localHitPoint = transform.InverseTransformPoint(worldHitPoint);
                    
                    // ハンドルのZ移動先を計算して制限する
                    float targetZ = localHitPoint.z + dragOffsetZ;
                    float clampedZ = Mathf.Clamp(targetZ, handleMinZ, handleMaxZ);
                    
                    Vector3 handlePos = operationHandle.localPosition;
                    handlePos.z = clampedZ;
                    operationHandle.localPosition = handlePos;
                }
            }
        }

        private void UpdateBarsState()
        {
            if (leftBar == null || rightBar == null || operationHandle == null) return;

            // 支点からのハンドルの距離割合を計算 (0: 支点に一番近い, 1: 一番遠い)
            float t = Mathf.InverseLerp(handleMinZ, handleMaxZ, operationHandle.localPosition.z);
            
            // 割合に応じて角度を決定（t=0 なら maxAngle[開く], t=1 なら minAngle[閉じる]）
            float currentAngle = Mathf.Lerp(maxAngle, minAngle, t);

            float halfBaseGap = baseGap / 2f;

            // 左棒・右棒の根元の隙間（localPosition）を固定
            leftBar.localPosition = new Vector3(-halfBaseGap, leftBar.localPosition.y, leftBar.localPosition.z);
            rightBar.localPosition = new Vector3(halfBaseGap, rightBar.localPosition.y, rightBar.localPosition.z);

            // Y軸を中心にローカル回転させてV字にする（奥が支点前提）
            leftBar.localRotation = Quaternion.Euler(0, -currentAngle, 0);
            rightBar.localRotation = Quaternion.Euler(0, currentAngle, 0);
        }
    }
}
