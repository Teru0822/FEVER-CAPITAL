using UnityEngine;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 場外（床など）に配置し、鉄球が落ちたこと（ゲームオーバー）を検知するスクリプト。
    /// OnOutZoneReached() を FallBallGameManager に伝達します。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FallBallOutZone : MonoBehaviour
    {
        [Tooltip("通知を送る対象の GameManager")]
        [SerializeField] private FallBallGameManager gameManager;
        
        [Tooltip("鉄球オブジェクトのタグ")]
        [SerializeField] private string ballTag = "Ball";

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"FallBallOutZone: オブジェクト検知: {other.name}, Tag={other.tag}");

            // 必ずタグが "Ball"（または指定の ballTag）であることを確認
            // Rigidbodyがあるだけで消すと、アームなどが触れた際に誤作動するため
            if (other.CompareTag(ballTag))
            {
                if (gameManager != null)
                {
                    Debug.Log($"FallBallOutZone: ボール {other.name} を検知。GameManagerに通知します");
                    gameManager.OnBallExit();
                }
                else
                {
                    Debug.LogWarning("FallBallOutZone: gameManager がアタッチされていません！");
                }

                // ボールを消去する
                Destroy(other.gameObject);
            }
        }
    }
}
