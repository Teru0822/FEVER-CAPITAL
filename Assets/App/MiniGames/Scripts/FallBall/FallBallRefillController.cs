using System.Collections;
using UnityEngine;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 鉄球落としのボール補充アニメーションを管理するクラス。
    /// 
    /// 2つのモードに対応:
    /// 1. Animator モード: Blender の「Scene」アニメーションクリップを再生して補充動作を行う
    /// 2. シェイプキーモード: スクリプトからシェイプキーを制御して補充動作を行う
    /// 
    /// Animator が設定されていればそちらを優先し、なければシェイプキーモードを使用します。
    /// </summary>
    public class FallBallRefillController : MonoBehaviour
    {
        [Header("モード選択")]
        [Tooltip("Animator による補充アニメーション（Blender の Scene クリップ等）。設定すると優先的に使用します")]
        [SerializeField] private Animator refillAnimator;
        [Tooltip("Animator で再生するステート名（デフォルト: Scene）")]
        [SerializeField] private string animationStateName = "Scene";

        [Header("シェイプキー対象（Animator 未使用時）")]
        [Tooltip("昇降棒（円柱）の SkinnedMeshRenderer")]
        [SerializeField] private SkinnedMeshRenderer rodRenderer;
        
        [Tooltip("補充アーム（立方体.001）の SkinnedMeshRenderer")]
        [SerializeField] private SkinnedMeshRenderer armRenderer;

        [Header("ボール設定")]
        [Tooltip("鉄球のテンプレート（球.002）。非表示にして保持します")]
        [SerializeField] private GameObject ballTemplate;
        
        [Tooltip("ボールの初期配置先（アームの Transform）")]
        [SerializeField] private Transform ballSpawnParent;

        [Header("タイミング設定（シェイプキーモード用）")]
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
            // シェイプキーモード用: インデックスを自動取得
            if (rodRenderer != null)
            {
                rodBlendShapeIndex = FindBlendShapeIndex(rodRenderer, "キー１");
                if (rodBlendShapeIndex < 0)
                {
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
        /// Animator が設定されていればアニメーションクリップで実行し、
        /// なければシェイプキーで実行します。
        /// </summary>
        public IEnumerator PlayRefillSequence()
        {
            if (IsRefilling) yield break;
            IsRefilling = true;

            if (refillAnimator != null)
            {
                // --- Animator モード ---
                yield return StartCoroutine(PlayAnimatorRefill());
            }
            else
            {
                // --- シェイプキーモード ---
                yield return StartCoroutine(PlayShapeKeyRefill());
            }

            IsRefilling = false;
        }

        /// <summary>
        /// Animator を使った補充アニメーション。
        /// Blender の「Scene」クリップを再生し、完了を待ちます。
        /// </summary>
        private IEnumerator PlayAnimatorRefill()
        {
            // ボールをアーム内にスポーン
            GameObject newBall = SpawnBallInArm();

            // アニメーション再生
            refillAnimator.Play(animationStateName, 0, 0f);
            
            // アニメーションの開始を1フレーム待つ
            yield return null;

            // アニメーションの完了を待つ
            AnimatorStateInfo stateInfo = refillAnimator.GetCurrentAnimatorStateInfo(0);
            while (stateInfo.normalizedTime < 1.0f)
            {
                stateInfo = refillAnimator.GetCurrentAnimatorStateInfo(0);
                yield return null;
            }

            // ボールを離す
            ReleaseBall(newBall);
        }

        /// <summary>
        /// シェイプキーを使った補充アニメーション。
        /// </summary>
        private IEnumerator PlayShapeKeyRefill()
        {
            // ボールをアーム内にスポーン
            GameObject newBall = SpawnBallInArm();

            // フェーズ 1: 昇降棒を伸ばす（アーム下降）
            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 0f, 100f, extendDuration));

            // フェーズ 2: アームを開く
            yield return StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 0f, 100f, openDuration));

            // フェーズ 3: ボールを離す
            yield return new WaitForSeconds(dropDelay);
            ReleaseBall(newBall);

            // フェーズ 4: アームを閉じつつ昇降棒を縮める（同時実行）
            StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 100f, 0f, retractDuration));
            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 100f, 0f, retractDuration));
        }

        /// <summary>
        /// ボールをアーム内にスポーンします。
        /// </summary>
        private GameObject SpawnBallInArm()
        {
            if (ballTemplate == null || ballSpawnParent == null) return null;

            // テンプレートが表示されている場合は非表示にする（初回のみ）
            if (ballTemplate.activeSelf)
            {
                ballTemplate.SetActive(false);
            }

            GameObject newBall = Instantiate(ballTemplate, ballSpawnParent);
            newBall.SetActive(true);
            newBall.transform.localPosition = Vector3.zero;
            
            // 物理演算を一時停止（アームに乗せたまま移動するため）
            Rigidbody rb = newBall.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            return newBall;
        }

        /// <summary>
        /// ボールをアームから離して自由落下させます。
        /// </summary>
        private void ReleaseBall(GameObject ball)
        {
            if (ball == null) return;

            ball.transform.SetParent(null);
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }
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
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                float value = Mathf.Lerp(from, to, smoothT);
                renderer.SetBlendShapeWeight(index, value);
                yield return null;
            }
            
            renderer.SetBlendShapeWeight(index, to);
        }
    }
}
