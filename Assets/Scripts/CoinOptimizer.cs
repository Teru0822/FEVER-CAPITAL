using System.Collections;
using UnityEngine;

public class CoinOptimizer : MonoBehaviour
{
    private Rigidbody rb;
    private float checkInterval = 1.0f; // 1秒ごとに確認
    private float sleepVelocity = 0.01f; // この速度以下なら「止まっている」と判定
    private float coinThickness = 0.05f; // コインの厚み（環境に合わせて調整）

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // 1万枚が同時に計算しないよう、最初の確認タイミングを0〜2秒の間でランダムにばらけさせる
        float randomStartDelay = Random.Range(0f, 2.0f);
        StartCoroutine(CheckAndFreezeRoutine(randomStartDelay));
    }

    private IEnumerator CheckAndFreezeRoutine(float delay)
    {
        // 最初のランダム待機（スポーン直後などに空中で凍結するのを防ぐため、最低でも1秒は確実に落下させる）
        yield return new WaitForSeconds(delay + 1.0f);
        
        // 毎回「new」するとゴミ（GC）が出るので、1秒待機する命令を使い回す（最適化の基本）
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        // Kinematicになるまで、1秒に1回だけループし続ける
        while (!rb.isKinematic)
        {
            // 速度がほぼゼロ（完全に止まった）か？
            if (rb.linearVelocity.sqrMagnitude < sleepVelocity)
            {
                // 止まっていれば無条件でKinematic（凍結）にして計算を止める！
                rb.isKinematic = true;
                
                // ループを抜けて、定期確認コルーチン自体を終了させる
                yield break; 
            }

            // 1秒休んでから再確認
            yield return wait;
        }
    }

    // アームが近づいた時に外部から叩き起こすための処理
    public void WakeUp(bool isChain = false)
    {
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