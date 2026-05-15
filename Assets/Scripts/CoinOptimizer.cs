using System.Collections;
using UnityEngine;

public class CoinOptimizer : MonoBehaviour
{
    private Rigidbody rb;
    private Collider _col;
    private float checkInterval = 1.0f;
    private float sleepVelocity = 0.01f;
    private float coinThickness = 0.05f;

    /// <summary>
    /// ItemSpawnerがスポーン完了時に設定する。
    /// この時刻を過ぎるまで、全コインは絶対に凍結チェックを開始しない。
    /// </summary>
    public static float freezeStartTime = float.MaxValue;

    // アームに叩き起こされている間は凍結を禁止するタイマー
    // WakeUp()が呼ばれるたびにリセットされ、2秒間はIsSleeping()でも凍結しない
    private float _keepAwakeTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        
        // 1万枚が同時に計算しないよう、最初の確認タイミングを0〜2秒の間でランダムにばらけさせる
        float randomStartDelay = Random.Range(0f, 2.0f);
        StartCoroutine(CheckAndFreezeRoutine(randomStartDelay));
    }

    void Update()
    {
        if (_keepAwakeTimer > 0f)
            _keepAwakeTimer -= Time.deltaTime;
    }

    private IEnumerator CheckAndFreezeRoutine(float delay)
    {
        // スポーン完了時刻（freezeStartTime）を過ぎるまで待機する
        // これにより「コインが降りかかっている途中」に空中で凍結するのを完全に防ぐ
        yield return new WaitUntil(() => Time.time >= freezeStartTime);

        // スポーン完了後にさら少し（1秒）待ってから開始（落下完了の余裕）
        yield return new WaitForSeconds(delay + 1.0f);
        
        // 毎回「new」するとゴミ（GC）が出るので、1秒待機する命令を使い回す（最適化の基本）
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        // Kinematicになるまで、1秒に1回だけループし続ける
        while (!rb.isKinematic)
        {
            // Unity物理エンジンが「静止した」と判断 かつ アームから離れて2秒経過した場合のみ凍結
            if (rb.IsSleeping() && _keepAwakeTimer <= 0f)
            {
                rb.isKinematic = true;
                yield break;
            }

            // 1秒休んでから再確認
            yield return wait;
        }
    }

    // アームが近づいた時に外部から叩き起こすための処理
    public void WakeUp(bool isChain = false)
    {
        // 叩き起こされたら2秒間は凍結しないようタイマーをリセット
        // アームが近くにある間はWakeUpNearbyCoinsから定期的に呼ばれ続けるため
        // アームから完全に離れるまで凍結しない
        _keepAwakeTimer = 2.0f;

        if (rb != null && rb.isKinematic)
        {
            rb.isKinematic = false;
            StartCoroutine(CheckAndFreezeRoutine(1.0f));

            // 初回の呼び出し時のみ、周囲のコインも連鎖的に起こす（下のコインが抜けて上のコインが空中に浮いたままになるのを防ぐ）
            if (!isChain)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, coinThickness * 4f);
                foreach (var hit in hits)
                {
                    CoinOptimizer other = hit.GetComponent<CoinOptimizer>();
                    if (other != null && other != this)
                    {
                        other.WakeUp(true); // 連鎖フラグを立てて呼ぶ（無限ループ防止）
                    }
                }
            }
        }
    }
}