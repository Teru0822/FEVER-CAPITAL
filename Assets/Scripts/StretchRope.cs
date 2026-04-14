using UnityEngine;

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
        // Spaceキーを押している間だけ下に伸びる
        if (Input.GetKey(KeyCode.Space))
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
        // スケール30に対して移動距離0.3の比率（0.01倍）を固定で適用
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
