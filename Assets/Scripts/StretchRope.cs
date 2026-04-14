using UnityEngine;
using UnityEngine.InputSystem;

public class StretchRope : MonoBehaviour
{
    public enum Axis { X, Y, Z, None }

    [Header("Animation Settings")]
    [Tooltip("伸縮スピード")]
    public float stretchSpeed = 2f;

    [Tooltip("伸縮の最大強さ（例: 30 に設定すると、最大30まで伸び、位置も最適な比率(0.01倍)で自動的に移動します）")]
    public float stretchIntensity = 30f;
    
    [Header("Axis Settings")]
    [Tooltip("どの軸方向にスケール(長さ)を伸ばすか（FBXの場合はZ軸が多いです）")]
    public Axis scaleAxis = Axis.Z;

    [Tooltip("位置(ポジション)をどの方向に動かすか（親空間のY軸マイナスなど）")]
    public Axis moveAxis = Axis.Y;
    public bool moveNegative = true;

    [Header("Attached Object Settings")]
    [Tooltip("伸びる先端に合わせて連動して動かすオブジェクト名（無効にする場合は空白）")]
    public string attachedObjectName = "7";

    [Tooltip("アタッチしたオブジェクトの移動倍率（中心Pivotのオブジェクトの端に追従させるなら通常は2.0です）")]
    public float attachedObjectMoveMultiplier = 2.0f;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private float stretchTime;

    private Transform attachedTransform;
    private Vector3 originalAttachedPosition;

    void Start()
    {
        originalScale = transform.localScale;
        originalPosition = transform.localPosition;

        if (!string.IsNullOrEmpty(attachedObjectName))
        {
            GameObject obj = GameObject.Find(attachedObjectName);
            if (obj != null)
            {
                attachedTransform = obj.transform;
                originalAttachedPosition = attachedTransform.localPosition;
            }
        }
    }

    // Animator等がスケールを上書きするのを防ぐため、LateUpdateで適用します。
    void LateUpdate()
    {
        bool isSpacePressed = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

        // Spaceキーを押している間だけ下に伸びる
        if (isSpacePressed)
        {
            stretchTime += Time.deltaTime * stretchSpeed;
        }
        else
        {
            // 離すと初期位置（0）まで縮む
            stretchTime -= Time.deltaTime * stretchSpeed;
        }
            
        // 0（初期位置）～ 1（最大StretchIntensity）に制限
        stretchTime = Mathf.Clamp01(stretchTime);

        // 滑らかな伸縮 (0 ~ 1の割合)
        float t = Mathf.SmoothStep(0, 1, stretchTime);

        // --- 伸縮の強さを決定 ---
        float currentScaleAdd = stretchIntensity * t;
        // スケールに対しての移動距離の比率（0.01倍）を固定で連動
        float currentMove = currentScaleAdd * 0.01f;

        // --- スケールの適用 ---
        Vector3 newScale = originalScale;
        switch (scaleAxis)
        {
            case Axis.X: newScale.x += currentScaleAdd; break;
            case Axis.Y: newScale.y += currentScaleAdd; break;
            case Axis.Z: newScale.z += currentScaleAdd; break;
        }
        transform.localScale = newScale;

        // --- 上下移動(位置)の適用 ---
        if (moveAxis != Axis.None)
        {
            float direction = moveNegative ? -1f : 1f;

            // ロープ（6オブジェクト）自身の位置移動
            Vector3 newPos = originalPosition;
            switch (moveAxis)
            {
                case Axis.X: newPos.x += currentMove * direction; break;
                case Axis.Y: newPos.y += currentMove * direction; break;
                case Axis.Z: newPos.z += currentMove * direction; break;
            }
            transform.localPosition = newPos;

            // 先端のオブジェクト（7オブジェクト）の連動移動
            if (attachedTransform != null)
            {
                Vector3 newAttachedPos = originalAttachedPosition;
                // ピボット中心のモデルが移動距離分ずれると、端の移動量はその2倍のためMultiplier(=2.0)をかける
                float attachedMove = currentMove * attachedObjectMoveMultiplier;
                
                switch (moveAxis)
                {
                    case Axis.X: newAttachedPos.x += attachedMove * direction; break;
                    case Axis.Y: newAttachedPos.y += attachedMove * direction; break;
                    case Axis.Z: newAttachedPos.z += attachedMove * direction; break;
                }
                attachedTransform.localPosition = newAttachedPos;
            }
        }
    }
}
