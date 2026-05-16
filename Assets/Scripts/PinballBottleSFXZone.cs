using UnityEngine;

/// <summary>
/// Trigger Collider に PinballBallController が侵入した時に効果音を鳴らすだけの単純ゾーン。
/// 瓶/穴/カップ等で「コロンコロン」と鳴らす用途。ボールの破壊や数え上げは行わない
/// (それは PinballBallSinkZone の役割)。
///
/// 使い方:
///   - 瓶等の GameObject に Trigger Collider を付け、本コンポーネントをアタッチ
///   - clip にコロンコロン音をアサイン
///   - PinballBallSinkZone と併用すれば「音 → 消滅 → カウント」が同時に発火する
/// </summary>
[RequireComponent(typeof(Collider))]
public class PinballBottleSFXZone : MonoBehaviour
{
    [Header("SFX")]
    [Tooltip("ボールが侵入した時に鳴らす AudioClip (例: コロンコロン)")]
    [SerializeField] private AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("音量")]
    [SerializeField] private float volume = 0.6f;

    [Range(0f, 0.5f)]
    [Tooltip("ピッチ揺らぎ ±幅 (連続発火時のバリエーション。コロンコロン感に有効)")]
    [SerializeField] private float pitchVariance = 0.2f;

    [Header("適用対象")]
    [Tooltip("対象タグ (空なら PinballBallController を持つ全衝突物)")]
    [SerializeField] private string targetTag = "";

    [Header("オプション")]
    [Tooltip("Awake 時に Collider.isTrigger を強制 ON")]
    [SerializeField] private bool forceTriggerOnAwake = true;

    private AudioSource _audioSource;

    void Awake()
    {
        if (forceTriggerOnAwake)
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger) col.isTrigger = true;
        }

        if (clip != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (clip == null || _audioSource == null) return;
        if (!string.IsNullOrEmpty(targetTag) && !other.gameObject.CompareTag(targetTag)) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        if (rb.GetComponent<PinballBallController>() == null) return;

        float v = pitchVariance;
        _audioSource.pitch = (v > 0f) ? 1f + Random.Range(-v, v) : 1f;
        _audioSource.PlayOneShot(clip, volume);
    }
}
