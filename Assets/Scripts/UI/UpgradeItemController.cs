using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 強化アイテム1つ（ボタン）にアタッチして、購入処理とお金のチェックを行うクラス
/// </summary>
public class UpgradeItemController : MonoBehaviour
{
    [Header("UI参照")]
    [Tooltip("このアイテム自体のButtonコンポーネント")]
    public Button buyButton;
    [Tooltip("値段を表示するテキスト")]
    public TMP_Text priceText;
    [Tooltip("現在のレベルを表示するテキスト（任意・空欄でも可）")]
    public TMP_Text levelText;

    [Header("アイテム基本設定")]
    [Tooltip("アイテムの名前（ログ用）")]
    public string itemName = "キャッチャー交換";
    [Tooltip("初期の購入金額")]
    public float baseCost = 200f;

    [Header("強化スケーリング設定")]
    [Tooltip("買い切り（1回しか買えない）場合はチェックを入れる")]
    public bool isOneTimePurchase = false;
    [Tooltip("最大レベル（0なら無限に強化可能）")]
    public int maxLevel = 0;
    [Tooltip("購入ごとに価格に乗算される倍率（例: 1.5なら毎回1.5倍）")]
    public float costMultiplier = 1.2f;
    [Tooltip("購入ごとに価格に加算される固定値（例: 50なら毎回+50）")]
    public float costAdd = 0f;

    // 現在の状態
    private float _currentCost;
    private int _currentLevel = 0;
    private bool _isSoldOut = false;

    void Start()
    {
        // 初期化
        _currentCost = baseCost;

        if (buyButton == null)
            buyButton = GetComponent<Button>();

        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuyClicked);

        UpdateUI();
    }

    void Update()
    {
        // 売り切れなら常に押せない状態にする
        if (_isSoldOut)
        {
            if (buyButton != null) buyButton.interactable = false;
            return;
        }

        // 現在の所持金が足りているかチェックして、足りなければボタンを灰色（押せない状態）にする
        if (buyButton != null && MoneyManager.Instance != null)
        {
            buyButton.interactable = MoneyManager.Instance.CurrentMoney >= _currentCost;
        }
    }

    private void OnBuyClicked()
    {
        if (MoneyManager.Instance == null)
        {
            Debug.LogWarning("MoneyManagerがシーンに存在しません！ お金が確認できないため処理を中断しました。");
            return;
        }

        if (_isSoldOut) return;

        // お金が足りているか確認
        if (MoneyManager.Instance.CurrentMoney >= _currentCost)
        {
            // お金を減らしてレベルを上げる
            MoneyManager.Instance.ReduceMoney(_currentCost);
            _currentLevel++;

            Debug.Log($"[{itemName}] をLv{_currentLevel}に強化しました！ (消費: {_currentCost} / 残り: {MoneyManager.Instance.CurrentMoney})");

            // ==========================================
            // TODO: ここに実際のキャッチャー交換・強化処理を書く
            // 例: UFOArmManager.Instance.SetArmLevel(_currentLevel);
            // ==========================================

            // 次のレベルの計算 または 売り切れ処理
            if (isOneTimePurchase || (maxLevel > 0 && _currentLevel >= maxLevel))
            {
                _isSoldOut = true;
            }
            else
            {
                // 次のコストを計算（倍率を掛けてから、固定値を足す）
                _currentCost = Mathf.Round(_currentCost * costMultiplier + costAdd);
            }

            // 表示を更新
            UpdateUI();
        }
        else
        {
            Debug.Log($"[{itemName}] の購入に失敗しました。お金が足りません。");
        }
    }

    /// <summary>
    /// UIのテキスト（値段やレベル）を最新の状態に更新する
    /// </summary>
    private void UpdateUI()
    {
        if (priceText != null)
        {
            priceText.text = _isSoldOut ? "SOLD OUT" : $"${_currentCost}";
        }

        if (levelText != null)
        {
            levelText.text = _isSoldOut ? "MAX" : $"Lv {_currentLevel}";
        }
    }
}
