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

    /// <summary>外から攻撃の動きを呼びたいとき用（Animatorを使わない場合）。</summary>
    public void PlayAttack()
    {
        _attackTimer = attackDuration;
    }

    /// <summary>外から被ダメージの動きを呼びたいとき用。</summary>
    public void PlayHit()
    {
        _hitTimer = hitDuration;
        _hitSign = Random.value < 0.5f ? -1f : 1f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return; // ポーズ中（timeScale 0）は止める

        // ---------- 状態を読む ----------
        float vx = _rb != null ? _rb.velocity.x : 0f;
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
        float time = Time.time + _seed;

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
        }

        // ---------- なめらかに追従 ----------
        float sharp = _hitTimer > 0f ? poseSharpness * 2f : poseSharpness;
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
