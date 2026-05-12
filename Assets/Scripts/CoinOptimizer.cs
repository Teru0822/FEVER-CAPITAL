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
        // 最初のランダム待機
        yield return new WaitForSeconds(delay);
        
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
    public void WakeUp()
    {
        if (rb != null && rb.isKinematic)
        {
            rb.isKinematic = false;
            StartCoroutine(CheckAndFreezeRoutine(1.0f));
        }
    }
}