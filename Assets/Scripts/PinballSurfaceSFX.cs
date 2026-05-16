using UnityEngine;

/// <summary>
/// 床/壁/障害物等の「面」に付ける SFX 設定。ボールがこの面に衝突したり転がったりした時に
/// それぞれ個別の音を鳴らす。木/金属/ガラス等、面ごとに音色を変えられる。
///
/// 使い方:
///   - 床/壁/オブジェクト等の GameObject (Collider 持ち) にアタッチ
///   - impactClip で衝突音、rollLoopClip で転がり音を設定
///   - PinballBallController が衝突時/接触中に検出して再生する
///
/// このコンポーネントが無いサーフェスは PinballBallConfig.wallImpactSfxClip
/// (グローバル既定音) で衝突音が鳴る。転がり音はこのコンポーネントが必要。
/// </summary>
public class PinballSurfaceSFX : MonoBehaviour
{
    [Header("衝突音 (1 発)")]
    [Tooltip("ボールがこの面に衝突した瞬間に鳴らす AudioClip。null なら衝突音なし (グローバル既定にフォールバック)")]
    public AudioClip impactClip;

    [Range(0f, 1f)]
    [Tooltip("基本音量 (実際は衝突速度に応じて 0~この値 に減衰)")]
    public float impactVolume = 0.5f;

    [Range(0f, 0.5f)]
    [Tooltip("ピッチ揺らぎ ±幅")]
    public float impactPitchVariance = 0.1f;

    [Min(0f)]
    [Tooltip("この相対速度未満の衝突は鳴らさない (微小衝突カット)")]
    public float impactMinSpeed = 0.5f;

    [Min(0.01f)]
    [Tooltip("この速度で音量最大に到達 (m/s)")]
    public float impactReferenceSpeed = 5f;

    [Header("転がり音 (ループ)")]
    [Tooltip("ボールがこの面に接触している間ループ再生する AudioClip。null なら転がり音なし")]
    public AudioClip rollLoopClip;

    [Range(0f, 1f)]
    [Tooltip("最大音量")]
    public float rollMaxVolume = 0.4f;

    [Min(0f)]
    [Tooltip("この速度未満では転がり音を鳴らさない")]
    public float rollMinSpeedForVolume = 0.2f;

    [Min(0.01f)]
    [Tooltip("この速度で最大音量に到達 (m/s)")]
    public float rollReferenceSpeed = 3f;

    [Range(0.5f, 2f)]
    [Tooltip("速度が低いとこのピッチ")]
    public float rollMinPitch = 0.8f;

    [Range(0.5f, 2f)]
    [Tooltip("速度が referenceSpeed 以上の時のピッチ")]
    public float rollMaxPitch = 1.2f;
}
