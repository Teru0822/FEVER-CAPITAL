using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject coinPrefab;
    public GameObject watchPrefab;

    [Header("Settings")]
    public int totalCoins = 1000;
    public int watchInterval = 100;
    public float spawnArea = 5.0f;

    [Header("Hierarchy Folder")]
    // 新しく追加：ここに空のオブジェクト（フォルダ）を登録します
    public Transform parentFolder; 

    void Start()
    {
        SpawnItems();
    }

    void SpawnItems()
    {
        for (int i = 1; i <= totalCoins; i++)
        {
            Vector3 randomPos = transform.position + Random.insideUnitSphere * spawnArea;
            Quaternion randomRot = Random.rotation;

            // 変更：第4引数に parentFolder を追加すると、自動でその中に入ります！
            Instantiate(coinPrefab, randomPos, randomRot, parentFolder);

            if (i % watchInterval == 0)
            {
                Vector3 watchPos = randomPos + Vector3.up * 1.0f;
                // 時計も同じようにフォルダに入れます
                Instantiate(watchPrefab, watchPos, randomRot, parentFolder);
            }
        }
    }
}