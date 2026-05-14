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
        [Tooltip("左側の棒のTransform（原点が上部の支点にあること）")]
        [SerializeField] private Transform leftBar;
        [Tooltip("右側の棒のTransform（原点が上部の支点にあること）")]
        [SerializeField] private Transform rightBar;
        [Tooltip("操作用の半透明のオブジェクト（ハンドルのTransform）")]
        [SerializeField] private Transform operationHandle;

        [Header("Physics Settings")]
        [Tooltip("棒全体の移動を物理的に行うためのRigidbody（IsKinematic推奨）")]
        [SerializeField] private Rigidbody parentRigidbody;

        [Header("Settings (Rotation Aim)")]
        [Tooltip("マウス左右移動による回転（エイム）の感度")]
        [SerializeField] private float yawSensitivity = 0.2f;
        [Tooltip("左方向への最大回転角度（ローカル）")]
        [SerializeField] private float minYaw = -45f;
        [Tooltip("右方向への最大回転角度（ローカル）")]
        [SerializeField] private float maxYaw = 45f;

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

        void Start()
        {
            if (Application.isPlaying)
            {
                if (operationHandle == null)
                {
                    Debug.LogError("🚨【重要エラー】Inspectorの『Operation Handle』にハンドルがドラッグ＆ドロップされていません！");
                }

                if (parentRigidbody == null) parentRigidbody = GetComponent<Rigidbody>();
                
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
                    allHits += $"[{hit.transform.name}] ";

                    if (operationHandle != null && (hit.transform == operationHandle || hit.transform.IsChildOf(operationHandle)))
                    {
                        hitHandle = true;
                        Debug.Log("✅ BarController: ハンドルのクリックを検知しました！");
                        break; // ハンドルが見つかったら終了
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
        }

        private void FixedUpdate()
        {
            // 物理演算（移動・回転）はFixedUpdateで行う
            if (isDragging && Mouse.current != null && Camera.main != null && operationHandle != null)
            {
                // --- 左右操作（ベース全体の回転：エイム） ---
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                currentYaw += mouseDelta.x * yawSensitivity;
                currentYaw = Mathf.Clamp(currentYaw, minYaw, maxYaw);

                Quaternion newRot = initialRotation * Quaternion.Euler(0, currentYaw, 0);

                if (parentRigidbody != null)
                {
                    parentRigidbody.MoveRotation(newRot);
                }
                else
                {
                    transform.localRotation = newRot;
                }

                // --- 縦移動（ハンドルのZ軸移動）の計算 ---
                // 回転後の状態でPlaneを再計算してズレを防ぐ
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                Plane dragPlane = new Plane(transform.up, transform.position);

                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 worldHitPoint = ray.GetPoint(enter);
                    
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

            // ハンドルのローカルX, Yは常に中央(0)などに固定し、Z軸方向（前後）の動きだけに制限する
            operationHandle.localPosition = new Vector3(0, operationHandle.localPosition.y, operationHandle.localPosition.z);

            // Y軸を中心にローカル回転させてV字にする（奥が支点前提）
            float finalAngle = invertBarRotation ? -currentAngle : currentAngle;
            leftBar.localRotation = Quaternion.Euler(0, -finalAngle, 0);
            rightBar.localRotation = Quaternion.Euler(0, finalAngle, 0);
        }

        // ピボット（支点）の位置をエディタ上で可視化する
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            if (leftBar != null)
            {
                Gizmos.DrawSphere(leftBar.position, 0.1f);
                Gizmos.DrawRay(leftBar.position, leftBar.forward * 2f);
            }
            if (rightBar != null)
            {
                Gizmos.DrawSphere(rightBar.position, 0.1f);
                Gizmos.DrawRay(rightBar.position, rightBar.forward * 2f);
            }
        }
    }
}
