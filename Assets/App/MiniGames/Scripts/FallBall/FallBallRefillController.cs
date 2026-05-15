using System.Collections;
using UnityEngine;

namespace MiniGames.FallBall
{
    /// <summary>
    /// 鉄球落としのボール補充アニメーションを管理するクラス。
    /// 
    /// 2つのモードに対応:
    /// 1. アニメーションクリップモード: Blender の「Scene」等のクリップを直接再生
    /// 2. シェイプキーモード: スクリプトからシェイプキーを制御
    /// 
    /// AnimationClip が設定されていればそちらを優先し、なければシェイプキーモードを使用します。
    /// </summary>
    public class FallBallRefillController : MonoBehaviour
    {
        [Header("アニメーションクリップモード")]
        [Tooltip("補充アニメーションのクリップ（Blender の Scene 等）。Projectウィンドウからドラッグしてください")]
        [SerializeField] private AnimationClip refillClip;
        [Tooltip("アニメーションを再生する対象のGameObject（fallball改のルートなど）")]
        [SerializeField] private GameObject animationTarget;

        [Header("シェイプキー対象（アニメーションクリップ未使用時）")]
        [Tooltip("昇降棒（円柱）の SkinnedMeshRenderer")]
        [SerializeField] private SkinnedMeshRenderer rodRenderer;
        
        [Tooltip("補充アーム（立方体.001）の SkinnedMeshRenderer")]
        [SerializeField] private SkinnedMeshRenderer armRenderer;

        [Header("ボール設定")]
        [Tooltip("鉄球のテンプレート（球.002）")]
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
        /// </summary>
        public bool IsRefilling { get; private set; }

        // シェイプキーのインデックス
        private int rodBlendShapeIndex = -1;
        private int armBlendShapeIndex = -1;
        
        // アニメーションクリップ再生用
        private Animation legacyAnimation;

        private void Start()
        {
            // シェイプキーモード用: インデックスを自動取得
            if (rodRenderer != null)
            {
                rodBlendShapeIndex = FindBlendShapeIndex(rodRenderer, "キー１");
                if (rodBlendShapeIndex < 0) rodBlendShapeIndex = 0;
            }
            if (armRenderer != null)
            {
                armBlendShapeIndex = FindBlendShapeIndex(armRenderer, "キー１");
                if (armBlendShapeIndex < 0) armBlendShapeIndex = 0;
            }

            // アニメーションクリップモード用: legacy Animation コンポーネントを準備
            if (refillClip != null)
            {
                GameObject target = animationTarget != null ? animationTarget : gameObject;
                legacyAnimation = target.GetComponent<Animation>();
                if (legacyAnimation == null)
                {
                    legacyAnimation = target.AddComponent<Animation>();
                }
                // クリップを Legacy モードに設定して追加
                refillClip.legacy = true;
                legacyAnimation.AddClip(refillClip, refillClip.name);
                legacyAnimation.playAutomatically = false;
            }
        }

        private int FindBlendShapeIndex(SkinnedMeshRenderer renderer, string shapeName)
        {
            if (renderer == null || renderer.sharedMesh == null) return -1;
            int count = renderer.sharedMesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                if (renderer.sharedMesh.GetBlendShapeName(i) == shapeName) return i;
            }
            for (int i = 0; i < count; i++)
            {
                if (renderer.sharedMesh.GetBlendShapeName(i).Contains(shapeName)) return i;
            }
            return -1;
        }

        /// <summary>
        /// ボール補充シーケンスを実行するコルーチン。
        /// </summary>
        public IEnumerator PlayRefillSequence()
        {
            if (IsRefilling) yield break;
            IsRefilling = true;

            if (refillClip != null && legacyAnimation != null)
            {
                yield return StartCoroutine(PlayClipRefill());
            }
            else
            {
                yield return StartCoroutine(PlayShapeKeyRefill());
            }

            IsRefilling = false;
        }

        /// <summary>
        /// AnimationClip を使った補充アニメーション。
        /// </summary>
        private IEnumerator PlayClipRefill()
        {
            // ボールをアーム内にスポーン
            GameObject newBall = SpawnBallInArm();

            // アニメーション再生
            legacyAnimation.Play(refillClip.name);

            // アニメーション完了を待つ
            yield return new WaitForSeconds(refillClip.length);

            // ボールを離す
            ReleaseBall(newBall);
        }

        /// <summary>
        /// シェイプキーを使った補充アニメーション。
        /// </summary>
        private IEnumerator PlayShapeKeyRefill()
        {
            GameObject newBall = SpawnBallInArm();

            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 0f, 100f, extendDuration));
            yield return StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 0f, 100f, openDuration));

            yield return new WaitForSeconds(dropDelay);
            ReleaseBall(newBall);

            StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 100f, 0f, retractDuration));
            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 100f, 0f, retractDuration));
        }

        private GameObject SpawnBallInArm()
        {
            if (ballTemplate == null || ballSpawnParent == null) return null;

            if (ballTemplate.activeSelf)
            {
                ballTemplate.SetActive(false);
            }

            GameObject newBall = Instantiate(ballTemplate, ballSpawnParent);
            newBall.SetActive(true);
            newBall.transform.localPosition = Vector3.zero;
            
            Rigidbody rb = newBall.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            return newBall;
        }

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

        private IEnumerator AnimateBlendShape(SkinnedMeshRenderer renderer, int index, float from, float to, float duration)
        {
            if (renderer == null || index < 0) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                renderer.SetBlendShapeWeight(index, Mathf.Lerp(from, to, smoothT));
                yield return null;
            }
            renderer.SetBlendShapeWeight(index, to);
        }
    }
}
