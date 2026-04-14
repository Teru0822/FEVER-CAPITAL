using UnityEngine;

public class StretchRope : MonoBehaviour
{
    [Header("Stretch Settings")]
    [Tooltip("Spaceキーを押している間伸びるか、自動で伸縮するか")]
    public bool autoStretch = false;
    
    [Tooltip("最大どれくらい下方向に伸びるか")]
    public float stretchLength = 1f;
    
    [Tooltip("伸縮スピード")]
    public float stretchSpeed = 2f;
    
    [Tooltip("スケール変更時に上端位置を合わせるための逆オフセット（Pivotが中心のモデル向け）")]
    public bool adjustPositionForCenterPivot = true;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private float stretchTime;

    void Start()
    {
        originalScale = transform.localScale;
        originalPosition = transform.localPosition;
    }

    void Update()
    {
        if (autoStretch)
        {
            // 自動でびよ〜んと伸縮する：0 〜 1 のサイン波
            stretchTime = (Mathf.Sin(Time.time * stretchSpeed) + 1f) / 2f;
        }
        else
        {
            // Spaceキーを押している間伸びる
            if (Input.GetKey(KeyCode.Space))
            {
                stretchTime += Time.deltaTime * stretchSpeed;
            }
            else
            {
                stretchTime -= Time.deltaTime * stretchSpeed;
            }
            
            // 0から1の範囲に制限
            stretchTime = Mathf.Clamp01(stretchTime);
        }

        // バネっぽい伸び感（少しイーズインアウト）
        float t = Mathf.SmoothStep(0, 1, stretchTime);

        float currentStretch = stretchLength * t;
        
        // Z(あるいはY)など、モデルの向き依存だが、ここではY軸に伸びると仮定
        transform.localScale = new Vector3(originalScale.x, originalScale.y + currentStretch, originalScale.z);

        // 下方向にだけ伸びるように、位置をY軸下方向に補正
        if (adjustPositionForCenterPivot)
        {
            transform.localPosition = new Vector3(
                originalPosition.x,
                originalPosition.y - (currentStretch / 2f),
                originalPosition.z
            );
        }
    }
}
