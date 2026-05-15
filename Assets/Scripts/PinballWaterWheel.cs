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
    [Tooltip("回転させる皿 Transform 群")]
    [SerializeField] private Transform[] plates;

    [Tooltip("各皿に Kinematic Rigidbody を Awake で自動付与する")]
    [SerializeField] private bool autoAddKinematicRigidbody = true;

    [Header("回転軸・速度")]
    [Tooltip("回転軸 (このオブジェクトのローカル空間)")]
    [SerializeField] private Vector3 rotationAxis = Vector3.right;

    [Tooltip("回転速度 (deg/s)。Stadium 時はリム上の同等線速度 (= radius * deg2rad * angularSpeed) に変換される")]
    [SerializeField] private float angularSpeed = 60f;

    [Header("皿の姿勢")]
    [Tooltip("ON: 皿は常に初期姿勢を維持 (パターノスター式、半円でも姿勢不変、玉が落ちにくい)\nOFF: 皿はパス接線+外向きで向く (水車式、半円を進むと皿も自然に反転する)\n\n皿プレハブの『上面』が local +Y 方向になるように作っておくと OFF 時に正しく外向きになる。")]
    [SerializeField] private bool plateAlwaysUpright = true;

    [Header("皿の物理")]
    [Tooltip("Awake 時に皿 Collider へ高摩擦の PhysicsMaterial を付与 (玉が滑り落ちない)")]
    [SerializeField] private bool autoApplyHighFrictionMaterial = true;

    // --- 内部状態 ---
    // Circle 用
    private Vector3[] _initialLocalOffsets;
    private float _currentAngle = 0f;

    // Stadium 用
    private float[] _phases;
    private float _pathLength;

    // 共通
    private Quaternion[] _initialLocalRotations;
    private Rigidbody[] _plateRigidbodies;

    void Awake()
    {
        if (plates == null || plates.Length == 0) return;
        int n = plates.Length;
        _initialLocalOffsets = new Vector3[n];
        _initialLocalRotations = new Quaternion[n];
        _plateRigidbodies = new Rigidbody[n];
        _phases = new float[n];

        for (int i = 0; i < n; i++)
        {
            if (plates[i] == null) continue;
            _initialLocalOffsets[i] = transform.InverseTransformPoint(plates[i].position);
            _initialLocalRotations[i] = Quaternion.Inverse(transform.rotation) * plates[i].rotation;
            _phases[i] = (float)i / n;

            if (autoAddKinematicRigidbody)
            {
                var rb = plates[i].GetComponent<Rigidbody>();
                if (rb == null) rb = plates[i].gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                _plateRigidbodies[i] = rb;
            }
            else
            {
                _plateRigidbodies[i] = plates[i].GetComponent<Rigidbody>();
            }

            if (autoApplyHighFrictionMaterial)
            {
                foreach (var col in plates[i].GetComponentsInChildren<Collider>())
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

        _pathLength = 2f * stretchLength + 2f * Mathf.PI * Mathf.Max(0.01f, radius);
    }

    void FixedUpdate()
    {
        if (plates == null || plates.Length == 0) return;
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

    void FixedUpdateCircle()
    {
        _currentAngle += angularSpeed * Time.fixedDeltaTime;
        Quaternion totalRot = Quaternion.AngleAxis(_currentAngle, rotationAxis.normalized);

        for (int i = 0; i < plates.Length; i++)
        {
            if (plates[i] == null) continue;
            Vector3 localPos = totalRot * _initialLocalOffsets[i];
            Vector3 worldPos = transform.TransformPoint(localPos);

            Quaternion localRot = plateAlwaysUpright
                ? _initialLocalRotations[i]
                : totalRot * _initialLocalRotations[i];
            Quaternion worldRot = transform.rotation * localRot;

            ApplyToPlate(i, worldPos, worldRot);
        }
    }

    void FixedUpdateStadium()
    {
        // angularSpeed (deg/s) をリム上の線速度 (m/s) に換算
        float r = Mathf.Max(0.01f, radius);
        float linearSpeed = angularSpeed * Mathf.Deg2Rad * r;
        float dPhase = (linearSpeed * Time.fixedDeltaTime) / Mathf.Max(0.001f, _pathLength);

        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;

        for (int i = 0; i < plates.Length; i++)
        {
            if (plates[i] == null) continue;
            _phases[i] = Mathf.Repeat(_phases[i] + dPhase, 1f);

            Vector3 localPos = SampleStadiumPath(_phases[i]);
            Vector3 worldPos = transform.TransformPoint(localPos);

            Quaternion worldRot;
            if (plateAlwaysUpright)
            {
                worldRot = transform.rotation * _initialLocalRotations[i];
            }
            else
            {
                // パス接線 (Z+) と外向き (Y+) で皿を向ける (水車式: 半円を進むと皿も回転する)
                Vector3 tangent = ComputeTangent(_phases[i]);
                // 外向き = rotationAxis × tangent → 直線では perp 方向、半円ではパス中心から外への方向
                Vector3 outward = Vector3.Cross(axisN, tangent);
                if (outward.sqrMagnitude < 0.0001f) outward = Vector3.up;
                else outward = outward.normalized;

                Vector3 worldTangent = transform.TransformDirection(tangent);
                Vector3 worldOutward = transform.TransformDirection(outward);

                if (worldTangent.sqrMagnitude > 0.0001f && worldOutward.sqrMagnitude > 0.0001f)
                {
                    worldRot = Quaternion.LookRotation(worldTangent, worldOutward);
                }
                else
                {
                    worldRot = transform.rotation * _initialLocalRotations[i];
                }
            }

            ApplyToPlate(i, worldPos, worldRot);
        }
    }

    void ApplyToPlate(int i, Vector3 worldPos, Quaternion worldRot)
    {
        var rb = _plateRigidbodies[i];
        if (rb != null && rb.isKinematic)
        {
            rb.MovePosition(worldPos);
            rb.MoveRotation(worldRot);
        }
        else
        {
            plates[i].position = worldPos;
            plates[i].rotation = worldRot;
        }
    }

    /// <summary>
    /// Stadium パス上 phase (0~1) の位置をローカル空間で返す。
    ///   Phase 0: +perp 側ストレート下端から開始
    ///   経路: 右ストレート上昇 → 上半円 → 左ストレート下降 → 下半円 → ループ
    /// </summary>
    Vector3 SampleStadiumPath(float phase01)
    {
        Vector3 stretchN = stretchAxis.sqrMagnitude > 0.0001f ? stretchAxis.normalized : Vector3.up;
        Vector3 axisN = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.right;
        Vector3 perp = Vector3.Cross(axisN, stretchN);
        if (perp.sqrMagnitude < 0.0001f) perp = Vector3.right;
        else perp = perp.normalized;

        float L = stretchLength;
        float r = Mathf.Max(0.01f, radius);
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

    Vector3 ComputeTangent(float phase01)
    {
        const float eps = 0.001f;
        Vector3 a = SampleStadiumPath(Mathf.Repeat(phase01 + eps, 1f));
        Vector3 b = SampleStadiumPath(Mathf.Repeat(phase01 - eps + 1f, 1f));
        Vector3 d = a - b;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.right;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 worldAxis = transform.TransformDirection(rotationAxis.normalized);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position - worldAxis * 0.5f, transform.position + worldAxis * 0.5f);

        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        if (shape == WheelShape.Circle)
        {
            // 円の軌道
            if (plates != null)
            {
                foreach (var plate in plates)
                {
                    if (plate == null) continue;
                    float r = Vector3.Distance(plate.position, transform.position);
                    DrawCircleGizmo(transform.position, worldAxis, r, 36);
                }
            }
        }
        else
        {
            // Stadium の軌道をサンプリングして描画
            const int seg = 80;
            Vector3 prev = transform.TransformPoint(SampleStadiumPath(0f));
            for (int i = 1; i <= seg; i++)
            {
                Vector3 cur = transform.TransformPoint(SampleStadiumPath((float)i / seg));
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
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
