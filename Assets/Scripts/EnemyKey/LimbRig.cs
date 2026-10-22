using UnityEngine;
using TMPro;

/// <summary>
/// 敵キー（手足の生えたキー）の手足を、コードだけで動かす。
///
/// 手足のスプライト自体は変えず、回転・位置・伸び縮みだけで
/// 「待機・歩き・空中・被ダメージ・攻撃・鎮静（倒れる）」を作る。
/// 状態は次から読み取る（Enemy.cs / Ally.cs は書き換えない）:
///   ・Rigidbody2D の速度        → 歩き / 空中
///   ・Enemy.life / Ally.life    → 被ダメージ（減った瞬間）/ 鎮静（0以下）
///   ・Animator の bool          → "Attack"（Allyの攻撃）/ "Hit" / "IsDead"
///
/// 角度の約束:
///   腕は「外側へ持ち上げる」向きがプラス（左腕は内部で符号を反転する）。
///   脚はそのまま回転角（プラス = 反時計回り）。
/// 距離の単位はすべてローカル（親の拡大率をかける前）の値。
/// </summary>
[DisallowMultipleComponent]
public class LimbRig : MonoBehaviour
{
    /// <summary>外から指定できる動き（SetMotion）。</summary>
    public enum KeyMotion
    {
        None,   // 普段の動き（待機・歩きなど）
        Talk,   // 話す（身振り・うなずき）
        Cheer,  // 喜ぶ（両腕を上げて跳ねる）
        Sway,   // ゆらゆら（体を左右に傾けて、腕を交互に振る）
        Hop,    // ぴょんぴょん（リズムよく連続ジャンプ）
        Wave,   // 手を振る
        Disco,  // ディスコ（腕を交互に上下、体を斜めに傾ける）
    }

    [Header("パーツ（メニューのプレハブ作成で自動設定される）")]
    [Tooltip("体と手足をまとめた親。体の揺れ・傾き・倒れる動きはここに掛かる（原点は足元）")]
    public Transform visual;
    public Transform armL;
    public Transform armR;
    public Transform legL;
    public Transform legR;

    [Header("向き")]
    [Tooltip("キャラが右向きなら true（Allyタイプ）。左向きなら false（Enemyタイプ）。攻撃・倒れるときに傾く向きに使う")]
    public bool spriteFacesRight = false;

    [Header("待機のみ（会話キーなどの動かないキャラ用）")]
    [Tooltip("true: 速度・life・Animatorを一切見ず、待機の動き（呼吸と腕の揺れ）だけをする")]
    public bool idleOnly = false;

    [Header("喜び（ステージクリアの演出用）")]
    [Tooltip("喜ぶ時に、両腕を上げる角度。大きいほど真上に近づく")]
    public float cheerArmAngle = 160f;
    [Tooltip("喜ぶ時に、跳ねる高さ（ローカル単位）")]
    public float cheerHop = 0.12f;

    [Header("歩き")]
    [Tooltip("1歩ぶんの距離。小さいほど足の回転が速くなる")]
    public float strideLength = 1.0f;
    [Tooltip("足の振りの最高速度（往復/秒）。速い敵でも足がバタバタしすぎないように")]
    public float maxCyclesPerSecond = 3.0f;
    [Tooltip("この速さ以下は「止まっている」とみなす")]
    public float moveThreshold = 0.15f;
    public float legSwing = 28f;
    public float armSwing = 22f;
    [Tooltip("腕の基本の開き（度）")]
    public float armRest = 8f;
    [Tooltip("歩くときの体の上下（ローカル単位）")]
    public float bobHeight = 0.06f;
    public float walkTilt = 3f;

    [Header("待機")]
    [Tooltip("待機の動き全体の倍率。大きいほど呼吸・腕の揺れ・足踏み・傾き・上下が目立つ。0で完全に静止")]
    public float idleStrength = 3f;
    public float breathAmount = 0.025f;
    public float breathSpeed = 2.2f;
    public float armSway = 4f;

    [Header("空中")]
    [Tooltip("縦の速さがこれを超えたら空中とみなす")]
    public float airVelocity = 0.6f;
    public float airArmAngle = 55f;

    [Header("被ダメージ")]
    public float hitDuration = 0.35f;
    [Tooltip("ダメージを受けた瞬間に、白い面がこの色に染まる")]
    public Color hitTint = new Color(1f, 0.55f, 0.55f, 1f);

    [Header("攻撃（Ally用）")]
    public float attackDuration = 0.4f;
    [Tooltip("遠距離攻撃（弾を投げる）の腕振りにかける秒数")]
    public float throwDuration = 0.35f;

    [Header("話す（SetTalk(true) の間。説明パネルのキーなど）")]
    [Tooltip("身振りをする腕を持ち上げる角度（度）")]
    public float talkGestureAngle = 40f;
    [Tooltip("身振りの速さ")]
    public float talkGestureSpeed = 5f;
    [Tooltip("話すリズムに合わせて跳ねる高さ（ローカル単位）")]
    public float talkHop = 0.05f;
    [Tooltip("話すリズムに合わせて伸び縮みする量")]
    public float talkSquash = 0.03f;
    [Tooltip("うなずく角度（度）")]
    public float talkNod = 4f;

    [Header("踊り（SetMotion で Sway / Hop / Wave / Disco にしたとき）")]
    [Tooltip("踊りの大きさ。1が標準。大きいほど、腕も体も大きく動く")]
    public float danceIntensity = 1f;
    [Tooltip("踊りの速さ。1が標準")]
    public float danceSpeed = 1f;

    [Header("鎮静（倒れる）")]
    [Tooltip("倒れる角度（度）")]
    public float deadTilt = 88f;
    [Tooltip("倒れたキーが地面にめり込まないように持ち上げる量（ローカル単位）")]
    public float deadLift = 0.47f;
    [Tooltip("倒れてから消え始めるまでの秒数")]
    public float fadeDelay = 1.6f;
    [Tooltip("消えるまでにかける秒数")]
    public float fadeDuration = 1.2f;

    [Header("動きのなめらかさ")]
    public float poseSharpness = 24f;

    [Header("Animatorの目印")]
    [Tooltip("Animatorの Attack / Hit を読んだら false に戻す（Enemy.cs・Ally.cs は戻さないため）")]
    public bool consumeAnimatorFlags = true;

    // ---- 参照 ----
    private Rigidbody2D _rb;
    private Enemy _enemy;
    private Ally _ally;
    private Animator _anim;
    private SpriteRenderer[] _sprites;
    private TextMeshPro[] _labels;

    private bool _hasLife;
    private float _lastLife;
    private bool _hasDeadFlag;
    private bool _hasHitFlag;
    private bool _hasAttackFlag;

    // ---- 状態 ----
    private Vector3 _visualBasePos;
    private Vector3 _visualBaseScale = Vector3.one;
    private float _seed;
    private float _phase;
    private float _moveWeight;
    private float _hitTimer;
    private float _attackTimer;
    private float _throwTimer;
    private float _clock;
    private float _deadTime;
    private float _hitSign = 1f;
    private bool _tintActive;

    // ---- 現在のポーズ（なめらかに追従させる値）----
    private float _aArmL;
    private float _aArmR;
    private float _aLegL;
    private float _aLegR;
    private float _tilt;
    private float _lift;
    private float _squash = 1f;

    private void Awake()
    {
        _rb = GetComponentInParent<Rigidbody2D>();
        _enemy = GetComponentInParent<Enemy>();
        _ally = GetComponentInParent<Ally>();
        _anim = GetComponentInParent<Animator>();

        if (idleOnly)
        {
            _rb = null;
            _enemy = null;
            _ally = null;
            _anim = null;
        }

        _seed = Random.value * 10f;

        if (visual == null) visual = FindChildByName("Visual");
        if (armL == null) armL = FindChildByName("ArmL");
        if (armR == null) armR = FindChildByName("ArmR");
        if (legL == null) legL = FindChildByName("LegL");
        if (legR == null) legR = FindChildByName("LegR");

        _sprites = GetComponentsInChildren<SpriteRenderer>(true);

        var limbList = new System.Collections.Generic.List<SpriteRenderer>();
        Transform[] limbRoots = { armL, armR, legL, legR };
        for (int i = 0; i < limbRoots.Length; i++)
        {
            if (limbRoots[i] != null)
                limbList.AddRange(limbRoots[i].GetComponentsInChildren<SpriteRenderer>(true));
        }
        _limbSprites = limbList.ToArray();
        _labels = GetComponentsInChildren<TextMeshPro>(true);

        if (visual != null)
        {
            _visualBasePos = visual.localPosition;
            _visualBaseScale = visual.localScale;
        }

        if (_enemy != null)
        {
            _hasLife = true;
            _lastLife = _enemy.life;
        }
        else if (_ally != null)
        {
            _hasLife = true;
            _lastLife = _ally.life;
        }

        if (_anim != null && _anim.runtimeAnimatorController != null)
        {
            _hasDeadFlag = HasBoolParam("IsDead");
            _hasHitFlag = HasBoolParam("Hit");
            _hasAttackFlag = HasBoolParam("Attack");
        }

        _aArmR = armRest;
        _aArmL = -armRest;
    }

    // ---- 外から動かす（ステージクリアの演出用） ----
    private bool _manual;
    private float _manualVx;
    private bool _cheer;
    private bool _talk;

    // 手足だけを薄くする（キー獲得の演出：手足が消えて、キーだけが残る）
    private SpriteRenderer[] _limbSprites;
    private float _limbAlpha = 1f;
    private float _limbFadeSpeed;
    private bool _limbFading;
    private KeyMotion _extraMotion = KeyMotion.None;

    /// <summary>
    /// 速度を外から指定して、歩く・走る動きをさせる（待機専用のキーでも使える）。
    /// 0を渡すと止まる。元に戻すには、ClearManualVelocityを呼ぶ。
    /// </summary>
    public void SetManualVelocity(float vx)
    {
        _manual = true;
        _manualVx = vx;
    }

    public void ClearManualVelocity()
    {
        _manual = false;
        _manualVx = 0f;
    }

    /// <summary>喜びのポーズ（両腕を上げて、小さく跳ねる）の、オン／オフ。</summary>
    public void SetCheer(bool on)
    {
        _cheer = on;
    }

    /// <summary>話しかけているような動き（身振り・うなずき・小さく跳ねる）の、オン／オフ。</summary>
    public void SetTalk(bool on)
    {
        _talk = on;
    }

    /// <summary>
    /// 動きを1つ指定する（Talk / Cheer / Sway / Hop / Wave / Disco）。None で普段の動きに戻る。
    /// SetTalk / SetCheer の指定も、ここで置き換わる。
    /// </summary>
    public void SetMotion(KeyMotion motion)
    {
        _talk = motion == KeyMotion.Talk;
        _cheer = motion == KeyMotion.Cheer;
        _extraMotion = (motion == KeyMotion.Sway || motion == KeyMotion.Hop
                        || motion == KeyMotion.Wave || motion == KeyMotion.Disco) ? motion : KeyMotion.None;
    }

    /// <summary>手足（腕・脚）だけを、duration秒かけて透明にする。キーの本体は、そのまま残る。</summary>
    public void FadeOutLimbs(float duration)
    {
        _limbFadeSpeed = duration > 0f ? 1f / duration : 1000f;
        _limbFading = true;
    }

    /// <summary>手足が、すっかり消えたか。</summary>
    public bool LimbsGone
    {
        get { return _limbAlpha <= 0.001f; }
    }

    /// <summary>外から攻撃の動きを呼びたいとき用（Animatorを使わない場合）。</summary>
    public void PlayAttack()
    {
        _attackTimer = attackDuration;
    }

    /// <summary>遠距離攻撃（弾を投げる）の腕振りを再生する。Ally.RangeAttack() から呼ばれる。</summary>
    public void PlayThrow()
    {
        _throwTimer = throwDuration;
    }

    /// <summary>外から被ダメージの動きを呼びたいとき用。</summary>
    public void PlayHit()
    {
        _hitTimer = hitDuration;
        _hitSign = Random.value < 0.5f ? -1f : 1f;
    }

    private void Update()
    {
        // ゲームが GamePause で止まっている間は、時間が止まっていても実時間でアニメーションだけ流す。
        // （速度の値は止まる直前のまま残るので、走っていたらその場で走りのループになる）
        float dt = GamePause.IsPaused ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        // ---------- 状態を読む ----------
        float vx = _manual ? _manualVx : (_rb != null ? _rb.velocity.x : 0f);
        float vy = _rb != null ? _rb.velocity.y : 0f;
        bool dead = false;

        if (_hasLife)
        {
            float life = CurrentLife();
            if (life < _lastLife - 0.001f && life > 0f)
                PlayHit();
            _lastLife = life;
            dead = life <= 0f;
        }

        if (_anim != null)
        {
            if (_hasDeadFlag && _anim.GetBool("IsDead"))
                dead = true;

            if (_hasHitFlag && _anim.GetBool("Hit"))
            {
                if (_hitTimer <= 0f) PlayHit();
                if (consumeAnimatorFlags) _anim.SetBool("Hit", false);
            }

            if (_hasAttackFlag && _anim.GetBool("Attack"))
            {
                if (_attackTimer <= 0f) PlayAttack();
                if (consumeAnimatorFlags) _anim.SetBool("Attack", false);
            }
        }

        if (_hitTimer > 0f) _hitTimer = Mathf.Max(0f, _hitTimer - dt);
        if (_attackTimer > 0f) _attackTimer = Mathf.Max(0f, _attackTimer - dt);
        if (_throwTimer > 0f) _throwTimer = Mathf.Max(0f, _throwTimer - dt);

        bool airborne = Mathf.Abs(vy) > airVelocity;
        bool moving = Mathf.Abs(vx) > moveThreshold;

        // 歩きの位相は「進んだ距離」に合わせて進める（止まれば止まる）
        if (moving && !airborne)
        {
            float cycles = Mathf.Min(Mathf.Abs(vx) / Mathf.Max(0.01f, strideLength), maxCyclesPerSecond);
            _phase += cycles * Mathf.PI * 2f * dt;
        }
        _moveWeight = Mathf.MoveTowards(_moveWeight, (moving && !airborne && !dead) ? 1f : 0f, dt * 8f);

        float fwd = spriteFacesRight ? -1f : 1f; // 前へ傾ける回転の向き
        _clock += dt;
        float time = _clock + _seed;

        // ---------- 目標ポーズを決める ----------
        float tArmR;
        float tArmL;
        float tLegL;
        float tLegR;
        float tTilt = 0f;
        float tLift = 0f;
        float tSquash = 1f;

        if (dead)
        {
            _deadTime += dt;
            float twitch = Mathf.Exp(-_deadTime * 2.5f) * Mathf.Sin(_deadTime * 22f);
            float armOut = 95f + 15f * twitch;
            tArmR = armOut;
            tArmL = -armOut;
            tLegL = 35f + 12f * twitch;
            tLegR = -22f - 12f * twitch;
            tTilt = -fwd * deadTilt;
            tLift = deadLift;
        }
        else if (_hitTimer > 0f)
        {
            float k = _hitTimer / Mathf.Max(0.01f, hitDuration);
            float flail = Mathf.Sin(time * 40f);
            tArmR = 70f + 18f * flail;
            tArmL = -(70f - 18f * flail);
            tLegL = 18f * flail;
            tLegR = -18f * flail;
            tTilt = _hitSign * 12f * k;
        }
        else if (_cheer)
        {
            // 喜び：両腕を上げて、小さく跳ねる
            float bounce = Mathf.Abs(Mathf.Sin(time * 7f));
            float wave = Mathf.Sin(time * 14f);
            tArmR = cheerArmAngle + 10f * wave;
            tArmL = -(cheerArmAngle - 10f * wave);
            tLegL = 6f * wave;
            tLegR = -6f * wave;
            tTilt = 4f * Mathf.Sin(time * 7f);
            tLift = cheerHop * bounce;
        }
        else if (_extraMotion != KeyMotion.None)
        {
            // 脚は、動きによっては使わないので、先に0（まっすぐ）にしておく
            tLegL = 0f;
            tLegR = 0f;

            float k = danceIntensity;
            float dt2 = time * danceSpeed;
            switch (_extraMotion)
            {
                case KeyMotion.Sway:
                {
                    float sw = Mathf.Sin(dt2 * 3.5f);
                    tArmR = 55f + 35f * k * sw;
                    tArmL = -(55f - 35f * k * sw);
                    tLegL = 10f * k * sw;
                    tLegR = -10f * k * sw;
                    tTilt = 12f * k * sw;
                    tLift = 0.03f * k * Mathf.Abs(sw);
                    break;
                }
                case KeyMotion.Hop:
                {
                    float b = Mathf.Abs(Mathf.Sin(dt2 * 8f));
                    float flap = Mathf.Sin(dt2 * 16f);
                    tArmR = 50f + 15f * k * flap;
                    tArmL = -(50f - 15f * k * flap);
                    tLegL = 14f * k * b;
                    tLegR = -14f * k * b;
                    tLift = 0.2f * k * b;
                    tSquash = 1f + 0.05f * k * (0.5f - b);
                    break;
                }
                case KeyMotion.Wave:
                {
                    tArmR = 150f + 20f * k * Mathf.Sin(dt2 * 12f);
                    tArmL = -(armRest + 4f);
                    tTilt = 5f * k * Mathf.Sin(dt2 * 3f);
                    tLift = 0.02f * k * Mathf.Abs(Mathf.Sin(dt2 * 3f));
                    break;
                }
                default: // Disco
                {
                    float s = Mathf.Sin(dt2 * 6f);
                    tArmR = 90f + 80f * k * s;
                    tArmL = -(90f - 80f * k * s);
                    tLegL = 16f * k * s;
                    tLegR = -16f * k * s;
                    tTilt = 14f * k * Mathf.Sin(dt2 * 3f);
                    tLift = 0.06f * k * Mathf.Abs(s);
                    break;
                }
            }
        }
        else
        {
            float swing = Mathf.Sin(_phase);
            float w = _moveWeight;

            if (airborne)
            {
                float flutter = Mathf.Sin(time * 14f) * 8f;
                tArmR = airArmAngle + flutter;
                tArmL = -(airArmAngle - flutter);
                tLegL = 18f;
                tLegR = -22f;
                tSquash = 1f + Mathf.Clamp(vy * 0.012f, -0.04f, 0.06f);
            }
            else
            {
                float idle = (1f - w) * idleStrength;
                float armRVal = armRest + w * armSwing * swing + idle * armSway * Mathf.Sin(time * 2.4f);
                float armLVal = armRest - w * armSwing * swing + idle * armSway * Mathf.Sin(time * 2.4f + 1.3f);
                tArmR = armRVal;
                tArmL = -armLVal;
                tLegR = w * legSwing * swing + idle * 1.5f * Mathf.Sin(time * 2.0f);
                tLegL = -w * legSwing * swing - idle * 1.5f * Mathf.Sin(time * 2.0f);
                tTilt = w * walkTilt * swing + idle * 0.8f * Mathf.Sin(time * 1.6f);
                tLift = w * bobHeight * Mathf.Abs(swing) + idle * 0.02f * (0.5f + 0.5f * Mathf.Sin(time * (breathSpeed + 0.4f)));
                tSquash = 1f + idle * breathAmount * Mathf.Sin(time * (breathSpeed + 0.4f));
            }

            // 話す：片腕で身振りをしながら、リズムに合わせて小さく跳ね、うなずく
            if (_talk && !airborne)
            {
                float gesture = 0.5f + 0.5f * Mathf.Sin(time * talkGestureSpeed);
                tArmR = armRest + talkGestureAngle * gesture;
                tArmL = -(armRest + 6f * Mathf.Sin(time * 3f + 1f));
                tTilt += talkNod * Mathf.Sin(time * talkGestureSpeed * 0.9f);
                tLift += talkHop * Mathf.Abs(Mathf.Sin(time * 10f));
                tSquash += talkSquash * Mathf.Sin(time * 20f);
            }

            // 攻撃は腕と傾きだけ上書きする（脚は歩き/待機のまま）
            if (_attackTimer > 0f)
            {
                float p = 1f - _attackTimer / Mathf.Max(0.01f, attackDuration);
                float v;
                if (p < 0.45f)
                {
                    float e = EaseOut(p / 0.45f);
                    v = Mathf.Lerp(armRest, 125f, e);
                    tTilt = -fwd * 6f * e;
                }
                else
                {
                    float s = (p - 0.45f) / 0.55f;
                    v = Mathf.Lerp(125f, -25f, s * s);
                    tTilt = Mathf.Lerp(-fwd * 6f, fwd * 10f, s);
                }
                tArmR = v;
                tArmL = -v;
            }

            // 弾を投げる: 向いている側の腕を頭の後ろへ構えてから、すばやく振り下ろす。反対の腕は軽くバランスを取る
            if (_throwTimer > 0f)
            {
                float tp = 1f - _throwTimer / Mathf.Max(0.01f, throwDuration);
                float fwdArm;
                float backArm;
                if (tp < 0.2f)
                {
                    float c = EaseOut(tp / 0.2f);
                    fwdArm = Mathf.Lerp(armRest, 150f, c);
                    backArm = Mathf.Lerp(armRest, armRest + 25f, c);
                    tTilt = -fwd * 8f * c;
                }
                else
                {
                    float q = (tp - 0.2f) / 0.8f;
                    float sm = q * q * (3f - 2f * q);
                    fwdArm = Mathf.Lerp(150f, -15f, sm);
                    backArm = Mathf.Lerp(armRest + 25f, armRest, sm);
                    tTilt = Mathf.Lerp(-fwd * 8f, fwd * 12f, sm);
                }

                if (spriteFacesRight)
                {
                    tArmR = fwdArm;
                    tArmL = -backArm;
                }
                else
                {
                    tArmL = -fwdArm;
                    tArmR = backArm;
                }
            }
        }

        // ---------- なめらかに追従 ----------
        float sharp = _hitTimer > 0f ? poseSharpness * 2f
                    : (_throwTimer > 0f || _attackTimer > 0f) ? poseSharpness * 2.5f
                    : poseSharpness;
        float f = 1f - Mathf.Exp(-sharp * dt);
        float bodyF = dead ? 1f - Mathf.Exp(-9f * dt) : f;

        _aArmR += (tArmR - _aArmR) * f;
        _aArmL += (tArmL - _aArmL) * f;
        _aLegL += (tLegL - _aLegL) * f;
        _aLegR += (tLegR - _aLegR) * f;
        _tilt += (tTilt - _tilt) * bodyF;
        _lift += (tLift - _lift) * bodyF;
        _squash += (tSquash - _squash) * f;

        // ---------- 反映 ----------
        if (armR != null) armR.localRotation = Quaternion.Euler(0f, 0f, _aArmR);
        if (armL != null) armL.localRotation = Quaternion.Euler(0f, 0f, _aArmL);
        if (legL != null) legL.localRotation = Quaternion.Euler(0f, 0f, _aLegL);
        if (legR != null) legR.localRotation = Quaternion.Euler(0f, 0f, _aLegR);

        if (visual != null)
        {
            visual.localRotation = Quaternion.Euler(0f, 0f, _tilt);
            visual.localPosition = _visualBasePos + new Vector3(0f, _lift, 0f);
            float sx = 1f - (_squash - 1f) * 0.5f;
            visual.localScale = new Vector3(_visualBaseScale.x * sx, _visualBaseScale.y * _squash, _visualBaseScale.z);
        }

        ApplyColors(dead);

        // 手足だけを薄くする（ほかの色の指定のあとに、透明度だけ上書きする）
        if (_limbFading || _limbAlpha < 1f)
        {
            if (_limbFading)
                _limbAlpha = Mathf.MoveTowards(_limbAlpha, 0f, _limbFadeSpeed * dt);
            if (_limbSprites != null)
            {
                for (int i = 0; i < _limbSprites.Length; i++)
                {
                    if (_limbSprites[i] == null) continue;
                    Color lc = _limbSprites[i].color;
                    lc.a = _limbAlpha;
                    _limbSprites[i].color = lc;
                }
            }
        }
    }

    private void ApplyColors(bool dead)
    {
        float k = hitDuration > 0f ? Mathf.Clamp01(_hitTimer / hitDuration) : 0f;
        float alpha = 1f;
        if (dead)
            alpha = 1f - Mathf.Clamp01((_deadTime - fadeDelay) / Mathf.Max(0.01f, fadeDuration));

        bool needed = k > 0f || alpha < 1f || _tintActive;
        if (!needed)
            return;

        Color c = Color.Lerp(Color.white, hitTint, k);
        c.a = alpha;

        if (_sprites != null)
        {
            for (int i = 0; i < _sprites.Length; i++)
                if (_sprites[i] != null) _sprites[i].color = c;
        }

        if (_labels != null)
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] == null) continue;
                Color tc = _labels[i].color;
                tc.a = alpha;
                _labels[i].color = tc;
            }
        }

        _tintActive = k > 0f || alpha < 1f;
    }

    private float CurrentLife()
    {
        if (_enemy != null) return _enemy.life;
        if (_ally != null) return _ally.life;
        return 1f;
    }

    private bool HasBoolParam(string name)
    {
        AnimatorControllerParameter[] ps = _anim.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == name && ps[i].type == AnimatorControllerParameterType.Bool)
                return true;
        return false;
    }

    private Transform FindChildByName(string childName)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == childName) return all[i];
        return null;
    }

    private static float EaseOut(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - (1f - x) * (1f - x);
    }
}
