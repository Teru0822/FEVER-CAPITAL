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
    [ExecuteAlways]
    public class BarController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("左側の棒")]
        [SerializeField] private Transform leftBar;
        [Tooltip("右側の棒")]
        [SerializeField] private Transform rightBar;
        [Tooltip("左側の支柱（回転軸の位置として使用）")]
        [SerializeField] private Transform leftPivot;
        [Tooltip("右側の支柱（回転軸の位置として使用）")]
        [SerializeField] private Transform rightPivot;
        [Tooltip("操作用の半透明のオブジェクト（ハンドルのTransform）")]
        [SerializeField] private Transform operationHandle;

        [Header("Physics Settings")]
        [Tooltip("棒全体の移動を物理的に行うためのRigidbody（IsKinematic推奨）")]
        [SerializeField] private Rigidbody parentRigidbody;
        [Tooltip("左右エイムの対象。未設定の場合は自身(transform)を回転")]
        [SerializeField] private Transform aimTarget;
        [Tooltip("左右エイムを無効化する場合はチェックを外してください")]
        [SerializeField] private bool enableYawAim = false;

        [Header("Settings (Rotation Aim)")]
        [Tooltip("マウス左右移動による回転（エイム）の感度")]
        [SerializeField] private float yawSensitivity = 0.2f;
        [Tooltip("左方向への最大回転角度（ローカル）")]
        [SerializeField] private float minYaw = -45f;
        [Tooltip("右方向への最大回転角度（ローカル）")]
        [SerializeField] private float maxYaw = 45f;
        [Tooltip("エイムの左右の動きが逆だと感じる場合はチェックを入れてください")]
        [SerializeField] private bool invertAimDirection = false;

        [Header("Settings (Opening Angle & Z Depth)")]
        [Tooltip("支点（奥）に一番近いときのハンドルのローカルZ座標")]
        [SerializeField] private float handleMinZ = 0.0f;
        [Tooltip("一番手前にあるときのハンドルのローカルZ座標")]
        [SerializeField] private float handleMaxZ = 5.0f;
        [Tooltip("ハンドルが一番奥にある時（支点に近い時）のV字開き角度")]
        [SerializeField] private float maxAngle = 30.0f;
        [Tooltip("ハンドルが一番手前にある時のV字開き角度（平行なら0）")]
        [SerializeField] private float minAngle = 0.0f;
        [Tooltip("棒の開く向きが逆（交差してしまう等）の場合はチェックを入れてください")]
        [SerializeField] private bool invertBarRotation = false;
        
        [Header("Settings (Base Gap)")]
        [Tooltip("支点での固定された隙間の広さ（ボールが転がる幅）")]
        [SerializeField] private float baseGap = 1.0f;

        private float currentYaw = 0f;
        private Quaternion initialRotation;
        private bool isGameActive = true; 
        private bool isDragging = false;
        
        // ドラッグ開始時のベースとマウス位置とのズレを記憶
        private float dragOffsetZ;

        // 棒の初期ワールド状態を保存
        private Vector3 leftBarInitialPos;
        private Quaternion leftBarInitialRot;
        private Vector3 rightBarInitialPos;
        private Quaternion rightBarInitialRot;

        void Start()
        {
            if (Application.isPlaying)
            {
                Debug.Log($"BarController Start: isGameActive={isGameActive}");
                
                if (operationHandle == null)
                {
                    Debug.LogError("🚨【重要エラー】Inspectorの『Operation Handle』にハンドルがドラッグ＆ドロップされていません！");
                }

                // ゲーム開始時のワールド座標と回転を完全に保存
                if (leftBar != null)
                {
                    leftBarInitialPos = leftBar.position;
                    leftBarInitialRot = leftBar.rotation;
                }
                if (rightBar != null)
                {
                    rightBarInitialPos = rightBar.position;
                    rightBarInitialRot = rightBar.rotation;
                }

                if (parentRigidbody == null) parentRigidbody = GetComponent<Rigidbody>();
                initialRotation = transform.localRotation;
                currentYaw = 0f;
            }
            UpdateBarsState();
        }
                
                if (parentRigidbody != null && !parentRigidbody.isKinematic)
                {
                    Debug.LogWarning("BarController: parentRigidbody は IsKinematic=true に設定することを推奨します。");
                }

                initialRotation = transform.localRotation;
                currentYaw = 0f;
            }
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

            if (Application.isPlaying)
            {
                HandleMouseInput();
            }
            UpdateBarsState();
        }

        private void HandleMouseInput()
        {
            if (Mouse.current == null || Camera.main == null) return;

            // マウスクリック開始
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                
                // RaycastAllを使用して、他の透明なコライダーに遮られても貫通して判定する
                RaycastHit[] hits = Physics.RaycastAll(ray);
                bool hitHandle = false;
                
                // 【デバッグ用】当たったすべてのオブジェクトの名前を記録
                string allHits = "";

                foreach (var hit in hits)
                {
                    // hit.transformではなく、確実にColliderが付いているオブジェクトの名前を取得
                    allHits += $"[{hit.collider.gameObject.name}] ";

                    // hit.collider.transform と比較する（親にRigidbodyがある場合のUnityの仕様対策）
                    if (operationHandle != null && (hit.collider.transform == operationHandle || hit.collider.transform.IsChildOf(operationHandle)))
                    {
                        hitHandle = true;
                        Debug.Log("✅ BarController: ハンドルのクリックを検知しました！");
                        break; 
                    }
                }

                if (!hitHandle && hits.Length > 0)
                {
                    Debug.Log($"❌ BarController: クリックした線上にハンドルがありません。\n通過したオブジェクト: {allHits}");
                }
                else if (!hitHandle)
                {
                    Debug.Log("BarController: クリックしましたが何も当たりませんでした。\n※MainCameraタグが設定されているか確認してください。");
                }

                if (hitHandle)
                {
                    isDragging = true;

                    Plane dragPlane = new Plane(transform.up, transform.position);
                    if (dragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 worldHitPoint = ray.GetPoint(enter);
                        
                        // クリックした位置と現在のベース位置(X)の差分を記録
                        // ※ドラッグ開始時のワールドヒットポイントと実際のベースX座標の差を計算するロジックに変更
                        dragOffsetZ = operationHandle.localPosition.z - transform.InverseTransformPoint(worldHitPoint).z;
                    }
                }
            }

            // マウス離上
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }
            // ドラッグ中の処理（InputSystemの値はUpdateで取得する）
            if (isDragging)
            {
                // --- 左右操作（ベース全体の回転：エイム） ---
                if (enableYawAim)
                {
                    Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                    float yawChange = mouseDelta.x * yawSensitivity;
                    if (invertAimDirection) yawChange = -yawChange;
                    
                    currentYaw += yawChange;
                    currentYaw = Mathf.Clamp(currentYaw, minYaw, maxYaw);

                    // aimTarget が設定されていればそちらを回転、なければ自身を回転
                    Transform yawTarget = aimTarget != null ? aimTarget : transform;
                    if (parentRigidbody == null || aimTarget != null)
                    {
                        yawTarget.localRotation = initialRotation * Quaternion.Euler(0, currentYaw, 0);
                    }
                }

                // --- 縦移動（ハンドルのZ軸移動）の計算 ---
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                Plane dragPlane = new Plane(transform.up, transform.position);

                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 worldHitPoint = ray.GetPoint(enter);
                    Vector3 localHitPoint = transform.InverseTransformPoint(worldHitPoint);
                    
                    float targetZ = localHitPoint.z + dragOffsetZ;
                    
                    // HandleMinZとHandleMaxZの数値の大小関係が逆（Zがマイナス方向など）の場合に備え、正しい最小値・最大値を判定する
                    float actualMinZ = Mathf.Min(handleMinZ, handleMaxZ);
                    float actualMaxZ = Mathf.Max(handleMinZ, handleMaxZ);
                    
                    // 単純にMinとMaxの間で制限する
                    float clampedZ = Mathf.Clamp(targetZ, actualMinZ, actualMaxZ);
                    
                    Vector3 handlePos = operationHandle.localPosition;
                    handlePos.z = clampedZ;
                    operationHandle.localPosition = handlePos;

                    // 親にRigidbodyがある状態で子オブジェクトを動かすと当たり判定が取り残される問題の修正
                    Physics.SyncTransforms();
                }
            }

            // ゲームの状態に関わらず常に更新（デバッグ用）
            UpdateBarsState();
        }
        }

        private void FixedUpdate()
        {
            // 左右エイムが有効な場合のみ物理回転を適用
            if (Application.isPlaying && enableYawAim && parentRigidbody != null && parentRigidbody.isKinematic)
            {
                Transform yawTarget = aimTarget != null ? aimTarget : transform;
                Quaternion newRot = initialRotation * Quaternion.Euler(0, currentYaw, 0);
                parentRigidbody.MoveRotation(newRot);
            }
        }

        private void UpdateBarsState()
        {
            if (leftBar == null || rightBar == null || operationHandle == null) return;

            float handleZ = operationHandle.localPosition.z;
            float t = Mathf.InverseLerp(handleMinZ, handleMaxZ, handleZ);
            float currentAngle = Mathf.Lerp(maxAngle, minAngle, t);
            float finalAngle = invertBarRotation ? -currentAngle : currentAngle;

            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[BarController] Z={handleZ:F2}, Angle={finalAngle:F2}");
            }

            // 左の棒の制御
            if (leftPivot != null)
            {
                leftBar.SetPositionAndRotation(leftBarInitialPos, leftBarInitialRot);
                leftBar.RotateAround(leftPivot.position, transform.up, -finalAngle);
            }
            else
            {
                leftBar.localRotation = leftBarInitialRot * Quaternion.Euler(0, -finalAngle, 0);
            }

            // 右の棒の制御
            if (rightPivot != null)
            {
                rightBar.SetPositionAndRotation(rightBarInitialPos, rightBarInitialRot);
                rightBar.RotateAround(rightPivot.position, transform.up, finalAngle);
            }
            else
            {
                rightBar.localRotation = rightBarInitialRot * Quaternion.Euler(0, finalAngle, 0);
            }
        }

        // ピボット（支点）の位置をエディタ上で可視化する
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            if (leftBar != null) Gizmos.DrawRay(leftBar.position, leftBar.forward * 2f);
            if (rightBar != null) Gizmos.DrawRay(rightBar.position, rightBar.forward * 2f);

            // 回転軸（ピボット）を青い球で表示
            Gizmos.color = Color.blue;
            if (leftPivot != null) Gizmos.DrawSphere(leftPivot.position, 0.05f);
            if (rightPivot != null) Gizmos.DrawSphere(rightPivot.position, 0.05f);
            
            // 現在の回転軸（leftBarの現在位置）を赤い球で表示
            Gizmos.color = Color.red;
            if (leftBar != null) Gizmos.DrawSphere(leftBar.position, 0.03f);
        }
    }
}
