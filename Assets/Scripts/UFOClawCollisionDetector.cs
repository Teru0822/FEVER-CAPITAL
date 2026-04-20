using UnityEngine;

/// <summary>
/// アーム（爪）が何かにぶつかったことを検知し、UFOArmControllerに「降下ストップ」を知らせるスクリプト
/// 判定を持たせたい爪先（001〜004）や爪の土台（finger）にアタッチして使います。
/// </summary>
public class UFOClawCollisionDetector : MonoBehaviour
{
    [Tooltip("司令塔であるUFOArmController (3番) をセットしてください")]
    public UFOArmController armController;

    private void Start()
    {
        // もしセットされていなければ、自動的に探す
        if (armController == null)
        {
            armController = FindAnyObjectByType<UFOArmController>();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // ぶつかった時にコントローラーへ通知する
        if (armController != null)
        {
            armController.OnClawCollided();
        }
    }

    // IsTrigger にチェックを入れている場合（すり抜けながら検知したい場合）はこちらが呼ばれる
    private void OnTriggerEnter(Collider other)
    {
        if (armController != null)
        {
            armController.OnClawCollided();
        }
    }
}
