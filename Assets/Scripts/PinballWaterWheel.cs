using UnityEngine;

/// <summary>
/// 水車型のベルトコンベア。複数の「皿」を回転軸まわりにぐるぐる回し、
/// 下から上に物体を運び上げる。皿は Kinematic Rigidbody として動くので
/// Unity 物理が自動的に玉を皿の上に載せて運搬する。
///
/// 形状:
///   - Circle:  純円形。皿は配置した初期位置を保ったまま回転する (従来挙動)
///   - Stadium: 真ん中だけ伸ばした円 (パターノスター形)。皿はパス上を等距離に
///              自動配置されてループする。stretchLength で伸ばす長さを指定
/// </summary>
public class PinballWaterWheel : MonoBehaviour
{
    public enum WheelShape
    {
        Circle,
        Stadium,
    }

    [Header("形状")]
    [Tooltip("Circle: 純円 (皿の初期位置保持)\nStadium: 真ん中だけ伸ばした円 (皿はパス上に自動均等配置)")]
    [SerializeField] private WheelShape shape = WheelShape.Circle;

    [Tooltip("Stadium 時の半径。Circle 時は皿の初期距離が半径")]
    [Min(0.01f)]
    [SerializeField] private float radius = 1f;

    [Tooltip("Stadium 時の真ん中ストレート部分の長さ (m)。0 で実質円形。+ で「真ん中だけ伸ばした」形に")]
    [Min(0f)]
    [SerializeField] private float stretchLength = 0f;

    [Tooltip("Stadium 時のストレート方向 (ローカル軸)。rotationAxis と直交させること。例: (0,1,0)")]
    [SerializeField] private Vector3 stretchAxis = Vector3.up;

    [Header("皿")]
    [Tooltip("皿が回る軌道の半径オフセット (radius からの増減)。0 で従来通り (= radius)。\nマイナスで皿だけ内側 (キャタピラに近づける)、プラスで皿だけ外側。\nキャタピラ軌道には影響しない (caterpillarRadiusOffset は radius を基準にする)")]
    [SerializeField] private float plateRadiusOffset = 0f;

    [Tooltip("回転させる皿 Transform 群。手動配置する場合に使用。\n下の Plate Prefab + Auto Spawn Plate Count を指定すると自動配置に切替 (この配列は上書き)")]
    [SerializeField] private Transform[] plates;

    [Tooltip("各皿に Kinematic Rigidbody を Awake で自動付与する")]
    [SerializeField] private bool autoAddKinematicRigidbody = true;

    [Header("自動スポーン (オプション)")]
    [Tooltip("プレハブを設定 + Auto Spawn Plate Count > 0 で、Awake 時にパス上へ等間隔自動配置する。手動 plates は上書きされる。")]
    [SerializeField] private GameObject platePrefab;

    [Tooltip("自動スポーンする皿の枚数 (0 = 自動スポーン無効、上の手動 plates を使用)")]
    [Min(0)]
    [SerializeField] private int autoSpawnPlateCount = 0;

    [Header("キャタピラ (オプション)")]
    [Tooltip("手動配置用キャタピラ Transform 群。下の Prefab + Count を指定すると自動配置に切替")]
    [SerializeField] private Transform[] caterpillarPlates;

    [Tooltip("キャタピラ用プレハブ")]
    [SerializeField] private GameObject caterpillarPrefab;

    [Tooltip("自動スポーンするキャタピラの数 (0 = 自動スポーン無効、手動を使用)")]
    [Min(0)]
    [SerializeField] private int autoSpawnCaterpillarCount = 0;

    [Tooltip("キャタピラが回る軌道の半径オフセット (マイナスで内側)")]
    [SerializeField] private float caterpillarRadiusOffset = -0.05f;

    [Header("装飾 (歯車など)")]
    [Tooltip("上部の回転中心に配置・回転させるオブジェクト")]
    [SerializeField] private Transform topGear;

    [Tooltip("下部の回転中心に配置・回転させるオブジェクト")]
    [SerializeField] private Transform bottomGear;

    [Tooltip("歯車の回転速度の倍率 (1.0でベルトと同じ速さ、マイナスで逆回転)")]
    [SerializeField] private float gearSpeedMultiplier = 1.0f;

    [Header("回転軸・速度")]
    [Tooltip("回転軸 (このオブジェクトのローカル空間)")]
    [SerializeField] private Vector3 rotationAxis = Vector3.right;

    [Tooltip("回転速度 (deg/s)。Stadium 時はリム上の同等線速度 (= radius * deg2rad * angularSpeed) に変換される")]
    [SerializeField] private float angularSpeed = 60f;

    [Header("皿の姿勢")]
    [Tooltip("OFF (既定): 皿はパス接線+外向きで向く (水車式、半円を進むと皿も自然に反転する)。設置時の姿勢オフセットは維持される。\nON: 皿は常に初期姿勢を維持 (パターノスター式、半円でも姿勢不変、玉が落ちにくい)")]
    [SerializeField] private bool plateAlwaysUpright = false;

    [Header("皿の物理")]
    [Tooltip("Awake 時に皿 Collider へ高摩擦の PhysicsMaterial を付与 (玉が滑り落ちない)")]
    [SerializeField] private bool autoApplyHighFrictionMaterial = true;

    [Header("カタカタ揺れ + SFX")]
    [Tooltip("Y軸まわりの微振動の最大角度 (deg)。0 で無効")]
    [SerializeField, Min(0f)] private float rattleAmplitudeDegrees = 1.5f;

    [Tooltip("カタカタの細かさ (Hz 相当)。大きいほど細かい震え")]
    [SerializeField, Min(0.1f)] private float rattleFrequency = 10f;

    [Tooltip("カタカタ音 AudioClip。null なら無音")]
    [SerializeField] private AudioClip rattleSfxClip;

    [Tooltip("カタカタ音を鳴らす間隔 (秒)。短いほど忙しいリズム")]
    [SerializeField, Min(0.02f)] private float rattleSfxInterval = 0.18f;

    [Range(0f, 1f)]
    [Tooltip("カタカタ音の音量")]
    [SerializeField] private float rattleSfxVolume = 0.3f;

    [Range(0f, 0.5f)]
    [Tooltip("カタカタ音のピッチ揺らぎ (±幅)")]
    [SerializeField] private float rattleSfxPitchVariance = 0.2f;

    [Header("デバッグ")]
    [Tooltip("OnDrawGizmosSelected で各皿の +Y (上面) と +Z (前方) を矢印で表示")]
    [SerializeField] private bool drawPlateAxesGizmo = false;

    [Tooltip("Awake で設定内容を Console に出力する")]
    [SerializeField] private bool logSettingsOnAwake = false;

    [Tooltip("Update のたびに plate[0] の phase / worldRot 等を Console に出力 (回転していない原因切り分け用)")]
    [SerializeField] private bool logPlate0EachFrame = false;

    private float _logTimer = 0f;

    // --- 内部状態 ---
    // Circle 用
    private Vector3[] _initialLocalOffsets;
    private Vector3[] _caterpillarInitialOffsets;
    private float _currentAngle = 0f;

    // Stadium 用
    private float[] _phases;
    private float[] _caterpillarPhases;
    private float _pathLength;
    private float _caterpillarPathLength;
    
    // 初期姿勢オフセット (皿の設置回転 = pathRot(初期phase) × offset)
    private Quaternion[] _pathRotationOffsets;
    private Quaternion[] _caterpillarPathRotationOffsets;

    // 共通
    private Quaternion[] _initialLocalRotations;
    private Quaternion[] _caterpillarInitialRotations;
    
    private Rigidbody[] _plateRigidbodies;
    private Rigidbody[] _caterpillarRigidbodies;

    // カタカタ揺れ + SFX
    private float _currentWobbleAngle = 0f;
    private float _rattleSfxTimer = 0f;
    private AudioSource _rattleAudioSource;

    void Awake()
    {
        if (logSettingsOnAwake)
        {
            Debug.Log($"[PinballWaterWheel] shape={shape}, plateAlwaysUpright={plateAlwaysUpright}, plates={(plates != null ? plates.Length : 0)}, radius={radius}, stretchLength={stretchLength}, rotationAxis={rotationAxis}, angularSpeed={angularSpeed}", this);
        }

        // カタカタ用 AudioSource (このオブジェクト自身に追加)
        if (rattleSfxClip != null)
        {
            _rattleAudioSource = gameObject.AddComponent<AudioSource>();
            _rattleAudioSource.playOnAwake = false;
            _rattleAudioSource.spatialBlend = 0f; // 2D 再生
        }

        Vector3 axisN0 = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;
        Vector3 stretchN = stretchAxis.sqrMagnitude > 0.0001f ? stretchAxis.normalized : Vector3.up;

        // 装飾(歯車)の初期配置
        if (topGear != null)
        {
            if (!topGear.gameObject.scene.IsValid())
            {
                topGear = Instantiate(topGear, transform);
            }
            topGear.localPosition = (shape == WheelShape.Stadium) ? stretchN * (stretchLength / 2f) : Vector3.zero;
        }
        if (bottomGear != null)
        {
            if (!bottomGear.gameObject.scene.IsValid())
            {
                bottomGear = Instantiate(bottomGear, transform);
            }
            bottomGear.localPosition = (shape == WheelShape.Stadium) ? -stretchN * (stretchLength / 2f) : Vector3.zero;
        }

        // 自動スポーン
        if (platePrefab != null && autoSpawnPlateCount > 0)
        {
            AutoSpawnPlates();
        }
        if (caterpillarPrefab != null && autoSpawnCaterpillarCount > 0)
        {
            AutoSpawnCaterpillars();
        }

        // 初期化処理
        _pathLength = 2f * stretchLength + 2f * Mathf.PI * Mathf.Max(0.01f, radius + plateRadiusOffset);
        _caterpillarPathLength = 2f * stretchLength + 2f * Mathf.PI * Mathf.Max(0.01f, radius + caterpillarRadiusOffset);

        if (plates == null || plates.Length == 0)
        {
            Debug.LogWarning("[PinballWaterWheel] plates 配列が空です。手動配置するか Plate Prefab + Auto Spawn Plate Count を設定してください。", this);
        }

        InitPlates(plates, radius + plateRadiusOffset, out _initialLocalOffsets, out _initialLocalRotations, out _plateRigidbodies, out _phases, out _pathRotationOffsets);
        InitPlates(caterpillarPlates, radius + caterpillarRadiusOffset, out _caterpillarInitialOffsets, out _caterpillarInitialRotations, out _caterpillarRigidbodies, out _caterpillarPhases, out _caterpillarPathRotationOffsets);
    }

    void InitPlates(Transform[] targetPlates, float currentRadius, 
        out Vector3[] initOffsets, out Quaternion[] initRots, out Rigidbody[] rbs, 
        out float[] phasesOut, out Quaternion[] pathRotOffsetsOut)
    {
        if (targetPlates == null || targetPlates.Length == 0)
        {
            initOffsets = null; initRots = null; rbs = null; phasesOut = null; pathRotOffsetsOut = null;
            return;
        }

        int n = targetPlates.Length;
        initOffsets = new Vector3[n];
        initRots = new Quaternion[n];
        rbs = new Rigidbody[n];
        phasesOut = new float[n];
        pathRotOffsetsOut = new Quaternion[n];

        Vector3 axisN0 = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;

        for (int i = 0; i < n; i++)
        {
            if (targetPlates[i] == null) continue;
            initOffsets[i] = transform.InverseTransformPoint(targetPlates[i].position);
            initRots[i] = Quaternion.Inverse(transform.rotation) * targetPlates[i].rotation;

            if (shape == WheelShape.Stadium)
            {
                phasesOut[i] = FindClosestPhase(initOffsets[i], currentRadius);

                Vector3 t0 = ComputeTangent(phasesOut[i], currentRadius);
                Vector3 o0 = Vector3.Cross(axisN0, t0);
                if (o0.sqrMagnitude < 0.0001f) o0 = Vector3.up;
                else o0 = o0.normalized;

                Vector3 wt0 = transform.TransformDirection(t0);
                Vector3 wo0 = transform.TransformDirection(o0);
                if (wt0.sqrMagnitude > 0.0001f && wo0.sqrMagnitude > 0.0001f)
                {
                    Quaternion initialPathRot = Quaternion.LookRotation(wt0, wo0);
                    pathRotOffsetsOut[i] = Quaternion.Inverse(initialPathRot) * targetPlates[i].rotation;
                }
                else
                {
                    pathRotOffsetsOut[i] = Quaternion.identity;
                }
            }
            else
            {
                phasesOut[i] = (float)i / n;
                pathRotOffsetsOut[i] = Quaternion.identity;
            }

            if (autoAddKinematicRigidbody)
            {
                var rb = targetPlates[i].GetComponent<Rigidbody>();
                if (rb == null) rb = targetPlates[i].gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rbs[i] = rb;
            }
            else
            {
                rbs[i] = targetPlates[i].GetComponent<Rigidbody>();
            }

            if (autoApplyHighFrictionMaterial)
            {
                foreach (var col in targetPlates[i].GetComponentsInChildren<Collider>())
                {
                    if (col.sharedMaterial == null)
                    {
                        col.sharedMaterial = new PhysicsMaterial("WaterWheelPlate")
                        {
                            bounciness = 0f,
                            dynamicFriction = 1f,
                            staticFriction = 1f,
                            bounceCombine = PhysicsMaterialCombine.Multiply,
                            frictionCombine = PhysicsMaterialCombine.Maximum,
                        };
                    }
                }
            }
        }
    }

    void AutoSpawnPlates()
    {
        plates = new Transform[autoSpawnPlateCount];
        SpawnAndSetupArray(autoSpawnPlateCount, platePrefab, plates, radius + plateRadiusOffset);
    }
    
    void AutoSpawnCaterpillars()
    {
        caterpillarPlates = new Transform[autoSpawnCaterpillarCount];
        SpawnAndSetupArray(autoSpawnCaterpillarCount, caterpillarPrefab, caterpillarPlates, radius + caterpillarRadiusOffset);
    }

    void SpawnAndSetupArray(int count, GameObject prefab, Transform[] outputArray, float currentRadius)
    {
        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;
        Quaternion prefabRot = prefab.transform.rotation;

        for (int i = 0; i < count; i++)
        {
            float phase = (float)i / count;

            GameObject go = Instantiate(prefab, transform);
            go.name = $"{prefab.name}_{i}";
            outputArray[i] = go.transform;

            Vector3 localPos = SampleStadiumPath(phase, currentRadius);
            outputArray[i].position = transform.TransformPoint(localPos);

            Quaternion initialRot;
            if (plateAlwaysUpright)
            {
                initialRot = transform.rotation * prefabRot;
            }
            else
            {
                Quaternion pathRot = ComputePathRotationWorld(phase, axisN, currentRadius);
                initialRot = pathRot * prefabRot;
            }
            outputArray[i].rotation = initialRot;
        }
    }

    Quaternion ComputePathRotationWorld(float phase, Vector3 axisN, float currentRadius)
    {
        Vector3 tangent = ComputeTangent(phase, currentRadius);
        Vector3 outward = Vector3.Cross(axisN, tangent);
        if (outward.sqrMagnitude < 0.0001f) outward = Vector3.up;
        else outward = outward.normalized;

        Vector3 worldTangent = transform.TransformDirection(tangent);
        Vector3 worldOutward = transform.TransformDirection(outward);

        if (worldTangent.sqrMagnitude > 0.0001f && worldOutward.sqrMagnitude > 0.0001f)
        {
            return Quaternion.LookRotation(worldTangent, worldOutward);
        }
        return Quaternion.identity;
    }

    float FindClosestPhase(Vector3 localPos, float currentRadius)
    {
        const int samples = 360;
        float bestPhase = 0f;
        float bestDist = float.MaxValue;
        for (int i = 0; i < samples; i++)
        {
            float p = (float)i / samples;
            Vector3 pp = SampleStadiumPath(p, currentRadius);
            float d = (pp - localPos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                bestPhase = p;
            }
        }
        return bestPhase;
    }

    void FixedUpdate()
    {
        if (rotationAxis.sqrMagnitude < 0.0001f) return;

        if (shape == WheelShape.Circle)
        {
            FixedUpdateCircle();
        }
        else
        {
            FixedUpdateStadium();
        }
    }

    void Update()
    {
        // カタカタ揺れ
        if (rattleAmplitudeDegrees > 0f)
        {
            float t = Time.time * rattleFrequency;
            float targetWobble = (Mathf.PerlinNoise(t, 7.123f) - 0.5f) * 2f * rattleAmplitudeDegrees;
            float deltaWobble = targetWobble - _currentWobbleAngle;
            transform.Rotate(Vector3.up, deltaWobble, Space.Self);
            _currentWobbleAngle = targetWobble;
        }

        // カタカタ SFX
        if (_rattleAudioSource != null && rattleSfxClip != null && rattleSfxInterval > 0f)
        {
            _rattleSfxTimer += Time.deltaTime;
            if (_rattleSfxTimer >= rattleSfxInterval)
            {
                _rattleSfxTimer -= rattleSfxInterval;
                float v = rattleSfxPitchVariance;
                _rattleAudioSource.pitch = (v > 0f) ? 1f + Random.Range(-v, v) : 1f;
                _rattleAudioSource.PlayOneShot(rattleSfxClip, rattleSfxVolume);
            }
        }
    }

    void FixedUpdateCircle()
    {
        _currentAngle += angularSpeed * Time.fixedDeltaTime;
        Quaternion totalRot = Quaternion.AngleAxis(_currentAngle, rotationAxis.normalized);

        UpdateCircleArray(plates, _initialLocalOffsets, _initialLocalRotations, _plateRigidbodies, totalRot);
        UpdateCircleArray(caterpillarPlates, _caterpillarInitialOffsets, _caterpillarInitialRotations, _caterpillarRigidbodies, totalRot);
        
        UpdateGears();
    }
    
    void UpdateCircleArray(Transform[] array, Vector3[] initOffsets, Quaternion[] initRots, Rigidbody[] rbs, Quaternion totalRot)
    {
        if (array == null) return;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == null) continue;
            Vector3 localPos = totalRot * initOffsets[i];
            Vector3 worldPos = transform.TransformPoint(localPos);

            Quaternion localRot = plateAlwaysUpright
                ? initRots[i]
                : totalRot * initRots[i];
            Quaternion worldRot = transform.rotation * localRot;

            ApplyToPlate(array[i], rbs[i], worldPos, worldRot);
        }
    }

    void FixedUpdateStadium()
    {
        UpdateStadiumArray(plates, radius + plateRadiusOffset, _pathLength, _phases, _initialLocalRotations, _pathRotationOffsets, _plateRigidbodies, true);
        UpdateStadiumArray(caterpillarPlates, radius + caterpillarRadiusOffset, _caterpillarPathLength, _caterpillarPhases, _caterpillarInitialRotations, _caterpillarPathRotationOffsets, _caterpillarRigidbodies, false);

        UpdateGears();
    }

    void UpdateStadiumArray(Transform[] array, float currentRadius, float pathLen, float[] phasesArray, Quaternion[] initRots, Quaternion[] pathRotOffsets, Rigidbody[] rbs, bool logFirst)
    {
        if (array == null) return;

        float r = Mathf.Max(0.01f, currentRadius);
        float linearSpeed = angularSpeed * Mathf.Deg2Rad * r;
        float dPhase = (linearSpeed * Time.fixedDeltaTime) / Mathf.Max(0.001f, pathLen);

        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == null) continue;
            phasesArray[i] = Mathf.Repeat(phasesArray[i] + dPhase, 1f);

            Vector3 localPos = SampleStadiumPath(phasesArray[i], currentRadius);
            Vector3 worldPos = transform.TransformPoint(localPos);

            Quaternion worldRot;
            if (plateAlwaysUpright)
            {
                worldRot = transform.rotation * initRots[i];
            }
            else
            {
                Vector3 tangent = ComputeTangent(phasesArray[i], currentRadius);
                Vector3 outward = Vector3.Cross(axisN, tangent);
                if (outward.sqrMagnitude < 0.0001f) outward = Vector3.up;
                else outward = outward.normalized;

                Vector3 worldTangent = transform.TransformDirection(tangent);
                Vector3 worldOutward = transform.TransformDirection(outward);

                if (worldTangent.sqrMagnitude > 0.0001f && worldOutward.sqrMagnitude > 0.0001f)
                {
                    Quaternion pathRot = Quaternion.LookRotation(worldTangent, worldOutward);
                    worldRot = pathRot * pathRotOffsets[i];
                }
                else
                {
                    worldRot = transform.rotation * initRots[i];
                }
            }

            ApplyToPlate(array[i], rbs[i], worldPos, worldRot);

            if (logFirst && logPlate0EachFrame && i == 0)
            {
                _logTimer += Time.fixedDeltaTime;
                if (_logTimer >= 0.5f)
                {
                    _logTimer = 0f;
                    Vector3 t = ComputeTangent(phasesArray[0], currentRadius);
                    Vector3 o = Vector3.Cross(axisN, t);
                    if (o.sqrMagnitude < 0.0001f) o = Vector3.up; else o = o.normalized;
                    Debug.Log(
                        $"[WaterWheel] plate0 phase={phasesArray[0]:F3} alwaysUpright={plateAlwaysUpright}\n" +
                        $"  tangent(local)={t} outward(local)={o} axisN={axisN}\n" +
                        $"  worldTangent={transform.TransformDirection(t)} worldOutward={transform.TransformDirection(o)}\n" +
                        $"  worldRotEuler={worldRot.eulerAngles}\n" +
                        $"  pathOffsetEuler={pathRotOffsets[0].eulerAngles}\n" +
                        $"  shape={shape} pathLength={pathLen:F3} radius={currentRadius} stretchLength={stretchLength}",
                        this);
                }
            }
        }
    }

    void UpdateGears()
    {
        Vector3 worldAxis = transform.TransformDirection(rotationAxis.normalized);
        // Stadium形状ではパスの進行方向がAngleAxisの回転方向と逆になるため、歯車の回転も反転させる
        float baseGearSpeed = (shape == WheelShape.Stadium) ? -angularSpeed : angularSpeed;
        float gearSpeed = baseGearSpeed * gearSpeedMultiplier;

        if (topGear != null)
        {
            topGear.Rotate(worldAxis, gearSpeed * Time.fixedDeltaTime, Space.World);
        }
        if (bottomGear != null)
        {
            bottomGear.Rotate(worldAxis, gearSpeed * Time.fixedDeltaTime, Space.World);
        }
    }

    void ApplyToPlate(Transform t, Rigidbody rb, Vector3 worldPos, Quaternion worldRot)
    {
        if (rb != null && rb.isKinematic)
        {
            rb.MovePosition(worldPos);
            rb.MoveRotation(worldRot);
        }
        else
        {
            t.position = worldPos;
            t.rotation = worldRot;
        }
    }

    Vector3 SampleStadiumPath(float phase01, float currentRadius)
    {
        Vector3 stretchN = stretchAxis.sqrMagnitude > 0.0001f ? stretchAxis.normalized : Vector3.up;
        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;
        Vector3 perp = Vector3.Cross(axisN, stretchN);
        if (perp.sqrMagnitude < 0.0001f) perp = Vector3.right;
        else perp = perp.normalized;

        float L = stretchLength;
        float r = Mathf.Max(0.01f, currentRadius);
        float total = 2f * L + 2f * Mathf.PI * r;
        float t = phase01 * total;

        if (t < L)
        {
            return perp * r + stretchN * (-L / 2f + t);
        }
        t -= L;
        if (t < Mathf.PI * r)
        {
            float theta = t / r;
            return stretchN * (L / 2f) + r * (Mathf.Cos(theta) * perp + Mathf.Sin(theta) * stretchN);
        }
        t -= Mathf.PI * r;
        if (t < L)
        {
            return -perp * r + stretchN * (L / 2f - t);
        }
        t -= L;
        float theta2 = t / r;
        return stretchN * (-L / 2f) + r * (-Mathf.Cos(theta2) * perp - Mathf.Sin(theta2) * stretchN);
    }

    Vector3 ComputeTangent(float phase01, float currentRadius)
    {
        Vector3 stretchN = stretchAxis.sqrMagnitude > 0.0001f ? stretchAxis.normalized : Vector3.up;
        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;
        Vector3 perp = Vector3.Cross(axisN, stretchN);
        if (perp.sqrMagnitude < 0.0001f) perp = Vector3.right;
        else perp = perp.normalized;

        float L = stretchLength;
        float r = Mathf.Max(0.01f, currentRadius);
        float total = 2f * L + 2f * Mathf.PI * r;
        float t = Mathf.Repeat(phase01, 1f) * total;

        if (t < L) return stretchN;
        t -= L;
        if (t < Mathf.PI * r)
        {
            float theta = t / r;
            return (-Mathf.Sin(theta) * perp + Mathf.Cos(theta) * stretchN).normalized;
        }
        t -= Mathf.PI * r;
        if (t < L) return -stretchN;
        t -= L;
        float theta2 = t / r;
        return (Mathf.Sin(theta2) * perp - Mathf.Cos(theta2) * stretchN).normalized;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 worldAxis = transform.TransformDirection(rotationAxis.normalized);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position - worldAxis * 0.5f, transform.position + worldAxis * 0.5f);

        if (shape == WheelShape.Circle)
        {
            DrawWheelGizmo(radius + plateRadiusOffset);
            if (caterpillarPrefab != null || (caterpillarPlates != null && caterpillarPlates.Length > 0))
            {
                DrawWheelGizmo(radius + caterpillarRadiusOffset);
            }
        }
        else
        {
            DrawStadiumGizmo(radius + plateRadiusOffset);
            if (caterpillarPrefab != null || (caterpillarPlates != null && caterpillarPlates.Length > 0))
            {
                DrawStadiumGizmo(radius + caterpillarRadiusOffset);
            }
        }

        if (drawPlateAxesGizmo)
        {
            DrawPlateAxes(plates);
            DrawPlateAxes(caterpillarPlates);
        }
    }

    void DrawWheelGizmo(float currentRadius)
    {
        Vector3 worldAxis = transform.TransformDirection(rotationAxis.normalized);
        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        DrawCircleGizmo(transform.position, worldAxis, currentRadius, 36);
    }

    void DrawStadiumGizmo(float currentRadius)
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        const int seg = 80;
        Vector3 prev = transform.TransformPoint(SampleStadiumPath(0f, currentRadius));
        for (int i = 1; i <= seg; i++)
        {
            Vector3 cur = transform.TransformPoint(SampleStadiumPath((float)i / seg, currentRadius));
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }

    void DrawPlateAxes(Transform[] array)
    {
        if (array == null) return;
        float arrowLen = 0.15f;
        foreach (var plate in array)
        {
            if (plate == null) continue;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(plate.position, plate.position + plate.up * arrowLen);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(plate.position, plate.position + plate.forward * arrowLen);
        }
    }

    static void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius, int segments)
    {
        Vector3 reference = Vector3.Cross(normal, Vector3.up);
        if (reference.sqrMagnitude < 0.001f) reference = Vector3.Cross(normal, Vector3.right);
        reference = reference.normalized * radius;

        Vector3 prev = center + reference;
        for (int i = 1; i <= segments; i++)
        {
            float ang = 360f / segments * i;
            Vector3 next = center + Quaternion.AngleAxis(ang, normal) * reference;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
