using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniGames.FallBall
{
    public class BarController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform leftBar;
        [SerializeField] private Transform rightBar;
        [Tooltip("回転の軸となる位置（青い点）。ここを中心に回転します")]
        [SerializeField] private Transform leftPivot;
        [Tooltip("回転の軸となる位置（青い点）。ここを中心に回転します")]
        [SerializeField] private Transform rightPivot;
        [SerializeField] private Transform operationHandle;
        [Tooltip("クリック判定に使用するカメラ。未指定ならCamera.mainを使用")]
        [SerializeField] private Camera targetCamera;

        [Header("Settings (Angle)")]
        [SerializeField] private float handleMinZ = 0.0f;
        [SerializeField] private float handleMaxZ = 5.0f;
        [SerializeField] private float angleAtMinZ = 30.0f;
        [SerializeField] private float angleAtMaxZ = 0.0f;
        [SerializeField] private bool invertBarRotation = false;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private bool isGameActive = true; 
        private bool isDragging = false;
        private float dragOffsetZ;

        private Vector3 leftBarInitialLocalPos;
        private Quaternion leftBarInitialLocalRot;
        private Vector3 rightBarInitialLocalPos;
        private Quaternion rightBarInitialLocalRot;

        void Start()
        {
            if (leftBar != null)
            {
                leftBarInitialLocalPos = leftBar.localPosition;
                leftBarInitialLocalRot = leftBar.localRotation;
            }
            if (rightBar != null)
            {
                rightBarInitialLocalPos = rightBar.localPosition;
                rightBarInitialLocalRot = rightBar.localRotation;
            }
        }

        public void SetActive(bool active)
        {
            isGameActive = active;
        }

        void Update()
        {
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                CheckClick();
            }
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }

            if (isDragging && isGameActive)
            {
                UpdateDrag();
            }

            UpdateBars();
        }

        private void CheckClick()
        {
            if (operationHandle == null) return;

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            // 全てのレイヤーを対象にするため、レイヤーマスクに -1 を指定
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, -1);

            foreach (var hit in hits)
            {
                if (hit.transform == operationHandle || hit.transform.IsChildOf(operationHandle))
                {
                    Debug.Log($"BarController: ハンドル「{hit.transform.name}」を直接クリックしました");
                    StartDrag(ray);
                    return;
                }
            }

            // 2. コライダーがない場合の距離判定（ハンドルの近くならOKとする）
            Plane plane = new Plane(transform.up, transform.position);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                float dist = Vector3.Distance(worldPoint, operationHandle.position);
                if (dist < 1.0f) // 1メートル以内に拡大
                {
                    Debug.Log($"BarController: 距離判定({dist:F2})でハンドルを掴みました");
                    StartDrag(ray);
                }
                else
                {
                    // 届かなかった場合もログを出して距離を教える
                    if (dist < 5.0f) Debug.Log($"BarController: クリック位置が遠すぎます (距離: {dist:F2}m)");
                }
            }
        }

        private void StartDrag(Ray ray)
        {
            isDragging = true;
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            Plane plane = new Plane(transform.up, transform.position);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                dragOffsetZ = operationHandle.localPosition.z - transform.InverseTransformPoint(worldPoint).z;
            }
        }

        private void UpdateDrag()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane plane = new Plane(transform.up, transform.position);

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                float targetZ = transform.InverseTransformPoint(worldPoint).z + dragOffsetZ;
                float minZ = Mathf.Min(handleMinZ, handleMaxZ);
                float maxZ = Mathf.Max(handleMinZ, handleMaxZ);
                
                Vector3 pos = operationHandle.localPosition;
                pos.z = Mathf.Clamp(targetZ, minZ, maxZ);
                operationHandle.localPosition = pos;
            }
        }

        private void UpdateBars()
        {
            if (leftBar == null || rightBar == null || operationHandle == null) return;

            float t = Mathf.InverseLerp(handleMinZ, handleMaxZ, operationHandle.localPosition.z);
            float angle = Mathf.Lerp(angleAtMinZ, angleAtMaxZ, t);
            if (invertBarRotation) angle = -angle;

            RotateBar(leftBar, leftPivot, leftBarInitialLocalPos, leftBarInitialLocalRot, -angle);
            RotateBar(rightBar, rightPivot, rightBarInitialLocalPos, rightBarInitialLocalRot, angle);
        }

        private void RotateBar(Transform bar, Transform pivot, Vector3 initialPos, Quaternion initialRot, float angle)
        {
            if (bar == null) return;

            // 状態をリセット
            bar.localPosition = initialPos;
            bar.localRotation = initialRot;

            if (pivot != null)
            {
                // pivotのローカル空間での回転軸（上方向）を使って回転
                // RotateAroundはワールド座標系で動くため、軸もワールドに変換
                Vector3 worldAxis = transform.up; 
                bar.RotateAround(pivot.position, worldAxis, angle);
            }
        }

        private void OnDrawGizmos()
        {
            // 回転軸を青い点で表示（これを動かしてください！）
            Gizmos.color = Color.blue;
            if (leftPivot != null) Gizmos.DrawSphere(leftPivot.position, 0.05f);
            if (rightPivot != null) Gizmos.DrawSphere(rightPivot.position, 0.05f);
            
            // 棒の付け根を赤い点で表示
            Gizmos.color = Color.red;
            if (leftBar != null) Gizmos.DrawSphere(leftBar.position, 0.03f);
            if (rightBar != null) Gizmos.DrawSphere(rightBar.position, 0.03f);

            // ハンドルの当たり判定（クリックできる場所）を緑色で表示
            if (operationHandle != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(operationHandle.position, Vector3.one * 0.2f);
                Gizmos.DrawLine(operationHandle.position, operationHandle.position + transform.up * 0.5f);
            }
        }
    }
}
