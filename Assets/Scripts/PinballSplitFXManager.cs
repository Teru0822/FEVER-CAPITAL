using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ピンボール分裂時の VFX (小さな火花) と SFX (シャン音) を統括するシングルトン。
/// PinballBallManager から OnSplit(worldPos) を呼ばれて発火する。
/// 大量分裂で同時発火しすぎないように 1 フレームあたりの最大数で間引く。
/// </summary>
public class PinballSplitFXManager : MonoBehaviour
{
    private static PinballSplitFXManager _instance;
    public static PinballSplitFXManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindFirstObjectByType<PinballSplitFXManager>();
            if (_instance == null)
            {
                var go = new GameObject("[PinballSplitFXManager]");
                _instance = go.AddComponent<PinballSplitFXManager>();
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceOnSceneLoad()
    {
        if (FindFirstObjectByType<PinballBallConfig>() != null || FindFirstObjectByType<PinballBallController>() != null)
        {
            _ = Instance;
        }
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<PinballBallConfig>() != null || FindFirstObjectByType<PinballBallController>() != null)
        {
            _ = Instance;
        }
    }

    private PinballBallConfig _config;
    private AudioSource[] _sourcePool;
    private int _nextSourceIdx = 0;

    private int _sfxPlayedThisFrame = 0;
    private int _sparksSpawnedThisFrame = 0;
    private int _impactPlayedThisFrame = 0;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void Start()
    {
        _config = FindFirstObjectByType<PinballBallConfig>();
        BuildSourcePool();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void BuildSourcePool()
    {
        if (_config == null) return;
        int size = Mathf.Max(1, _config.sfxPoolSize);
        _sourcePool = new AudioSource[size];
        for (int i = 0; i < size; i++)
        {
            var go = new GameObject($"[SfxSource_{i}]");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            // 2D 再生 (画面内ピンボール用、定位より粒立ちを優先)。3D 化したいなら 1f に。
            src.spatialBlend = 0f;
            _sourcePool[i] = src;
        }
    }

    void LateUpdate()
    {
        // 毎フレーム 0 リセット (per-frame cap 用)
        _sfxPlayedThisFrame = 0;
        _sparksSpawnedThisFrame = 0;
        _impactPlayedThisFrame = 0;
    }

    /// <summary>分裂時に呼ぶ。火花 VFX と SFX を発火 (フレーム上限内のみ)。</summary>
    public void OnSplit(Vector3 worldPos)
    {
        if (_config == null) return;
        TrySpawnSpark(worldPos);
        TryPlaySfx(worldPos);
    }

    void TrySpawnSpark(Vector3 worldPos)
    {
        var prefab = _config.sparkParticlePrefab;
        if (prefab == null) return;
        if (_config.maxSparksPerFrame > 0 && _sparksSpawnedThisFrame >= _config.maxSparksPerFrame) return;
        _sparksSpawnedThisFrame++;

        var ps = Instantiate(prefab, worldPos, prefab.transform.rotation);
        ps.Play();
        // 自動破棄: ParticleSystem の duration + max lifetime + 余裕でマーク
        var main = ps.main;
        float life = main.duration + main.startLifetime.constantMax + 0.5f;
        Destroy(ps.gameObject, life);
    }

    void TryPlaySfx(Vector3 worldPos)
    {
        var clip = _config.splitSfxClip;
        if (clip == null) return;
        if (_sourcePool == null || _sourcePool.Length == 0) return;
        if (_config.maxSfxPerFrame > 0 && _sfxPlayedThisFrame >= _config.maxSfxPerFrame) return;
        _sfxPlayedThisFrame++;

        var src = _sourcePool[_nextSourceIdx];
        _nextSourceIdx = (_nextSourceIdx + 1) % _sourcePool.Length;

        float v = _config.splitSfxPitchVariance;
        src.pitch = (v > 0f) ? 1f + Random.Range(-v, v) : 1f;
        src.transform.position = worldPos;
        src.PlayOneShot(clip, _config.splitSfxVolume);
    }

    /// <summary>
    /// AudioSource プール経由でワンショット再生する汎用 API。
    /// 衝突 SFX (BallSurfaceAudio 等) の共通入口。フレーム上限と速度スケールに対応。
    /// </summary>
    /// <param name="impactSpeed">速度に応じた音量スケール用 (0 以下なら速度スケール無効、フル音量)</param>
    /// <param name="referenceSpeed">この速度で音量最大 (impactSpeed > 0 の時のみ有効)</param>
    public void PlayPooledOneShot(Vector3 worldPos, AudioClip clip, float volume, float pitchVariance, float impactSpeed = -1f, float referenceSpeed = 1f)
    {
        if (clip == null) return;
        if (_sourcePool == null || _sourcePool.Length == 0) return;
        if (_config != null && _config.maxImpactSfxPerFrame > 0 && _impactPlayedThisFrame >= _config.maxImpactSfxPerFrame) return;
        _impactPlayedThisFrame++;

        var src = _sourcePool[_nextSourceIdx];
        _nextSourceIdx = (_nextSourceIdx + 1) % _sourcePool.Length;

        src.pitch = (pitchVariance > 0f) ? 1f + Random.Range(-pitchVariance, pitchVariance) : 1f;
        src.transform.position = worldPos;

        float volScale = 1f;
        if (impactSpeed > 0f)
        {
            volScale = Mathf.Clamp01(impactSpeed / Mathf.Max(0.01f, referenceSpeed));
        }
        src.PlayOneShot(clip, volume * volScale);
    }
}
