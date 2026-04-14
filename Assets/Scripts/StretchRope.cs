using UnityEngine;

public class StretchRope : MonoBehaviour
{
    public enum Axis { X, Y, Z, None }

    [Header("Stretch Settings")]
    [Tooltip("自動で伸縮するか、Spaceキーで手動操作するか")]
    public bool autoStretch = true;
    
    [Tooltip("最大どれくらい伸びるか")]
    public float stretchLength = 1f;
    
    [Tooltip("伸縮スピード")]
    public float stretchSpeed = 2f;
    
    [Header("Axis Settings")]
    [Tooltip("どの軸方向にスケール(長さ)を伸ばすか（FBXの場合はZ軸が多いです）")]
    public Axis scaleAxis = Axis.Z;

    [Tooltip("位置(ポジション)をどの方向にズラすか（FBXのPivotが中心の時に上端を固定するため。基本は親空間のY軸マイナス、つまり下に移動させます）")]
    public Axis moveAxis = Axis.Y;
    public bool moveNegative = true;

    [Tooltip("伸びた時の位置補正の強さ（中心Pivotの場合は0.5周辺で調整）")]
    public float positionOffsetMultiplier = 0.5f;

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

        // 滑らかな伸縮
        float t = Mathf.SmoothStep(0, 1, stretchTime);
        float currentStretch = stretchLength * t;

        // 指定した軸のスケールだけを伸ばす
        Vector3 newScale = originalScale;
        switch (scaleAxis)
        {
            case Axis.X: newScale.x += currentStretch; break;
            case Axis.Y: newScale.y += currentStretch; break;
            case Axis.Z: newScale.z += currentStretch; break;
        }
        transform.localScale = newScale;

        // 位置の補正
        if (positionOffsetMultiplier != 0f && moveAxis != Axis.None)
        {
            float offsetAmount = currentStretch * positionOffsetMultiplier;
            float direction = moveNegative ? -1f : 1f;

            Vector3 newPos = originalPosition;
            switch (moveAxis)
            {
                case Axis.X: newPos.x += offsetAmount * direction; break;
                case Axis.Y: newPos.y += offsetAmount * direction; break;
                case Axis.Z: newPos.z += offsetAmount * direction; break;
            }
            transform.localPosition = newPos;
        }
    }
}
