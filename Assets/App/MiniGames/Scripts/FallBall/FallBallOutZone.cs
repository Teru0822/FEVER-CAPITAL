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
        [SerializeField] private string ballTag = "Player";

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(ballTag))
            {
                if (gameManager != null)
                {
                    // GameManagerの失敗処理を呼び出す
                    gameManager.OnOutZoneReached();
                }

                // ボールを消去する
                Destroy(other.gameObject);
            }
        }
    }
}
