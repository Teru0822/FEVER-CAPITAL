using System.Collections;
using UnityEngine;

/// <summary>
/// ピンボールの「5」オブジェクト（ボール）制御スクリプト。
/// - 全オブジェクトに対して当たり判定を持つ
/// - 「4」オブジェクトに押されて飛び出す
/// - Z軸正方向に重力と同じ大きさの力を常に受ける
/// - 「2」オブジェクトに衝突するたびに半分サイズで二分裂する
///   （分裂のX方向オフセットは世代ごとに半分になる）
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class PinballBallController : MonoBehaviour
{
    [Header("ボール設定")]
    public float mass = 0.5f;

    public float bounciness = 0.5f;

    [Header("分裂設定")]
    [Tooltip("衝突時に分裂するオブジェクトのタグ（Unity の Tag を指定）")]
    public string splitTargetTag = "Splitter";

    [Tooltip("分裂後に左右へ広がる速度（m/s）")]
    public float splitSpread = 1f;

    [Tooltip("分裂数（2以上）")]
    [Min(2)]
    public int splitCount = 2;

    [Tooltip("分裂後のスケール倍率（0〜1）。例：0.5なら半分のサイズ")]
    [Range(0.01f, 1f)]
    public float splitScaleRatio = 0.5f;

    [Tooltip("分裂後のスポーン位置をY軸上方向にずらす量")]
    public float spawnUpOffset = 0.5f;

    [Tooltip("一回目の分裂時の左右Xオフセット（以降の世代は自動的に半分になる）")]
    public float spawnXOffset = 0.5f;

    [Tooltip("「2」との衝突を無視する時間（秒）")]
    public float ignoreCollisionDuration = 0.4f;

    // 実際にこのボールが分裂するときのXオフセット（世代ごとに半減）
    // Awake後にSpawnSplitBallから上書きされる
    private float _currentXOffset;

    // 同一フレームでの二重分裂を防ぐフラグ
    private bool _isSplitting = false;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        SphereCollider col = GetComponent<SphereCollider>();
        PhysicsMaterial mat = new PhysicsMaterial("BallPhysics");
        mat.bounciness = bounciness;
        mat.dynamicFriction = 0.3f;
        mat.staticFriction  = 0.3f;
        mat.frictionCombine  = PhysicsMaterialCombine.Average;
        mat.bounceCombine    = PhysicsMaterialCombine.Maximum;
        col.material = mat;

        // 初期値（子ボールではSpawnSplitBallがAwake直後に上書きする）
        _currentXOffset = spawnXOffset;
    }

    void FixedUpdate()
    {
        rb.AddForce(new Vector3(0f, 0f, rb.mass * Mathf.Abs(Physics.gravity.y)), ForceMode.Force);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_isSplitting) return;
        if (!collision.gameObject.CompareTag(splitTargetTag)) return;

        _isSplitting = true;

        // 衝突した瞬間の位置を記録（コルーチン待機中のズレを防ぐ）
        Vector3 posAtCollision = transform.position;

        StartCoroutine(SplitNextFrame(posAtCollision, collision.collider));
    }

    IEnumerator SplitNextFrame(Vector3 posAtCollision, Collider splitTargetCollider)
    {
        yield return new WaitForFixedUpdate();

        int count = Mathf.Max(2, splitCount);
        Vector3 nextScale = transform.localScale * splitScaleRatio;
        Vector3 spawnBase = posAtCollision + Vector3.up * spawnUpOffset;

        // -_currentXOffset 〜 +_currentXOffset の範囲に count 個を等間隔配置
        // 速度も同じ比率で左右に広がる
        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0f : ((float)i / (count - 1)) * 2f - 1f; // -1 〜 +1
            Vector3 spawnPos = spawnBase + Vector3.right * (_currentXOffset * t);
            Vector3 vel = Vector3.right * (splitSpread * t);
            SpawnSplitBall(spawnPos, vel, nextScale, splitTargetCollider);
        }

        Destroy(gameObject);
    }

    void SpawnSplitBall(Vector3 position, Vector3 velocity, Vector3 scale, Collider ignoreCollider)
    {
        GameObject child = Instantiate(gameObject, position, transform.rotation);
        child.transform.localScale = scale;

        PinballBallController ctrl = child.GetComponent<PinballBallController>();
        if (ctrl != null)
        {
            // 次世代のXオフセットは現世代の半分
            ctrl._currentXOffset = _currentXOffset / 2f;
            // 子ボールは分裂可能（_isSplitting = false のまま）
        }

        Rigidbody childRb = child.GetComponent<Rigidbody>();
        if (childRb != null)
            childRb.linearVelocity = velocity;

        // 「2」との衝突を一定時間無視
        Collider childCol = child.GetComponent<Collider>();
        if (childCol != null && ignoreCollider != null)
        {
            Physics.IgnoreCollision(childCol, ignoreCollider, true);
            ctrl?.StartCoroutine(RestoreCollision(childCol, ignoreCollider, ignoreCollisionDuration));
        }
    }

    IEnumerator RestoreCollision(Collider col, Collider other, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (col != null && other != null)
            Physics.IgnoreCollision(col, other, false);
    }
}
