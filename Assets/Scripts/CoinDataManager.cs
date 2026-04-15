using System.Collections.Generic;
using System.IO;
using UnityEngine;

// JSONに保存するためのデータ構造
[System.Serializable]
public class CoinData
{
    public Vector3 position;
    public Quaternion rotation;
}

[System.Serializable]
public class CoinSaveData
{
    public List<CoinData> coins = new List<CoinData>();
}

public class CoinDataManager : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("生成するコインのプレハブ")]
    public GameObject coinPrefab;

    [Tooltip("現在エディタ上で1万枚のコインが入っている親オブジェクト")]
    public Transform editorCoinsFolder;

    [Tooltip("保存するファイル名")]
    public string saveFileName = "CoinPositions.json";

    [Tooltip("読み込む最大枚数（0 = 全件・上限なし）\n重いときはここを小さくしてください（例: 500）")]
    public int maxCoins = 0;

    // ▼ エディタの右クリックメニューから実行できる魔法の機能
    [ContextMenu("1. 現在の配置をJSONに保存する (Save)")]
    public void SaveCoinsToJson()
    {
        if (editorCoinsFolder == null)
        {
            Debug.LogError("Editor Coins Folder が設定されていません！");
            return;
        }

        CoinSaveData saveData = new CoinSaveData();

        // フォルダの中にあるすべてのコインの位置と角度をリストに記録
        foreach (Transform child in editorCoinsFolder)
        {
            CoinData data = new CoinData();
            data.position = child.position;
            data.rotation = child.rotation;
            saveData.coins.Add(data);
        }

        // JSONテキストに変換して Assets フォルダ直下に保存
        string json = JsonUtility.ToJson(saveData, true);
        string path = Path.Combine(Application.dataPath, saveFileName);
        File.WriteAllText(path, json);

        Debug.Log($"成功: {saveData.coins.Count}枚のコインデータを {path} に保存しました！\n※ヒエラルキーのコインを削除しても大丈夫です。");
    }

    void Start()
    {
        StartCoroutine(LoadAndSpawnCoinsRoutine());
    }

    private System.Collections.IEnumerator LoadAndSpawnCoinsRoutine()
    {
        string path = Path.Combine(Application.dataPath, saveFileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning("セーブデータが見つかりません。");
            yield break;
        }

        string json = File.ReadAllText(path);
        CoinSaveData loadData = JsonUtility.FromJson<CoinSaveData>(json);

        // 上限枚数を決定（maxCoins が 0 以下なら全件）
        int limit = (maxCoins > 0)
            ? Mathf.Min(maxCoins, loadData.coins.Count)
            : loadData.coins.Count;

        Transform runtimeFolder = new GameObject("RuntimeCoinsFolder").transform;

        int count = 0;
        int spawnPerFrame = 500;

        for (int i = 0; i < limit; i++)
        {
            CoinData data = loadData.coins[i];
            Instantiate(coinPrefab, data.position, data.rotation, runtimeFolder);
            count++;

            if (count % spawnPerFrame == 0)
                yield return null;
        }

        string limitStr = maxCoins > 0 ? maxCoins.ToString() : "なし";
        Debug.Log($"{count}枚のコイン生成が完了しました！（上限: {limitStr} / 全データ: {loadData.coins.Count}枚）");
    }
}