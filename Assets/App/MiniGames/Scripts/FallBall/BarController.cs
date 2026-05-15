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
        [Tooltip("棒全体の傾き（ステアリング）の軸となる共通オブジェクト")]
        [SerializeField] private Transform commonSteeringPivot;
        [Tooltip("クリック判定に使用するカメラ。未指定ならCamera.mainを使用")]
        [SerializeField] private Camera targetCamera;

        [Header("Settings (Angle/Opening)")]
        [SerializeField] private float handleMinZ = 0.0f;
        [SerializeField] private float handleMaxZ = 5.0f;
        [Tooltip("ハンドルの判定範囲（横長の直方体）")]
        [SerializeField] private Vector3 handleInteractionBoxSize = new Vector3(2.0f, 1.0f, 1.0f);
        [SerializeField] private float angleAtMinZ = 30.0f;
        [SerializeField] private float angleAtMaxZ = 0.0f;
        [SerializeField] private bool invertBarRotation = false;

        [Header("Settings (Steering)")]
        [SerializeField] private float handleMinX = -2.0f;
        [SerializeField] private float handleMaxX = 2.0f;
        [SerializeField] private float maxSteeringAngle = 30.0f;
        [SerializeField] private bool invertSteering = false;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private bool isGameActive = true; 
        private bool isDragging = false;
        private float dragOffsetX;
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
            
            // 1. 直接クリック判定 (コライダーがある場合)
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

            // 2. 距離判定 (Box判定)
            Plane plane = new Plane(transform.up, operationHandle.position);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                // ハンドルのローカル空間での相対位置を計算
                Vector3 localOffset = operationHandle.InverseTransformPoint(worldPoint);
                
                if (Mathf.Abs(localOffset.x) < handleInteractionBoxSize.x * 0.5f &&
                    Mathf.Abs(localOffset.y) < handleInteractionBoxSize.y * 0.5f &&
                    Mathf.Abs(localOffset.z) < handleInteractionBoxSize.z * 0.5f)
                {
                    Debug.Log("BarController: Box判定内でハンドルを掴みました");
                    StartDrag(ray);
                }
            }
        }

        private void StartDrag(Ray ray)
        {
            isDragging = true;
            Plane plane = new Plane(transform.up, operationHandle.position);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                
                // ドラッグ開始時のマウス位置とハンドルのlocalPositionの差分を記録
                dragOffsetX = operationHandle.localPosition.x - localPoint.x;
                dragOffsetZ = operationHandle.localPosition.z - localPoint.z;
            }
        }

        private void UpdateDrag()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane plane = new Plane(transform.up, operationHandle.position);

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

                // ハンドルの移動制限範囲を適用
                float targetX = localPoint.x + dragOffsetX;
                float targetZ = localPoint.z + dragOffsetZ;

                float minX = Mathf.Min(handleMinX, handleMaxX);
                float maxX = Mathf.Max(handleMinX, handleMaxX);
                float minZ = Mathf.Min(handleMinZ, handleMaxZ);
                float maxZ = Mathf.Max(handleMinZ, handleMaxZ);

                Vector3 pos = operationHandle.localPosition;
                pos.x = Mathf.Clamp(targetX, minX, maxX);
                pos.z = Mathf.Clamp(targetZ, minZ, maxZ);
                operationHandle.localPosition = pos;
            }
        }

        private void UpdateBars()
        {
            if (leftBar == null || rightBar == null || operationHandle == null) return;

            // 1. 開閉角の計算 (Z軸)
            float tZ = Mathf.InverseLerp(handleMinZ, handleMaxZ, operationHandle.localPosition.z);
            float openingAngle = Mathf.Lerp(angleAtMinZ, angleAtMaxZ, tZ);
            if (invertBarRotation) openingAngle = -openingAngle;

            // 2. 傾き角の計算 (X軸)
            float tX = Mathf.InverseLerp(handleMinX, handleMaxX, operationHandle.localPosition.x);
            float steeringAngle = Mathf.Lerp(-maxSteeringAngle, maxSteeringAngle, tX);
            if (invertSteering) steeringAngle = -steeringAngle;

            // 左右の棒を更新
            RotateBar(leftBar, leftPivot, leftBarInitialLocalPos, leftBarInitialLocalRot, -openingAngle, steeringAngle);
            RotateBar(rightBar, rightPivot, rightBarInitialLocalPos, rightBarInitialLocalRot, openingAngle, steeringAngle);
        }

        private void RotateBar(Transform bar, Transform pivot, Vector3 initialPos, Quaternion initialRot, float openingAngle, float steeringAngle)
        {
            if (bar == null) return;

            // 状態をリセット
            bar.localPosition = initialPos;
            bar.localRotation = initialRot;

            Vector3 worldUp = transform.up;

            // Step 1: 各棒のPivotを中心に「開閉」
            if (pivot != null)
            {
                bar.RotateAround(pivot.position, worldUp, openingAngle);
            }

            // Step 2: 共通のPivotを中心に「傾き（ステアリング）」
            if (commonSteeringPivot != null)
            {
                bar.RotateAround(commonSteeringPivot.position, worldUp, steeringAngle);
            }
        }

        private void OnDrawGizmos()
        {
            // 回転軸を青い点で表示
            Gizmos.color = Color.blue;
            if (leftPivot != null) Gizmos.DrawSphere(leftPivot.position, 0.05f);
            if (rightPivot != null) Gizmos.DrawSphere(rightPivot.position, 0.05f);
            if (commonSteeringPivot != null) Gizmos.DrawSphere(commonSteeringPivot.position, 0.07f);
            
            // 棒の付け根を赤い点で表示
            Gizmos.color = Color.red;
            if (leftBar != null) Gizmos.DrawSphere(leftBar.position, 0.03f);
            if (rightBar != null) Gizmos.DrawSphere(rightBar.position, 0.03f);

            // ハンドルの当たり判定を緑色の直方体で表示
            if (operationHandle != null)
            {
                Gizmos.color = Color.green;
                Gizmos.matrix = operationHandle.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, handleInteractionBoxSize);
                Gizmos.matrix = Matrix4x4.identity;
                
                Gizmos.color = Color.green;
                Gizmos.DrawLine(operationHandle.position, operationHandle.position + transform.up * 0.5f);
            }
        }
    }
}
