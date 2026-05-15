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

            // 指定のタグか、あるいはRigidbodyを持っている（ボールである可能性が高い）場合
            if (other.CompareTag(ballTag) || other.GetComponent<Rigidbody>() != null)
            {
                if (gameManager != null)
                {
                    Debug.Log("FallBallOutZone: GameManagerにボール退出を通知します");
                    // OnOutZoneReached ではなく OnBallExit を呼ぶ（GameManager側の実装に合わせる）
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
