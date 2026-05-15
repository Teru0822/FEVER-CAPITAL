using System.Collections;
using UnityEngine;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 鉄球落としのボール補充アニメーションを管理するクラス。
    /// シェイプキーを使って昇降棒の伸縮とアームの開閉を制御し、
    /// ボールを棒の上に配置するシーケンスを実行します。
    /// </summary>
    public class FallBallRefillController : MonoBehaviour
    {
        [Header("シェイプキー対象")]
        [Tooltip("昇降棒（円柱）の SkinnedMeshRenderer")]
        [SerializeField] private SkinnedMeshRenderer rodRenderer;
        
        [Tooltip("補充アーム（立方体.001）の SkinnedMeshRenderer")]
        [SerializeField] private SkinnedMeshRenderer armRenderer;

        [Header("ボール設定")]
        [Tooltip("鉄球のテンプレート（球.002）。非表示にして保持します")]
        [SerializeField] private GameObject ballTemplate;
        
        [Tooltip("ボールの初期配置先（アームの Transform）")]
        [SerializeField] private Transform ballSpawnParent;

        [Header("タイミング設定")]
        [Tooltip("昇降棒が伸びる時間（秒）")]
        [SerializeField] private float extendDuration = 1.0f;
        
        [Tooltip("アームが開く時間（秒）")]
        [SerializeField] private float openDuration = 0.5f;
        
        [Tooltip("アームを閉じつつ昇降棒を縮める時間（秒）")]
        [SerializeField] private float retractDuration = 1.0f;
        
        [Tooltip("アームが開いてからボールを離すまでの待機時間（秒）")]
        [SerializeField] private float dropDelay = 0.2f;

        /// <summary>
        /// 補充シーケンスが実行中かどうか。
        /// true の間はスペースキー等による追加スポーンを無効化するために使用。
        /// </summary>
        public bool IsRefilling { get; private set; }

        // シェイプキーのインデックス（Start で自動検出）
        private int rodBlendShapeIndex = -1;
        private int armBlendShapeIndex = -1;

        private void Start()
        {
            // シェイプキー「キー１」のインデックスを自動取得
            if (rodRenderer != null)
            {
                rodBlendShapeIndex = FindBlendShapeIndex(rodRenderer, "キー１");
                if (rodBlendShapeIndex < 0)
                {
                    // 見つからない場合はインデックス0をフォールバックとして使用
                    Debug.LogWarning("FallBallRefill: 昇降棒のシェイプキー「キー１」が見つかりません。インデックス0を使用します。");
                    rodBlendShapeIndex = 0;
                }
            }

            if (armRenderer != null)
            {
                armBlendShapeIndex = FindBlendShapeIndex(armRenderer, "キー１");
                if (armBlendShapeIndex < 0)
                {
                    Debug.LogWarning("FallBallRefill: アームのシェイプキー「キー１」が見つかりません。インデックス0を使用します。");
                    armBlendShapeIndex = 0;
                }
            }

            // テンプレートを非表示にする
            if (ballTemplate != null)
            {
                ballTemplate.SetActive(false);
            }
        }

        /// <summary>
        /// 指定した名前のシェイプキーのインデックスを検索します。
        /// </summary>
        private int FindBlendShapeIndex(SkinnedMeshRenderer renderer, string shapeName)
        {
            if (renderer.sharedMesh == null) return -1;
            
            int count = renderer.sharedMesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                string name = renderer.sharedMesh.GetBlendShapeName(i);
                if (name == shapeName) return i;
            }
            
            // 完全一致が見つからない場合、部分一致で探す
            for (int i = 0; i < count; i++)
            {
                string name = renderer.sharedMesh.GetBlendShapeName(i);
                if (name.Contains(shapeName)) return i;
            }
            
            return -1;
        }

        /// <summary>
        /// ボール補充シーケンスを実行するコルーチン。
        /// 1. ボールをアーム内にスポーン
        /// 2. 昇降棒を伸ばす（アーム下降）
        /// 3. アームを開く（ボールを離す）
        /// 4. アームを閉じつつ昇降棒を縮める（元に戻る）
        /// </summary>
        public IEnumerator PlayRefillSequence()
        {
            if (IsRefilling) yield break;
            IsRefilling = true;

            // --- フェーズ 1: ボールをアーム内にスポーン ---
            GameObject newBall = null;
            if (ballTemplate != null && ballSpawnParent != null)
            {
                newBall = Instantiate(ballTemplate, ballSpawnParent);
                newBall.SetActive(true);
                newBall.transform.localPosition = Vector3.zero;
                
                // 物理演算を一時停止（アームに乗せたまま移動するため）
                Rigidbody rb = newBall.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }

            // --- フェーズ 2: 昇降棒を伸ばす（アーム下降） ---
            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 0f, 100f, extendDuration));

            // --- フェーズ 3: アームを開く ---
            yield return StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 0f, 100f, openDuration));

            // --- フェーズ 4: ボールを離す ---
            if (newBall != null)
            {
                // 少し待機してからボールを離す
                yield return new WaitForSeconds(dropDelay);
                
                // 親を解除して自由にする
                newBall.transform.SetParent(null);
                
                // 物理演算を有効化
                Rigidbody rb = newBall.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.linearVelocity = Vector3.zero;
                }
            }

            // --- フェーズ 5: アームを閉じつつ昇降棒を縮める（同時実行） ---
            StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 100f, 0f, retractDuration));
            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 100f, 0f, retractDuration));

            IsRefilling = false;
        }

        /// <summary>
        /// シェイプキーの値を from から to へ duration 秒かけて補間するコルーチン。
        /// </summary>
        private IEnumerator AnimateBlendShape(SkinnedMeshRenderer renderer, int index, float from, float to, float duration)
        {
            if (renderer == null || index < 0) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // SmoothStep で自然な加減速を付ける
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                float value = Mathf.Lerp(from, to, smoothT);
                renderer.SetBlendShapeWeight(index, value);
                yield return null;
            }
            
            // 最終値を確実にセット
            renderer.SetBlendShapeWeight(index, to);
        }
    }
}
