using System;
using UnityEngine;

/// <summary>
/// ゲーム内のお金を管理するシングルトンクラス
/// </summary>
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [Header("初期設定")]
    [SerializeField] private float _currentMoney = 10000;
    public float CurrentMoney => _currentMoney;


    [Header("ターン管理")]
    [SerializeField] private int _decreaseInterval = 1;
    [SerializeField] private float _exponentialRate = 1.5f;
    [SerializeField] private float _initialDecreaseAmount = 100f;

    private int _currentTurnCount = 0;
    private float _previousDecreaseAmount = 0;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初期化
        _previousDecreaseAmount = _initialDecreaseAmount;
    }

    /// <summary>
    /// お金を増加させる
    /// </summary>
    /// <param name="amount">基本額</param>
    /// <param name="multiplier">倍率（デフォルトは1.0）</param>
    public void AddMoney(float amount, float multiplier = 1.0f)
    {
        if (amount <= 0) return;

        float finalAmount = amount * multiplier;
        _currentMoney += finalAmount;
        Debug.Log($"お金が増加しました: +{finalAmount} (現在: {_currentMoney})");
    }

    /// <summary>
    /// お金を減少させる
    /// </summary>
    /// <param name="amount">基本額</param>
    /// <param name="multiplier">倍率（デフォルトは1.0）</param>
    public void ReduceMoney(float amount, float multiplier = 1.0f)
    {
        if (amount <= 0) return;

        float finalAmount = amount * multiplier;
        _currentMoney -= finalAmount;
        Debug.Log($"お金が減少しました: -{finalAmount} (現在: {_currentMoney})");

        CheckGameOver();
    }

    /// <summary>
    /// 規定のターン数に応じた減少処理を行うための関数
    /// </summary>
    public void AdvanceTurn()
    {
        _currentTurnCount++;

        // 指定ターンごとに減少処理を実行
        if (_currentTurnCount % _decreaseInterval == 0)
        {
            ApplyTurnDecrease();
        }
    }

    /// <summary>
    /// ターン経過によるお金の減少処理
    /// </summary>
    private void ApplyTurnDecrease()
    {
        ReduceMoney(_previousDecreaseAmount);

        // 次回の減少額を指数関数的に増加させて記憶
        _previousDecreaseAmount *= _exponentialRate;
    }

    /// <summary>
    /// 所持金が0以下になった場合にゲームオーバーを告知する
    /// </summary>
    private void CheckGameOver()
    {
        if (_currentMoney <= 0)
        {
            _currentMoney = 0;
            Debug.Log("所持金額が0になりました。ゲームオーバーです。");
            
            //TODO:ここに詳細なゲームオーバー時の処理を実装
        }
    }
}
