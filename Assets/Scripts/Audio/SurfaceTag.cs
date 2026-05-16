using UnityEngine;

/// <summary>
/// 床/壁/障害物等の面オブジェクトにアタッチして「素材」を宣言するだけのコンポーネント。
/// 実音響データは SurfaceProfile (ScriptableObject) 側に集約されているので、
/// このコンポーネント自体には設定項目はほぼ無い (素材を入れ替える時は profile を差し替えるだけ)。
///
/// ボール側 (BallSurfaceAudio) は OnCollisionEnter / OnCollisionStay で
/// この SurfaceTag を GetComponentInParent で拾って素材を判定する。
/// </summary>
[DisallowMultipleComponent]
public class SurfaceTag : MonoBehaviour
{
    [Tooltip("この面の音響プロファイル (木/金属/ガラス等)。null ならこの面は無音扱い")]
    public SurfaceProfile profile;
}
