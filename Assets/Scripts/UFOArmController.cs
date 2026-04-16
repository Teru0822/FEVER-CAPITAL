using UnityEngine;

/// <summary>
/// UFOキャッチャーの状態機械と全体制御。
/// 空のGameObjectにアタッチし、各Transformをインスペクターで設定してください。
/// </summary>
public class UFOArmController : MonoBehaviour
{
    public enum ArmState { Idle, Moving, Descending, Grabbing, Ascending }

    // ─────────────────────────────────────
    [Header("アーム参照")]
    [Tooltip("アーム全体を動かす親Transform（オブジェクト3 か、3を子に持つ空オブジェクト）")]
    public Transform armRoot;

    [Tooltip("縦レール（Object 1）: Z方向をアームに追従させる")]
    public Transform rail1;

    [Tooltip("横レール（Object 2）: X方向をアームに追従させる")]
    public Transform rail2;

    [Tooltip("StretchRope が付いているオブジェクト（6）")]
    public StretchRope stretchRope;

    // ─────────────────────────────────────
    [Header("XZ 移動設定")]
    [Tooltip("レバー入力に対するアーム移動速度")]
    public float moveSpeed = 3f;
    [Tooltip("初期位置からの移動限界（X方向）")]
    public float moveRangeX = 5f;
    [Tooltip("初期位置からの移動限界（Z方向）")]
    public float moveRangeZ = 5f;

    // ─────────────────────────────────────
    [Header("爪（finger）設定")]
    [Tooltip("finger.001〜.004 を配列に設定してください")]
    public Transform[] fingerParts;
    [Tooltip("爪が開いたときのローカルX軸回転角度（上向きに開く場合は正の値）")]
    public float fingerOpenAngle = 40f;
    [Tooltip("指の開閉スピード")]
    public float fingerSpeed = 4f;

    // ─────────────────────────────────────
    [Header("昇降設定")]
    [Tooltip("自動下降時の StretchRope の速度倍率")]
    public float descentSpeedMultiplier = 1.5f;
    [Tooltip("掴んでから上昇を開始するまでの待機秒数")]
    public float grabWaitSeconds = 0.5f;

    // ─────────────────────────────────────
    // 内部状態
    private ArmState _state = ArmState.Idle;
    private Vector2  _leverInput;
    private Vector3  _armInitialPos;
    private float    _grabTimer;

    private Quaternion[] _fingerDefaultRot;
    private Quaternion[] _fingerOpenRot;
    private bool         _wantFingerOpen = false;

    // ─────────────────────────────────────
    void Start()
    {
        if (armRoot != null) _armInitialPos = armRoot.position;

        // 爪の初期/開いた回転を記録
        if (fingerParts != null && fingerParts.Length > 0)
        {
            _fingerDefaultRot = new Quaternion[fingerParts.Length];
            _fingerOpenRot    = new Quaternion[fingerParts.Length];
            for (int i = 0; i < fingerParts.Length; i++)
            {
                if (fingerParts[i] == null) continue;
                _fingerDefaultRot[i] = fingerParts[i].localRotation;
                _fingerOpenRot[i]    = Quaternion.Euler(fingerOpenAngle, 0f, 0f) * _fingerDefaultRot[i];
            }
        }
    }

    // ─────────────────────────────────────
    /// <summary>LeverController から呼ばれる（x: -1〜1 左右, z: -1〜1 前後）</summary>
    public void SetLeverInput(float x, float z)
    {
        _leverInput = new Vector2(x, z);
    }

    /// <summary>ButtonController から呼ばれる：下降サイクルを開始</summary>
    public void StartDescentCycle()
    {
        if (_state != ArmState.Idle && _state != ArmState.Moving) return;
        _state = ArmState.Descending;
        _wantFingerOpen = true;
        stretchRope?.StartExternalDescent(descentSpeedMultiplier);
    }

    // ─────────────────────────────────────
    void Update()
    {
        UpdateMovement();
        UpdateRailFollow();
        UpdateFingers();
        UpdateStateMachine();
    }

    void UpdateMovement()
    {
        // 下降中・掴み中・上昇中はXZ移動しない
        if (_state == ArmState.Descending ||
            _state == ArmState.Grabbing   ||
            _state == ArmState.Ascending) return;
        if (armRoot == null) return;

        Vector3 pos = armRoot.position;
        pos.x += _leverInput.x * moveSpeed * Time.deltaTime;
        pos.z += _leverInput.y * moveSpeed * Time.deltaTime;

        pos.x = Mathf.Clamp(pos.x, _armInitialPos.x - moveRangeX, _armInitialPos.x + moveRangeX);
        pos.z = Mathf.Clamp(pos.z, _armInitialPos.z - moveRangeZ, _armInitialPos.z + moveRangeZ);

        armRoot.position = pos;
        _state = (_leverInput.sqrMagnitude > 0.01f) ? ArmState.Moving : ArmState.Idle;
    }

    void UpdateRailFollow()
    {
        if (armRoot == null) return;

        // Rail1: 左右（X方向）移動をアームに合わせる
        if (rail1 != null)
        {
            Vector3 p = rail1.position;
            p.x = armRoot.position.x;
            rail1.position = p;
        }

        // Rail2: 上下・奥手前（Z方向）移動をアームに合わせる
        if (rail2 != null)
        {
            Vector3 p = rail2.position;
            p.z = armRoot.position.z;
            rail2.position = p;
        }
    }

    void UpdateFingers()
    {
        if (fingerParts == null) return;
        for (int i = 0; i < fingerParts.Length; i++)
        {
            if (fingerParts[i] == null) continue;
            var target = _wantFingerOpen ? _fingerOpenRot[i] : _fingerDefaultRot[i];
            fingerParts[i].localRotation = Quaternion.Slerp(
                fingerParts[i].localRotation, target, Time.deltaTime * fingerSpeed);
        }
    }

    void UpdateStateMachine()
    {
        switch (_state)
        {
            case ArmState.Descending:
                // StretchRope が最大まで伸びたら「掴む」へ
                if (stretchRope != null && stretchRope.IsAtMax())
                {
                    _state = ArmState.Grabbing;
                    _wantFingerOpen = false;
                    _grabTimer = grabWaitSeconds;
                    stretchRope.StartExternalAscent(descentSpeedMultiplier);
                }
                break;

            case ArmState.Grabbing:
                _grabTimer -= Time.deltaTime;
                if (_grabTimer <= 0f)
                    _state = ArmState.Ascending;
                break;

            case ArmState.Ascending:
                // StretchRope が完全に縮んだら IDLE へ
                if (stretchRope != null && stretchRope.IsAtMin())
                    _state = ArmState.Idle;
                break;
        }
    }
}
