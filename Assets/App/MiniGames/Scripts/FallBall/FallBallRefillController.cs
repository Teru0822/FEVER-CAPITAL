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
            Debug.Log($"FallBallRefill Start: refillClip={refillClip != null}, animationTarget={animationTarget != null}, " +
                      $"rodRenderer={rodRenderer != null}, armRenderer={armRenderer != null}, " +
                      $"ballTemplate={ballTemplate != null}, ballSpawnParent={ballSpawnParent != null}");

            // シェイプキーモード用: インデックスを自動取得
            if (rodRenderer != null)
            {
                LogBlendShapeNames(rodRenderer, "昇降棒");
                rodBlendShapeIndex = FindBlendShapeIndex(rodRenderer, "キー 1");
                Debug.Log($"FallBallRefill: 昇降棒シェイプキー index={rodBlendShapeIndex}");
                if (rodBlendShapeIndex < 0) rodBlendShapeIndex = 0;
            }
            if (armRenderer != null)
            {
                LogBlendShapeNames(armRenderer, "アーム");
                armBlendShapeIndex = FindBlendShapeIndex(armRenderer, "キー 1");
                Debug.Log($"FallBallRefill: アームシェイプキー index={armBlendShapeIndex}");
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
                refillClip.legacy = true;
                legacyAnimation.AddClip(refillClip, refillClip.name);
                legacyAnimation.playAutomatically = false;
                Debug.Log($"FallBallRefill: アニメーションクリップ「{refillClip.name}」をセットアップ完了（長さ: {refillClip.length}秒）");
            }
            else
            {
                Debug.Log("FallBallRefill: アニメーションクリップ未設定。シェイプキーモードを使用します。");
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
        /// SkinnedMeshRenderer のシェイプキー名を全てログ出力します（デバッグ用）。
        /// </summary>
        private void LogBlendShapeNames(SkinnedMeshRenderer renderer, string label)
        {
            if (renderer == null || renderer.sharedMesh == null)
            {
                Debug.Log($"FallBallRefill: {label} - sharedMesh が null です");
                return;
            }
            int count = renderer.sharedMesh.blendShapeCount;
            if (count == 0)
            {
                Debug.Log($"FallBallRefill: {label} - シェイプキーがありません（count=0）");
                return;
            }
            for (int i = 0; i < count; i++)
            {
                string name = renderer.sharedMesh.GetBlendShapeName(i);
                Debug.Log($"FallBallRefill: {label} シェイプキー[{i}] = \"{name}\"");
            }
        }

        /// <summary>
        /// ボール補充シーケンスを実行するコルーチン。
        /// </summary>
        public IEnumerator PlayRefillSequence()
        {
            if (IsRefilling) yield break;
            IsRefilling = true;

            Debug.Log($"FallBallRefill: PlayRefillSequence 開始 (clipMode={refillClip != null && legacyAnimation != null})");

            if (refillClip != null && legacyAnimation != null)
            {
                yield return StartCoroutine(PlayClipRefill());
            }
            else
            {
                yield return StartCoroutine(PlayShapeKeyRefill());
            }

            IsRefilling = false;
            Debug.Log("FallBallRefill: PlayRefillSequence 完了");
        }

        /// <summary>
        /// AnimationClip を使った補充アニメーション。
        /// </summary>
        private IEnumerator PlayClipRefill()
        {
            Debug.Log($"FallBallRefill: 補充アニメーション「{refillClip.name}」を開始します");
            
            // 1. 最初からボールを生成（親子関係なし、物理有効）
            SpawnBallInArm();

            // 2. アニメーション再生
            legacyAnimation.Play(refillClip.name);
            
            // 3. アニメーション完了まで待機
            yield return new WaitForSeconds(refillClip.length);
            Debug.Log("FallBallRefill: 補充シーケンス完了");
        }

        /// <summary>
        /// シェイプキーを使った補充アニメーション。
        /// </summary>
        private IEnumerator PlayShapeKeyRefill()
        {
            Debug.Log("FallBallRefill: シェイプキーによる補充を開始します");
            
            SpawnBallInArm();

            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 0f, 100f, extendDuration));
            yield return StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 0f, 100f, openDuration));

            yield return new WaitForSeconds(dropDelay);
            
            StartCoroutine(AnimateBlendShape(armRenderer, armBlendShapeIndex, 100f, 0f, retractDuration));
            yield return StartCoroutine(AnimateBlendShape(rodRenderer, rodBlendShapeIndex, 100f, 0f, retractDuration));
        }

        private GameObject SpawnBallInArm()
        {
            if (ballTemplate == null || ballSpawnParent == null) return null;

            // 親を指定せず、現在のスポーン位置に生成
            GameObject newBall = Instantiate(ballTemplate, ballSpawnParent.position, ballSpawnParent.rotation);
            newBall.name = "RefilledBall_" + Time.frameCount;
            
            // 重要：アウト判定(OutZone)で検知されるようにタグを設定
            newBall.tag = "Ball";
            
            newBall.transform.localScale = ballTemplate.transform.localScale;
            newBall.SetActive(true);
            
            Rigidbody rb = newBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false; 
                rb.linearVelocity = Vector3.zero;
            }

            // 【改善】アームの動く部品（自分自身の子要素）とのみ衝突を無視する
            Collider ballCollider = newBall.GetComponent<Collider>();
            if (ballCollider != null)
            {
                // 自分（RefillController）の子要素にあるコライダーのみを対象にする
                // ただし、もしミニゲーム全体が自分自身の子なら、より慎重に「アーム」のルートを指定すべき
                Collider[] myColliders = GetComponentsInChildren<Collider>();
                foreach (var otherCollider in myColliders)
                {
                    // 自分自身やトリガーは除外、かつ「棒」や「ゴール」を無視しないように注意
                    // ここでは「アーム」に関連するものだけを無視するように、名前などでフィルタリングするか
                    // もしくは単純に「アーム」のルートトランスフォームを限定して取得する
                    if (otherCollider != ballCollider && !otherCollider.isTrigger)
                    {
                        // 念のため、他の重要なコライダー（ゴールなど）を無視しないように
                        if (otherCollider.gameObject.name.Contains("arm") || otherCollider.gameObject.name.Contains("rod"))
                        {
                            Physics.IgnoreCollision(ballCollider, otherCollider, true);
                            StartCoroutine(RestoreCollision(ballCollider, otherCollider, 1.0f));
                        }
                    }
                }
            }

            Debug.Log($"FallBallRefill: ボールを独立生成（タグ: {newBall.tag}）: {newBall.name}");
            return newBall;
        }

        private IEnumerator RestoreCollision(Collider c1, Collider c2, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (c1 != null && c2 != null)
            {
                Physics.IgnoreCollision(c1, c2, false);
            }
        }

        private void ReleaseBall(GameObject ball)
        {
            // 最初からはなしているので、ここでは何もしない
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
