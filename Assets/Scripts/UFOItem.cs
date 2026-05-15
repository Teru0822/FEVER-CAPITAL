using UnityEngine;

public enum UFOItemType
{
    CopperCoin,
    SilverCoin,
    GoldCoin,
    Watch
}

/// <summary>
/// 各アイテムのプレハブ（銅・銀・金・時計）にアタッチするクラス
/// 自身の価値や種類を定義する
/// </summary>
public class UFOItem : MonoBehaviour
{
    [Tooltip("アイテムの種類")]
    public UFOItemType itemType;

    [Tooltip("このアイテムが落とし口に入った時に貰える基本金額")]
    public float baseValue = 100f;
}
