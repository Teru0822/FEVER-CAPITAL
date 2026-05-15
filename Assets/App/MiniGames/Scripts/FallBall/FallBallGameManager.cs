using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [Tooltip("落下させる鉄球のGameObject（プレハブまたはシーン内の元オブジェクト）")]
        [SerializeField] private GameObject ballObject;
        [Tooltip("ボール補充アニメーションのコントローラー")]
        [SerializeField] private FallBallRefillController refillController;

        [Header("Debug & Test")]
        [Tooltip("テスト用に、ボールが落ちてもゲームを終了せず操作を続けられるようにする")]
        [SerializeField] private bool allowContinuousPlay = true;
        
        private Rigidbody ballRigidbody;
        private int currentBet;
        private bool isFinished = false;

        private GameObject ballTemplate;
        private Vector3 initialBallPosition;
        private Quaternion initialBallRotation;

        private void Start()
        {
            Debug.Log($"FallBallGameManager Start: ballObject={ballObject != null}, refillController={refillController != null}");
            
            if (ballObject != null)
            {
                ballTemplate = ballObject;
                initialBallPosition = ballTemplate.transform.position;
                initialBallRotation = ballTemplate.transform.rotation;
                
                if (refillController != null)
                {
                    Debug.Log("FallBallGameManager: 起動時の自動補充を開始します。");
                    SpawnNewBall();
                }
                else
                {
                    ballTemplate.SetActive(false);
                    SpawnNewBall();
                }
            }
            else
            {
                Debug.LogWarning("FallBallGameManager: ballObject が設定されていません！");
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Debug.Log($"FallBallGameManager: Spaceキー検知! refillController={refillController != null}, IsRefilling={refillController?.IsRefilling}");
                
                // 補充アニメーション中は追加スポーンを無効化
                if (refillController != null && refillController.IsRefilling) return;
                
                SpawnNewBall();
            }
        }

        private void SpawnNewBall()
        {
            // RefillController が設定されている場合はアニメーション付きで補充
            // (RefillController は自身の ballTemplate を持つので GameManager の ballTemplate は不要)
            if (refillController != null)
            {
                StartCoroutine(refillController.PlayRefillSequence());
                Debug.Log("FallBall: 補充アニメーションを開始しました");
                return;
            }
            
            if (ballTemplate == null) return;
            
            // RefillController が未設定の場合は従来のシンプルなスポーン
            GameObject newBall = Instantiate(ballTemplate, initialBallPosition, initialBallRotation);
            newBall.SetActive(true);
            
            Rigidbody rb = newBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }
            Debug.Log("FallBall: Spaceキーで新しいボールを出しました！");
        }

        public void Initialize(int betAmount)
        {
            currentBet = betAmount;
            isFinished = false;
            
            if (ballObject != null)
            {
                ballRigidbody = ballObject.GetComponent<Rigidbody>();
            }
            
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
            if (isFinished && !allowContinuousPlay) return;
            isFinished = true;
            
            if (!allowContinuousPlay && barController != null) 
            {
                barController.SetActive(false);
            }
            
            Debug.Log($"FallBall: Goal Reached! Success. Won: {currentBet * successMultiplier}");
            OnGameCompleted?.Invoke(true, successMultiplier);
        }

        /// <summary>
        /// ボールが場外に落ちた（失敗）ときに呼ばれる。
        /// </summary>
        public void OnOutZoneReached()
        {
            // 自動再生成を有効にするため、ここでは単にログを出して再生成ルーチンを呼ぶ
            Debug.Log("FallBall: ボールが場外に落ちました");
            OnBallExit();
        }

        /// <summary>
        /// ボールがシーンから消えた（アウトまたはゴール）際に、次のボールを出すための通知。
        /// </summary>
        public void OnBallExit()
        {
            if (isFinished && !allowContinuousPlay) return;
            
            Debug.Log("FallBall: ボール退出検知。1秒後に再出現させます。");
            StartCoroutine(WaitAndSpawnBall());
        }

        private System.Collections.IEnumerator WaitAndSpawnBall()
        {
            yield return new WaitForSeconds(1.0f);
            SpawnNewBall();
        }
    }
}
