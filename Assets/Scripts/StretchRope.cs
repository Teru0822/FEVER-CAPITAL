using UnityEngine;

public class StretchRope : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Stretch Settings")]
    [Tooltip("自動で伸縮するか、Spaceキーで手動操作するか")]
    public bool autoStretch = true;
    
    [Tooltip("最大どれくらい伸びるか")]
    public float stretchLength = 1f;
    
    [Tooltip("伸縮スピード")]
    public float stretchSpeed = 2f;
    
    [Tooltip("どの軸方向に伸ばすか（FBXの向きに合わせて変更してください。Yがダメな場合はZを試してください）")]
    public Axis stretchAxis = Axis.Y;

    [Tooltip("伸びた時に位置を下に補正する係数（中心Pivotの場合は0.5、上部Pivotの場合は0など調整）")]
    public float positionOffsetMultiplier = 0.5f;

    [Tooltip("補正方向を「ワールド空間の下」にするか（ONにすると、オブジェクトの回転に関わらず真下に伸びるように補正します）")]
    public bool useWorldDownForOffset = false;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private float stretchTime;

    void Start()
    {
        originalScale = transform.localScale;
        originalPosition = transform.localPosition;
    }

    // Animator等がスケールを上書きするのを防ぐため、LateUpdateでスケールを適用します。
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
        switch (stretchAxis)
        {
            case Axis.X: newScale.x += currentStretch; break;
            case Axis.Y: newScale.y += currentStretch; break;
            case Axis.Z: newScale.z += currentStretch; break;
        }
        transform.localScale = newScale;

        // 中心固定になって両側に伸びてしまうのを防ぐため、位置を「下」へズラす
        if (positionOffsetMultiplier != 0f)
        {
            float offsetAmount = currentStretch * positionOffsetMultiplier;
            
            if (useWorldDownForOffset)
            {
                // ワールド空間での「真下（Vector3.down）」にズラす
                transform.position = (transform.parent != null) 
                    ? transform.parent.TransformPoint(originalPosition) + Vector3.down * offsetAmount
                    : originalPosition + Vector3.down * offsetAmount;
            }
            else
            {
                // ローカル空間でのマイナス方向にズラす
                Vector3 offsetDir = Vector3.zero;
                switch (stretchAxis)
                {
                    case Axis.X: offsetDir = Vector3.left; break;
                    case Axis.Y: offsetDir = Vector3.down; break;
                    case Axis.Z: offsetDir = Vector3.back; break;
                }
                transform.localPosition = originalPosition + (offsetDir * offsetAmount);
            }
        }
    }
}
