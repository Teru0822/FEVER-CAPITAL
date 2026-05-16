using UnityEngine;

/// <summary>
/// ボール側の素材別オーディオコントローラ。
///   - 衝突瞬間: SurfaceTag を持つ相手と当たったら、面プロファイル + ボール素材から
///     impact one-shot を再生 (PinballSplitFXManager のプールを共有利用)
///   - 接触中:   1 本のループ AudioSource で転がり音を鳴らす。床素材が変わったら
///     2 ソース間でクロスフェード (ブツッ音回避)
///   - 接触切れ: 一定時間で転がり音をフェードアウト
///
/// 法線方向相対速度で衝突強度を計算するので、壁スレや微振動では発火しない。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BallSurfaceAudio : MonoBehaviour
{
    [Header("ボール素材")]
    [Tooltip("このボールの素材プロファイル。SurfaceProfile の clip 配列インデックスを決める")]
    public BallMaterialProfile ballMaterial;

    [Header("転がり音 (ループ)")]
    [Range(0f, 0.5f)]
    [Tooltip("床素材切り替わり時のクロスフェード長 (秒)")]
    public float crossfadeTime = 0.08f;

    [Range(0f, 0.5f)]
    [Tooltip("接触が途切れてから転がり音を消すまでの猶予 (秒)。物理ステップで1Fだけ離れる事故をマスクする")]
    public float airExitTime = 0.1f;

    [Header("衝突音")]
    [Min(0f)]
    [Tooltip("衝突 SFX のクールダウン (秒)。物理ジッタによる連打を防ぐ")]
    public float impactCooldown = 0.05f;

    [Tooltip("法線が真上に近い (床) 接触のみ転がり音判定対象とする。値は cos(angle)。0.3 ≒ ±72°")]
    [Range(-1f, 1f)]
    public float rollNormalDotMin = 0.3f;

    Rigidbody _rb;
    AudioSource _rollA;
    AudioSource _rollB;
    bool _useA = true;       // 今アクティブなのが _rollA か _rollB か
    SurfaceProfile _currentRollProfile;
    float _lastImpactTime = -1f;
    float _lastContactTime = -1f;

    int BallIndex => ballMaterial != null ? ballMaterial.index : 0;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rollA = CreateRollSource("[RollAudio_A]");
        _rollB = CreateRollSource("[RollAudio_B]");
    }

    AudioSource CreateRollSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;
        s.volume = 0f;
        return s;
    }

    void OnCollisionEnter(Collision c)
    {
        if (Time.time - _lastImpactTime < impactCooldown) return;
        var profile = ResolveImpactSurface(c, out float normalSpeed);
        if (profile == null) return;
        if (normalSpeed < profile.impactMinSpeed) return;

        var clip = profile.GetImpactClip(BallIndex);
        if (clip == null) return;

        float vol = profile.impactVolumeMax * Mathf.Clamp01(profile.impactVolumeByNormalSpeed.Evaluate(normalSpeed));
        if (vol < 0.005f) return;

        Vector3 hitPos = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        var mgr = PinballSplitFXManager.Instance;
        if (mgr != null)
        {
            mgr.PlayPooledOneShot(hitPos, clip, vol, profile.impactPitchVariance);
        }
        _lastImpactTime = Time.time;
    }

    void OnCollisionStay(Collision c)
    {
        var profile = ResolveRollSurface(c);
        if (profile == null) return;
        _lastContactTime = Time.time;
        UpdateRolling(profile);
    }

    void Update()
    {
        // 床から離れた / 素材未設定の面しかない時に転がり音をフェードアウト
        if (Time.time - _lastContactTime > airExitTime && _currentRollProfile != null)
        {
            StopRolling();
        }
        // クロスフェード中の非アクティブソースを徐々に絞る
        var inactive = _useA ? _rollB : _rollA;
        if (inactive.volume > 0f && inactive.isPlaying)
        {
            inactive.volume = Mathf.MoveTowards(inactive.volume, 0f, Time.deltaTime / Mathf.Max(0.001f, crossfadeTime));
            if (inactive.volume <= 0.0001f) inactive.Stop();
        }
    }

    void UpdateRolling(SurfaceProfile profile)
    {
        var clip = profile.GetRollLoop(BallIndex);
        if (clip == null)
        {
            StopRolling();
            return;
        }

        if (_currentRollProfile != profile)
        {
            // 素材切り替え: アクティブソースを反転 + クリップ差し替え
            _useA = !_useA;
            var active = _useA ? _rollA : _rollB;
            active.clip = clip;
            active.volume = 0f;
            active.pitch = 1f;
            if (!active.isPlaying) active.Play();
            _currentRollProfile = profile;
        }

        var src = _useA ? _rollA : _rollB;
        float speed = _rb.linearVelocity.magnitude;
        float targetVol = profile.rollVolumeMax * Mathf.Clamp01(profile.rollVolumeBySpeed.Evaluate(speed));
        src.volume = Mathf.MoveTowards(src.volume, targetVol, Time.deltaTime / Mathf.Max(0.001f, crossfadeTime));
        src.pitch = profile.rollPitchBySpeed.Evaluate(speed);
    }

    void StopRolling()
    {
        _currentRollProfile = null;
        // アクティブソースもフェードで切る (Update で進む)
        var active = _useA ? _rollA : _rollB;
        active.volume = Mathf.MoveTowards(active.volume, 0f, Time.deltaTime / Mathf.Max(0.001f, crossfadeTime));
        if (active.volume <= 0.0001f) active.Stop();
    }

    /// <summary>
    /// 衝突時のサーフェス解決: 一番衝突インパルスが強い接触点の SurfaceTag を採用。
    /// 法線方向相対速度も同時に計算して返す。
    /// </summary>
    SurfaceProfile ResolveImpactSurface(Collision c, out float normalSpeed)
    {
        normalSpeed = 0f;
        SurfaceProfile best = null;
        float bestScore = -1f;
        for (int i = 0; i < c.contactCount; i++)
        {
            var p = c.GetContact(i);
            var tag = p.otherCollider != null ? p.otherCollider.GetComponentInParent<SurfaceTag>() : null;
            if (tag == null || tag.profile == null) continue;
            float vn = Mathf.Abs(Vector3.Dot(c.relativeVelocity, p.normal));
            if (vn > bestScore)
            {
                bestScore = vn;
                best = tag.profile;
                normalSpeed = vn;
            }
        }
        return best;
    }

    /// <summary>
    /// 接触中のサーフェス解決: 法線が上向き寄り + 一番押し合っている接触点の SurfaceTag を採用。
    /// (壁スレでの誤発火を防ぐ)
    /// </summary>
    SurfaceProfile ResolveRollSurface(Collision c)
    {
        SurfaceProfile best = null;
        float bestImpulse = -1f;
        for (int i = 0; i < c.contactCount; i++)
        {
            var p = c.GetContact(i);
            if (Vector3.Dot(p.normal, Vector3.up) < rollNormalDotMin) continue;
            var tag = p.otherCollider != null ? p.otherCollider.GetComponentInParent<SurfaceTag>() : null;
            if (tag == null || tag.profile == null) continue;
            float imp = c.impulse.magnitude;
            if (imp > bestImpulse)
            {
                bestImpulse = imp;
                best = tag.profile;
            }
        }
        return best;
    }
}
