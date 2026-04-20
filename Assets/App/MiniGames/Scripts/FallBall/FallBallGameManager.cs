using System;
using UnityEngine;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 鉄球落とし（FALLBALL）ミニゲームの進行管理と判定を行うクラス。
    /// IMiniGame インターフェースを実装。
    /// </summary>
    public class FallBallGameManager : MonoBehaviour, IMiniGame
    {
        public event Action<bool, float> OnGameCompleted;

        [Header("Settings")]
        [Tooltip("成功時の獲得倍率")]
        [SerializeField] private float successMultiplier = 2.0f;
        
        [Header("References")]
        [Tooltip("棒の操作コントローラー")]
        [SerializeField] private BarController barController;
        [Tooltip("落下させる鉄球のRigidbody")]
        [SerializeField] private Rigidbody ballRigidbody;
        
        private int currentBet;
        private bool isFinished = false;

        public void Initialize(int betAmount)
        {
            currentBet = betAmount;
            isFinished = false;
            
            // 操作を一旦無効化して物理挙動も止めておく
            if (barController != null) barController.SetActive(false);
            if (ballRigidbody != null)
            {
                ballRigidbody.isKinematic = true;
                ballRigidbody.linearVelocity = Vector3.zero;
            }
            
            Debug.Log($"FallBall Initialized. Bet: {betAmount}");
        }

        public void StartGame()
        {
            // ゲーム開始：操作と物理挙動を有効化
            if (barController != null) barController.SetActive(true);
            if (ballRigidbody != null) ballRigidbody.isKinematic = false;
            
            Debug.Log("FallBall Started!");
        }

        /// <summary>
        /// ボールが「筒（ゴール）」に触れたときに呼ばれる。
        /// ゴールとなるTriggerコライダーを持つオブジェクトのスクリプトから呼び出す想定。
        /// </summary>
        public void OnGoalReached()
        {
            if (isFinished) return;
            isFinished = true;
            
            if (barController != null) barController.SetActive(false);
            
            Debug.Log($"FallBall: Goal Reached! Success. Won: {currentBet * successMultiplier}");
            OnGameCompleted?.Invoke(true, successMultiplier);
        }

        /// <summary>
        /// ボールが場外に落ちた（失敗）ときに呼ばれる。
        /// 床などに配置したTriggerコライダーから呼び出す想定。
        /// </summary>
        public void OnOutZoneReached()
        {
            if (isFinished) return;
            isFinished = true;
            
            if (barController != null) barController.SetActive(false);
            
            Debug.Log("FallBall: Dropped outside! Failed.");
            OnGameCompleted?.Invoke(false, 0f);
        }
    }
}
