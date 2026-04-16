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

    [Tooltip("揺れの強さ（感度）")]
    public float swaySensitivity = 2f;
    [Tooltip("揺れが静まるまでの時間（ダンピング）")]
    public float swayDamping = 3f;
    [Tooltip("振り子の戻る力（バネの強さ）")]
    public float swaySpringForce = 15f;

    private Vector3 _lastWorldPos;
    private Vector3 _velocity;
    private Vector3 _swayAngle;
    private Vector3 _swayVelocity;
    public Quaternion currentSwayRot { get; private set; } = Quaternion.identity;

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

        // 【移動ベースの揺れ】
        // アームが移動している時、空気抵抗や慣性のように「移動方向と逆」に傾かせる目標角度
        Vector3 targetSway = new Vector3(currentVel.z, 0f, -currentVel.x) * swaySensitivity;

        // 【バネ（スプリング）の単振動シミュレーション】
        // 常に targetSway に向かって引っ張られ、行き過ぎて揺れる
        Vector3 angleDiff = targetSway - _swayAngle;
        Vector3 springAccel = (angleDiff * swaySpringForce) - (_swayVelocity * swayDamping);

        _swayVelocity += springAccel * Time.deltaTime;
        _swayAngle += _swayVelocity * Time.deltaTime;

        // 暴れすぎないように最大角度を制限
        _swayAngle.x = Mathf.Clamp(_swayAngle.x, -50f, 50f);
        _swayAngle.z = Mathf.Clamp(_swayAngle.z, -50f, 50f);

        currentSwayRot = Quaternion.Euler(_swayAngle.x, 0f, _swayAngle.z);
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

                // 開閉アニメーションの補間（揺れを含まないピュアな状態）
                Quaternion targetBaseRot = _wantFingerOpen ? _fingerOpenRot[i] : _fingerDefaultRot[i];
                _fingerCurrentBaseRot[i] = Quaternion.Lerp(_fingerCurrentBaseRot[i], targetBaseRot, Time.deltaTime * fingerSpeed);

                // ピュアな開閉状態に、物理的な揺れ（Sway）を合成してセット
                fingerParts[i].localRotation = currentSwayRot * _fingerCurrentBaseRot[i];
            }
        }

        // その他（6番ロープなど）に対する揺れの適用
        if (extraSwayParts != null && extraSwayParts.Length > 0)
        {
            for (int i = 0; i < extraSwayParts.Length; i++)
            {
                if (extraSwayParts[i] == null) continue;
                extraSwayParts[i].localRotation = currentSwayRot * _extraSwayDefaultRot[i];
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
