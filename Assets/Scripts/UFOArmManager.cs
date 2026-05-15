using UnityEngine;

/// <summary>
/// アームの強化（プレハブの切り替え）を管理するマネージャークラス。
/// UFOキャッチャー本体のオブジェクトや、空のオブジェクトにアタッチして使います。
/// </summary>
public class UFOArmManager : MonoBehaviour
{
    public static UFOArmManager Instance { get; private set; }

    [Tooltip("操作するUFOキャッチャー本体のController")]
    public UFOArmController ufoController;

    [Tooltip("レベル1（初期状態）〜最大レベルまでのアームの親オブジェクト（シーン上のもの）を順に登録します")]
    public GameObject[] armSetsByLevel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 指定されたレベルのアームに交換する
    /// </summary>
    /// <param name="level">現在のレベル（1〜）</param>
    public void SetArmLevel(int level)
    {
        if (ufoController == null)
        {
            Debug.LogWarning("UFOArmManager: ufoControllerが設定されていません！");
            return;
        }

        if (armSetsByLevel == null || armSetsByLevel.Length == 0)
        {
            Debug.LogWarning("UFOArmManager: アームのオブジェクトが登録されていません！");
            return;
        }

        // レベルは1始まりと想定し、配列インデックス（0〜）に変換。配列の最大数を超えないようにクリップする。
        int index = Mathf.Clamp(level - 1, 0, armSetsByLevel.Length - 1);

        // 【修正】現在稼働している古いアーム（Lv1など配列に入っていないもの含む）を確実に非表示にする
        if (ufoController.fingerParts != null && ufoController.fingerParts.Length > 0 && ufoController.fingerParts[0] != null)
        {
            ufoController.fingerParts[0].parent.gameObject.SetActive(false);
        }

        // 一旦配列内のすべてのアームを非表示にする
        for (int i = 0; i < armSetsByLevel.Length; i++)
        {
            if (armSetsByLevel[i] != null)
            {
                armSetsByLevel[i].SetActive(false);
            }
        }

        // 目標のアームだけを表示して適用する
        GameObject targetArm = armSetsByLevel[index];
        if (targetArm != null)
        {
            targetArm.SetActive(true);
            ufoController.ChangeClaw_InScene(targetArm);
            Debug.Log($"UFOArmManager: アームを Lv{level} (インデックス:{index}) に切り替えました！");
        }
    }
}
