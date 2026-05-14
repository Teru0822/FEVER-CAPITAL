using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace Assets.App.Intercom.Scripts
{
    /// <summary>
    /// インターホンの操作シーケンスを管理するクラス (Issue #6)。
    /// </summary>
    public class IntercomController : MonoBehaviour
    {
        public enum IntercomState
        {
            Idle,       // 待機中
            Calling,    // 着信中（ランプ緑点滅、映像表示、音再生）
            Talking     // 通話中（ランプ緑点灯、映像表示、音停止）
        }

        [Header("Settings")]
        [SerializeField] private IntercomState currentState = IntercomState.Idle;
        [SerializeField] private Color lampColor = Color.green;
        [SerializeField] private float blinkSpeed = 2.0f;
        [SerializeField] private string emissionKeyword = "_EmissionColor";

        [Header("References")]
        [SerializeField] private GameObject displayObject;      // ディスプレイ（RawImageやRenderTextureが貼られた面）
        [SerializeField] private Renderer lampRenderer;        // 右上ランプのRenderer
        [SerializeField] private AudioSource callingSfx;       // 呼び出し音
        [SerializeField] private GameObject entranceCamera;   // 玄関カメラ（RenderTextureを書き込むカメラ）

        private Material lampMaterial;
        private Color defaultEmissionColor;

        private void Start()
        {
            if (lampRenderer != null)
            {
                lampMaterial = lampRenderer.material;
                defaultEmissionColor = Color.black; // 初期は消灯
                lampMaterial.SetColor(emissionKeyword, defaultEmissionColor);
            }

            // 初期状態は待機
            UpdateState(IntercomState.Idle);
        }

        private void Update()
        {
            // U キー入力を監視 (Idle時のみ着信)
            if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
            {
                if (currentState == IntercomState.Idle)
                {
                    UpdateState(IntercomState.Calling);
                }
            }

            // 着信中のランプ点滅制御
            if (currentState == IntercomState.Calling)
            {
                float lerp = (Mathf.Sin(Time.time * blinkSpeed * Mathf.PI) + 1.0f) / 2.0f;
                Color currentEmission = Color.Lerp(Color.black, lampColor, lerp);
                lampMaterial.SetColor(emissionKeyword, currentEmission);
            }
        }

        public void UpdateState(IntercomState newState)
        {
            currentState = newState;

            switch (currentState)
            {
                case IntercomState.Idle:
                    SetDisplayActive(false);
                    SetLampEmission(false);
                    if (callingSfx != null) callingSfx.Stop();
                    break;

                case IntercomState.Calling:
                    SetDisplayActive(true);
                    // 点滅はUpdateで行う
                    if (callingSfx != null && !callingSfx.isPlaying) callingSfx.Play();
                    break;

                case IntercomState.Talking:
                    SetDisplayActive(true);
                    SetLampEmission(true); // 点灯
                    if (callingSfx != null) callingSfx.Stop();
                    break;
            }
        }

        private void SetDisplayActive(bool active)
        {
            if (displayObject != null) displayObject.SetActive(active);
            if (entranceCamera != null) entranceCamera.SetActive(active);
        }

        private void SetLampEmission(bool active)
        {
            if (lampMaterial != null)
            {
                lampMaterial.SetColor(emissionKeyword, active ? lampColor : Color.black);
                if (active) lampMaterial.EnableKeyword("_EMISSION");
                else lampMaterial.DisableKeyword("_EMISSION");
            }
        }

        // --- ボタンから呼ばれる公開メソッド ---

        /// <summary>
        /// 下段中央ボタン：通話開始
        /// </summary>
        public void OnClickCentralButton()
        {
            if (currentState == IntercomState.Calling)
            {
                UpdateState(IntercomState.Talking);
                Debug.Log("Intercom: 通話開始");
            }
        }

        /// <summary>
        /// 下段右ボタン：通話終了
        /// </summary>
        public void OnClickRightButton()
        {
            if (currentState != IntercomState.Idle)
            {
                UpdateState(IntercomState.Idle);
                Debug.Log("Intercom: 通話終了");
            }
        }

        /// <summary>
        /// 下段左ボタン：受け渡し口開錠（将来対応用フック）
        /// </summary>
        public void OnClickLeftButton()
        {
            Debug.Log("Intercom: 受け渡し口の開錠（未実装・将来対応）");
        }
    }
}
