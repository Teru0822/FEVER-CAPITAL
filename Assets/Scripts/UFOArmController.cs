using UnityEngine;

/// <summary>
/// UFOキャッチャーの状態機械と全体制御。
/// 空のGameObjectにアタッチし、各Transformをインスペクターで設定してください。
/// </summary>
public class UFOArmController : MonoBehaviour
{
    public enum ArmState { Idle, Moving, OpeningClaw, Descending, Grabbing, Ascending }

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
    [Header("移動範囲の指定（Sceneビューで赤い枠が見えます）")]
    public Vector2 playAreaCenter = new Vector2(0f, 0f); // Xが左右、YがZ(奥手前)方向
    public Vector2 playAreaSize   = new Vector2(9f, 9f);

    // ─────────────────────────────────────
    [Header("爪（finger）設定")]
    [Tooltip("finger.001〜.004 を配列に設定してください")]
    public Transform[] fingerParts;
    [Tooltip("爪が開いたときのローカルX軸回転角度（開き幅）")]
    public float fingerOpenAngle = 40f;
    [Tooltip("逆に閉まる方向に動いてしまう指がある場合、ここのListにチェックを入れて反転させてください（0が001用、1が002用など）")]
    public bool[] invertFingerAngle;

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
    private float    _stateTimer; // 様々な待機タイマー兼用

    private Quaternion[] _fingerDefaultRot;
    private Quaternion[] _fingerOpenRot;
    private Quaternion[] _fingerCurrentBaseRot; // 開閉の純粋な回転を保持
    private bool         _wantFingerOpen = false;

    // ─────────────────────────────────────
    [Header("揺れ（Sway）設定")]
    [Tooltip("指以外の、一緒に揺らしたいパーツ（6番のロープなど）を指定します")]
    public Transform[] extraSwayParts;
    private Quaternion[] _extraSwayDefaultRot;

    [Header("ロープの揺れ設定（Extra Sway Parts用）")]
    public float extraSwaySensitivity = 2f;
    public float extraSwayDamping = 3f;
    public float extraSwaySpringForce = 15f;

    [Header("爪の揺れ設定（Finger Parts用）")]
    [Tooltip("爪の土台（finger）など、開閉アニメはないが爪と同じ強さで揺れてほしいパーツ")]
    public Transform[] clawBaseParts;
    private Quaternion[] _clawBaseDefaultRot;

    [UnityEngine.Serialization.FormerlySerializedAs("swaySensitivity")]
    public float clawSwaySensitivity = 2f;
    [UnityEngine.Serialization.FormerlySerializedAs("swayDamping")]
    public float clawSwayDamping = 3f;
    [UnityEngine.Serialization.FormerlySerializedAs("swaySpringForce")]
    public float clawSwaySpringForce = 15f;

    private Vector3 _lastWorldPos;
    
    // Extra Sway State
    private Vector3 _ropeSwayAngle;
    private Vector3 _ropeSwayVelocity;
    public Quaternion ropeSwayRot { get; private set; } = Quaternion.identity;

    // Claw Sway State
    private Vector3 _clawSwayAngle;
    private Vector3 _clawSwayVelocity;
    public Quaternion clawSwayRot { get; private set; } = Quaternion.identity;

    // ─────────────────────────────────────
    void Start()
    {
        if (armRoot != null) _armInitialPos = armRoot.position;

        // 爪の初期/開いた回転を記録
        if (fingerParts != null && fingerParts.Length > 0)
        {
            _fingerDefaultRot = new Quaternion[fingerParts.Length];
            _fingerOpenRot    = new Quaternion[fingerParts.Length];
            _fingerCurrentBaseRot = new Quaternion[fingerParts.Length];
            for (int i = 0; i < fingerParts.Length; i++)
            {
                if (fingerParts[i] == null) continue;
                _fingerDefaultRot[i] = fingerParts[i].localRotation;

                float angle = fingerOpenAngle;
                // インバート指定があれば角度を反転
                if (invertFingerAngle != null && i < invertFingerAngle.Length && invertFingerAngle[i])
                {
                    angle = -fingerOpenAngle;
                }

                _fingerOpenRot[i]    = Quaternion.Euler(angle, 0f, 0f) * _fingerDefaultRot[i];
                _fingerCurrentBaseRot[i] = _fingerDefaultRot[i];
            }
        }

        // その他揺らすパーツの初期回転を記録
        if (extraSwayParts != null && extraSwayParts.Length > 0)
        {
            _extraSwayDefaultRot = new Quaternion[extraSwayParts.Length];
            for (int i = 0; i < extraSwayParts.Length; i++)
            {
                if (extraSwayParts[i] != null)
                    _extraSwayDefaultRot[i] = extraSwayParts[i].localRotation;
            }
        }

        // 爪土台の初期回転を記録
        if (clawBaseParts != null && clawBaseParts.Length > 0)
        {
            _clawBaseDefaultRot = new Quaternion[clawBaseParts.Length];
            for (int i = 0; i < clawBaseParts.Length; i++)
            {
                if (clawBaseParts[i] != null)
                    _clawBaseDefaultRot[i] = clawBaseParts[i].localRotation;
            }
        }

        if (armRoot != null) _lastWorldPos = armRoot.position;
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
        
        // すぐ下降せず、まず爪を上に開くフェーズに入る
        _state = ArmState.OpeningClaw;
        _wantFingerOpen = true;
        _stateTimer = 1.0f; // 1秒間待機する
    }

    // ─────────────────────────────────────
    void Update()
    {
        UpdateMovement();
        UpdateSwayPhysics();
        UpdateRailFollow();
        UpdateFingersAndSway();
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

        // 手動で設定した赤い枠(Gizmos)の範囲内に強制クリップ
        float halfX = playAreaSize.x / 2f;
        float halfZ = playAreaSize.y / 2f;
        pos.x = Mathf.Clamp(pos.x, playAreaCenter.x - halfX, playAreaCenter.x + halfX);
        pos.z = Mathf.Clamp(pos.z, playAreaCenter.y - halfZ, playAreaCenter.y + halfZ);

        armRoot.position = pos;
        
        // コントロール中のみ状態をMovingにする（操作不可ステート時は維持）
        if (_state == ArmState.Idle || _state == ArmState.Moving)
        {
            _state = (_leverInput.sqrMagnitude > 0.01f) ? ArmState.Moving : ArmState.Idle;
        }
    }

    void UpdateSwayPhysics()
    {
        if (armRoot == null) return;
        if (Time.deltaTime == 0f) return;

        // 座標から現在の移動速度（Velocity）を取得
        Vector3 currentPos = armRoot.position;
        Vector3 currentVel = (currentPos - _lastWorldPos) / Time.deltaTime;
        _lastWorldPos = currentPos;

        // 【ロープ（Extra）側の揺れ計算】
        Vector3 ropeTargetSway = new Vector3(currentVel.z, 0f, currentVel.x) * extraSwaySensitivity;
        Vector3 ropeAngleDiff = ropeTargetSway - _ropeSwayAngle;
        Vector3 ropeSpringAccel = (ropeAngleDiff * extraSwaySpringForce) - (_ropeSwayVelocity * extraSwayDamping);
        _ropeSwayVelocity += ropeSpringAccel * Time.deltaTime;
        _ropeSwayAngle += _ropeSwayVelocity * Time.deltaTime;
        _ropeSwayAngle.x = Mathf.Clamp(_ropeSwayAngle.x, -50f, 50f);
        _ropeSwayAngle.z = Mathf.Clamp(_ropeSwayAngle.z, -50f, 50f);
        ropeSwayRot = Quaternion.Euler(_ropeSwayAngle.x, 0f, _ropeSwayAngle.z);

        // 【爪（Claw）側の揺れ計算】
        Vector3 clawTargetSway = new Vector3(currentVel.z, 0f, currentVel.x) * clawSwaySensitivity;
        Vector3 clawAngleDiff = clawTargetSway - _clawSwayAngle;
        Vector3 clawSpringAccel = (clawAngleDiff * clawSwaySpringForce) - (_clawSwayVelocity * clawSwayDamping);
        _clawSwayVelocity += clawSpringAccel * Time.deltaTime;
        _clawSwayAngle += _clawSwayVelocity * Time.deltaTime;
        _clawSwayAngle.x = Mathf.Clamp(_clawSwayAngle.x, -50f, 50f);
        _clawSwayAngle.z = Mathf.Clamp(_clawSwayAngle.z, -50f, 50f);
        clawSwayRot = Quaternion.Euler(_clawSwayAngle.x, 0f, _clawSwayAngle.z);
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

    void UpdateFingersAndSway()
    {
        // 爪（finger）に対する開閉と揺れの合成
        if (fingerParts != null && fingerParts.Length > 0)
        {
            for (int i = 0; i < fingerParts.Length; i++)
            {
                if (fingerParts[i] == null) continue;

                // 開閉アニメーションの補間（純粹なローカル状態）
                Quaternion targetBaseRot = _wantFingerOpen ? _fingerOpenRot[i] : _fingerDefaultRot[i];
                _fingerCurrentBaseRot[i] = Quaternion.Lerp(_fingerCurrentBaseRot[i], targetBaseRot, Time.deltaTime * fingerSpeed);

                // 1. 純粋なローカル回転（開閉）のみをセットする。
                // 1. 純粋なローカル回転（開閉）をセット
                fingerParts[i].localRotation = _fingerCurrentBaseRot[i];
                // 2. 爪用の個別揺れを適用
                fingerParts[i].rotation = clawSwayRot * fingerParts[i].rotation;
            }
        }

        // 爪土台に対する揺れの適用（Claw設定）
        if (clawBaseParts != null && clawBaseParts.Length > 0)
        {
            for (int i = 0; i < clawBaseParts.Length; i++)
            {
                if (clawBaseParts[i] == null) continue;
                clawBaseParts[i].localRotation = _clawBaseDefaultRot[i];
                clawBaseParts[i].rotation = clawSwayRot * clawBaseParts[i].rotation;
            }
        }

        // その他（6番ロープなど）に対する揺れの適用（Extra設定）
        if (extraSwayParts != null && extraSwayParts.Length > 0)
        {
            for (int i = 0; i < extraSwayParts.Length; i++)
            {
                if (extraSwayParts[i] == null) continue;

                // 1. 本来のローカル回転をセット
                extraSwayParts[i].localRotation = _extraSwayDefaultRot[i];
                // 2. ロープ用の個別揺れを適用
                extraSwayParts[i].rotation = ropeSwayRot * extraSwayParts[i].rotation;
            }
        }
    }

    /// <summary>
    /// 当たり判定スクリプト（UFOClawCollisionDetector）から「何かにぶつかった」時に呼ばれる
    /// </summary>
    public void OnClawCollided()
    {
        // 下降中に景品や床にぶつかったら、最大まで伸びきるのを待たずに強制的に「掴む＆上昇」へ移行する
        if (_state == ArmState.Descending)
        {
            _state = ArmState.Grabbing;
            _wantFingerOpen = false;
            _stateTimer = grabWaitSeconds; // 少し待ってから上昇する
            
            if (stretchRope != null)
            {
                stretchRope.StartExternalAscent(descentSpeedMultiplier);
            }
        }
    }

    void UpdateStateMachine()
    {
        switch (_state)
        {
            case ArmState.OpeningClaw:
                // 指定時間（1秒）待ってから下降をスタートする
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    _state = ArmState.Descending;
                    stretchRope?.StartExternalDescent(descentSpeedMultiplier);
                }
                break;

            case ArmState.Descending:
                // StretchRope が最大まで伸びたら「掴む」へ
                if (stretchRope != null && stretchRope.IsAtMax())
                {
                    _state = ArmState.Grabbing;
                    _wantFingerOpen = false;
                    _stateTimer = grabWaitSeconds;
                    stretchRope.StartExternalAscent(descentSpeedMultiplier);
                }
                break;

            case ArmState.Grabbing:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
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
