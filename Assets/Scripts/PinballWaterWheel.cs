using UnityEngine;

/// <summary>
/// 水車型のベルトコンベア。複数の「皿」を回転軸まわりにぐるぐる回し、
/// 下から上に物体を運び上げる。皿は Kinematic Rigidbody として動くので
/// Unity 物理が自動的に玉を皿の上に載せて運搬する。
///
/// セットアップ:
///   1. 空 GameObject を水車の中心位置に置き、本コンポーネントをアタッチ
///   2. その子として皿オブジェクトを好みの距離 (= 半径) ・好みの間隔で配置
///   3. plates 配列に各皿の Transform をドラッグ
///   4. rotationAxis を回転させたい軸 (例: ローカル X) に、angularSpeed を deg/s で
///   5. 玉が乗る面に Collider が付いていることを確認 (PhysicsMaterial で摩擦高めにすると安定)
/// </summary>
public class PinballWaterWheel : MonoBehaviour
{
    [Header("皿")]
    [Tooltip("回転させる皿 Transform 群。子オブジェクトとして配置するのを推奨")]
    [SerializeField] private Transform[] plates;

    [Tooltip("各皿に Kinematic Rigidbody を Awake で自動付与する (Unity 物理で玉を運搬可能にする)")]
    [SerializeField] private bool autoAddKinematicRigidbody = true;

    [Header("回転軸・速度")]
    [Tooltip("回転軸 (このオブジェクトのローカル空間)。右に倒した水車なら (1, 0, 0) など")]
    [SerializeField] private Vector3 rotationAxis = Vector3.right;

    [Tooltip("回転速度 (deg/s)。+ で右ねじ方向、- で逆")]
    [SerializeField] private float angularSpeed = 60f;

    [Header("皿の姿勢")]
    [Tooltip("ON: 皿は常に初期姿勢を維持 (パターノスター式、玉が落ちにくい)\nOFF: 皿も回転と一緒にひっくり返る (本物の水車)")]
    [SerializeField] private bool plateAlwaysUpright = true;

    [Header("皿の物理")]
    [Tooltip("Awake 時に皿 Collider へ高摩擦の PhysicsMaterial を付与 (玉が滑り落ちない)")]
    [SerializeField] private bool autoApplyHighFrictionMaterial = true;

    private Vector3[] _initialLocalOffsets;
    private Quaternion[] _initialLocalRotations;
    private Rigidbody[] _plateRigidbodies;
    private float _currentAngle = 0f;

    void Awake()
    {
        if (plates == null || plates.Length == 0) return;
        int n = plates.Length;
        _initialLocalOffsets = new Vector3[n];
        _initialLocalRotations = new Quaternion[n];
        _plateRigidbodies = new Rigidbody[n];

        for (int i = 0; i < n; i++)
        {
            if (plates[i] == null) continue;
            // 中心からの初期オフセット (この距離 = 半径) と相対姿勢を記録
            _initialLocalOffsets[i] = transform.InverseTransformPoint(plates[i].position);
            _initialLocalRotations[i] = Quaternion.Inverse(transform.rotation) * plates[i].rotation;

            // Kinematic Rigidbody を自動付与 (動く Collider として Unity 物理に認識させる)
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

            // 高摩擦マテリアルを付与
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
    }

    void FixedUpdate()
    {
        if (plates == null || plates.Length == 0) return;
        if (rotationAxis.sqrMagnitude < 0.0001f) return;

        _currentAngle += angularSpeed * Time.fixedDeltaTime;
        Quaternion totalRot = Quaternion.AngleAxis(_currentAngle, rotationAxis.normalized);

        for (int i = 0; i < plates.Length; i++)
        {
            if (plates[i] == null) continue;

            // ローカル空間で初期オフセットを totalRot で回す
            Vector3 localPos = totalRot * _initialLocalOffsets[i];
            Vector3 worldPos = transform.TransformPoint(localPos);

            Quaternion localRot;
            if (plateAlwaysUpright)
            {
                // 皿は初期姿勢を保つ (パターノスター式)
                localRot = _initialLocalRotations[i];
            }
            else
            {
                // 皿も回転と一緒にひっくり返る (水車式)
                localRot = totalRot * _initialLocalRotations[i];
            }
            Quaternion worldRot = transform.rotation * localRot;

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
    }

    void OnDrawGizmosSelected()
    {
        // 回転軸を可視化
        Vector3 worldAxis = transform.TransformDirection(rotationAxis.normalized);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position - worldAxis * 0.5f, transform.position + worldAxis * 0.5f);

        // 皿の軌道を可視化 (半径 = 皿の中心オフセット長)
        if (plates == null) return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        foreach (var plate in plates)
        {
            if (plate == null) continue;
            float radius = Vector3.Distance(plate.position, transform.position);
            DrawCircleGizmo(transform.position, worldAxis, radius, 36);
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
