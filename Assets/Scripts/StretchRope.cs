using UnityEngine;

public class StretchRope : MonoBehaviour
{
    public enum Axis { X, Y, Z, None }

    [Header("Animation Settings")]
    [Tooltip("自動で伸縮するか、Spaceキーで手動操作するか")]
    public bool autoStretch = true;
    
    [Tooltip("伸縮スピード")]
    public float stretchSpeed = 2f;
    
    [Header("Scale (伸縮) Settings")]
    [Tooltip("どの軸方向にスケール(長さ)を伸ばすか（FBXの場合はZ軸が多いです）")]
    public Axis scaleAxis = Axis.Z;

    [Tooltip("伸縮の強さ（最大でどれくらいスケールを増やすか）")]
    public float stretchScaleAmount = 1f;

    [Header("Movement (上下移動) Settings")]
    [Tooltip("位置(ポジション)をどの方向に動かすか（親空間のY軸マイナスなど）")]
    public Axis moveAxis = Axis.Y;
    public bool moveNegative = true;

    [Tooltip("上下移動の距離（ここで移動の強さを直接設定できるようになりました。伸びる量に合わせて微調整してください）")]
    public float moveDistanceAmount = 0.5f;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private float stretchTime;

    void Start()
    {
        originalScale = transform.localScale;
        originalPosition = transform.localPosition;
    }

    // Animator等がスケールを上書きするのを防ぐため、LateUpdateで適用します。
    void LateUpdate()
    {
        if (autoStretch)
        {
            // 0 ～ 1 のサイン波
            stretchTime = (Mathf.Sin(Time.time * stretchSpeed) + 1f) / 2f;
        }
        else
        {
            if (Input.GetKey(KeyCode.Space))
                stretchTime += Time.deltaTime * stretchSpeed;
            else
                stretchTime -= Time.deltaTime * stretchSpeed;
                
            stretchTime = Mathf.Clamp01(stretchTime);
        }

        // 滑らかな伸縮 (0 ~ 1の割合)
        float t = Mathf.SmoothStep(0, 1, stretchTime);

        // --- 伸縮(スケール)の適用 ---
        float currentScaleAdd = stretchScaleAmount * t;
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
            float currentMove = moveDistanceAmount * t;
            float direction = moveNegative ? -1f : 1f;

            Vector3 newPos = originalPosition;
            switch (moveAxis)
            {
                case Axis.X: newPos.x += currentMove * direction; break;
                case Axis.Y: newPos.y += currentMove * direction; break;
                case Axis.Z: newPos.z += currentMove * direction; break;
            }
            transform.localPosition = newPos;
        }
    }
}
