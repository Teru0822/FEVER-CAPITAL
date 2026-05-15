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
    /// ボールが壁/床/消費済ピン等に衝突した時に呼ぶ。衝突速度に応じて音量が変わる。
    /// 一定速度未満は無音。フレーム上限内のみ発火。
    /// </summary>
    public void OnImpact(Vector3 worldPos, float impactSpeed)
    {
        if (_config == null) return;
        var clip = _config.wallImpactSfxClip;
        if (clip == null) return;
        if (_sourcePool == null || _sourcePool.Length == 0) return;
        if (impactSpeed < _config.wallImpactMinSpeed) return;
        if (_config.maxImpactSfxPerFrame > 0 && _impactPlayedThisFrame >= _config.maxImpactSfxPerFrame) return;
        _impactPlayedThisFrame++;

        var src = _sourcePool[_nextSourceIdx];
        _nextSourceIdx = (_nextSourceIdx + 1) % _sourcePool.Length;

        float v = _config.wallImpactPitchVariance;
        src.pitch = (v > 0f) ? 1f + Random.Range(-v, v) : 1f;
        src.transform.position = worldPos;

        // 速度に応じた音量 (referenceSpeed で max)
        float refSpeed = Mathf.Max(0.01f, _config.wallImpactReferenceSpeed);
        float volScale = Mathf.Clamp01(impactSpeed / refSpeed);
        src.PlayOneShot(clip, _config.wallImpactSfxVolume * volScale);
    }
}
