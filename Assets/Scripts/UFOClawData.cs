using UnityEngine;

[System.Serializable]
public class UFOClawData : MonoBehaviour
{
    [Header("カスタム開閉設定（UFOArmControllerと同じ項目です）")]
    public float fingerOpenAngle = 40f;
    public bool[] invertFingerAngle;
    public Vector3[] fingerAngleOffsets;

    [Header("完全にカスタムな位置・角度を使う場合")]
    public bool useCustomOpenTransform;
    public Vector3[] customOpenLocalPositions;
    public Vector3[] customOpenLocalRotations;
}
