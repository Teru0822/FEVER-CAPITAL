using UnityEngine;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 筒（ゴール）に配置し、鉄球が中に入ったことを検知するスクリプト。
    /// OnGoalReached() を FallBallGameManager に伝達します。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FallBallGoal : MonoBehaviour
    {
        [Tooltip("このゴールが通知を送る対象の GameManager")]
        [SerializeField] private FallBallGameManager gameManager;
        
        [Tooltip("鉄球オブジェクトに付けられているタグ")]
        [SerializeField] private string ballTag = "Ball";

        private void OnTriggerEnter(Collider other)
        {
            // ぶつかってきた（中に入った）オブジェクトのタグが鉄球のものだったら
            if (other.CompareTag(ballTag))
            {
                if (gameManager != null)
                {
                    // GameManagerの成功処理を呼び出す
                    gameManager.OnGoalReached();
                }
                else
                {
                    Debug.LogWarning("FallBallGoal: GameManager がアタッチされていません！");
                }
            }
        }
    }
}
