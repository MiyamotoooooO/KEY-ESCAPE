using System.Collections;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - 跳ね橋。レバーがオンになると、立っていた橋が、パタンと倒れて、道になる。オフに戻ると、また立ち上がる。
///
/// 組み上がる形（プレハブ LeverBridge）：
///   LeverBridge（このスクリプト＋Rigidbody2D）… 置いた位置が、支点（橋の根もと。倒れた時の、橋の上の面の高さ）
///     └ Deck … SpriteRenderer（絵：sketch_bridge）と BoxCollider2D（橋の当たり判定）
///
/// 使い方：
///   1. プレハブを、橋を架けたい場所の、手前のがけのふち（橋の上の面の高さ）に置く。
///   2. Length（橋の長さ）を、向こうのがけに届く長さにする。整数にすると、板がきれいに並ぶ。
///   3. 右へ倒すなら Extend Right をON、左へ倒すなら OFF。
///   4. レバー（Lever）の Targets に、この橋を入れる。
///
/// 動き：
///   ・オフ（初期）：立ち上がっている（Raised Angle の角度）。立っている間は、壁として扱われる。
///   ・オン：支点を中心に、倒れる（Lower Seconds の秒数。だんだん速く）。倒れ終わると、少し弾んで、カメラが揺れる。
///   ・オフに戻す：ゆっくり立ち上がる（Raise Seconds の秒数）。橋の上にいると、落ちる。
///   ・Down When On をOFFにすると、逆になる（オンで立ち上がる橋）。
/// 効果音：Lower Clip（倒れ始め）、Land Clip（倒れ終わり）、Raise Clip（立ち上がり）。空なら鳴らさない。
/// </summary>
public class LeverBridge : LeverReceiver
{
    [Header("橋")]
    [Tooltip("橋の長さ（ユニット）。整数にすると、板がきれいに並ぶ。")]
    public float length = 6f;
    [Tooltip("ONなら、支点から右へ倒れる。OFFなら、左へ倒れる。")]
    public bool extendRight = true;
    [Tooltip("立ち上がった時の角度（度）。90で、まっすぐ立つ。")]
    public float raisedAngle = 90f;
    [Tooltip("ONなら、レバーがオンの時に倒れて橋になる（ふつう）。OFFなら、逆（オンで立ち上がる）。")]
    public bool downWhenOn = true;

    [Header("動き")]
    [Tooltip("倒れるまでの秒数。")]
    public float lowerSeconds = 0.9f;
    [Tooltip("立ち上がるまでの秒数。")]
    public float raiseSeconds = 1.3f;
    [Tooltip("倒れ終わった時に、少し弾む角度（度）。0なら、弾まない。")]
    public float bounceAngle = 5f;
    [Tooltip("倒れ終わった時の、カメラの揺れの強さ。0なら、揺らさない。")]
    public float landShake = 0.08f;

    [Header("地面として扱う")]
    [Tooltip("ONなら、ゲームを始めるときに、橋の Layer を「Default」にそろえる。このゲームの主人公は、Default の物を「地面」として見つけるので、Ground レイヤーのままだと、橋の上で、ずっと空中のアニメーションになる。")]
    public bool useDefaultLayer = true;

    [Header("調べる用")]
    [Tooltip("ONなら、レバーから知らせが来るたびに、Console に出す。")]
    public bool debugLog = false;

    [Header("部品（プレハブに入っている）")]
    public Transform deck;
    public SpriteRenderer deckRenderer;
    public BoxCollider2D deckCollider;

    [Header("効果音（空なら鳴らさない）")]
    public AudioClip lowerClip;
    public AudioClip landClip;
    public AudioClip raiseClip;
    [Range(0f, 1f)] public float volume = 1f;

    private Rigidbody2D _rb;
    private float _angle;
    private Coroutine _anim;

    private float Sign { get { return extendRight ? 1f : -1f; } }

    private void Reset()
    {
        deck = transform.Find("Deck");
        if (deck != null)
        {
            deckRenderer = deck.GetComponent<SpriteRenderer>();
            deckCollider = deck.GetComponent<BoxCollider2D>();
        }
    }

    private void OnValidate()
    {
        if (length < 0.5f)
            length = 0.5f;

#if UNITY_EDITOR
        // OnValidate の中では、SpriteRenderer の Size を変えられない（警告が出る）ので、少しあとに回す。
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || deck == null || deckRenderer == null)
                return;
            ApplyShape();
            UnityEditor.EditorUtility.SetDirty(deck);
            UnityEditor.EditorUtility.SetDirty(deckRenderer);
            if (deckCollider != null)
                UnityEditor.EditorUtility.SetDirty(deckCollider);
        };
#endif
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // 主人公の「地面」の判定（CharacterController2D の Ground Check）は、Ground レイヤーを含まない。
        // 橋が Ground レイヤーだと、立っていても空中あつかいになるので、Default にそろえる。
        if (useDefaultLayer)
        {
            gameObject.layer = 0;
            if (deck != null)
                deck.gameObject.layer = 0;
        }

        // レバーから知らせが来るまでは、立ち上がった状態（オフ）。
        SetAngleNow(UpAngle());
    }

    private void Start()
    {
        // Awake の中では、SpriteRenderer の Size を変えられない（警告が出る）ので、ここで整える。
        // すでに合っていれば、何も変えない。
        ApplyShape();
    }

    /// <summary>長さと向きに合わせて、橋の絵と当たり判定を整える。</summary>
    public void ApplyShape()
    {
        if (deck == null || deckRenderer == null)
            return;

        float thick = deckRenderer.sprite != null ? deckRenderer.sprite.bounds.size.y : 0.7f;
        float dir = Sign;

        // 値が変わる時だけ、書き換える（同じ値を入れ直すと、警告が出るため）
        Vector2 size = new Vector2(length, thick);
        if (deckRenderer.drawMode != SpriteDrawMode.Tiled)
            deckRenderer.drawMode = SpriteDrawMode.Tiled;
        if (deckRenderer.tileMode != SpriteTileMode.Continuous)
            deckRenderer.tileMode = SpriteTileMode.Continuous;
        if ((deckRenderer.size - size).sqrMagnitude > 0.000001f)
            deckRenderer.size = size;
        if (deckRenderer.flipX != !extendRight)
            deckRenderer.flipX = !extendRight;

        // 上の面が、支点の高さ。支点から、橋が伸びる。
        deck.localPosition = new Vector3(dir * length * 0.5f, -thick * 0.5f, 0f);
        deck.localRotation = Quaternion.identity;

        if (deckCollider != null)
        {
            deckCollider.size = new Vector2(length, thick);
            deckCollider.offset = Vector2.zero;
        }
    }

    private float UpAngle() { return Sign * raisedAngle; }

    /// <summary>カメラが映す場所：倒れた橋の、真ん中。</summary>
    public override Vector3 FocusPosition
    {
        get
        {
            float thick = deckRenderer != null && deckRenderer.sprite != null ? deckRenderer.sprite.bounds.size.y : 0.7f;
            return transform.position + new Vector3(Sign * length * 0.5f, -thick * 0.5f, 0f);
        }
    }

    /// <summary>倒れる（または立ち上がる）動きが終わるまでの秒数。</summary>
    public override float GetEffectSeconds(bool on)
    {
        bool down = downWhenOn ? on : !on;
        if (down)
            return Mathf.Max(0.05f, lowerSeconds) + (bounceAngle > 0.01f ? 0.3f : 0f);
        return Mathf.Max(0.05f, raiseSeconds);
    }

    public override void SetLeverState(bool on, bool instant)
    {
        bool down = downWhenOn ? on : !on;
        float target = down ? 0f : UpAngle();

        if (debugLog)
            Debug.Log("[LeverBridge] " + name + "：レバーが" + (on ? "オン" : "オフ") + "になりました。橋は" + (down ? "倒れます" : "立ち上がります") + (instant ? "（演出なし）" : "") + "。", this);

        if (_anim != null)
        {
            StopCoroutine(_anim);
            _anim = null;
        }

        if (instant || !isActiveAndEnabled)
        {
            SetAngleNow(target);
            return;
        }

        if (Mathf.Approximately(_angle, target))
            return;
        _anim = StartCoroutine(down ? Lower() : Raise());
    }

    // ================= 動き =================

    private IEnumerator Lower()
    {
        PlaySfx(lowerClip);
        float start = _angle;
        float dur = Mathf.Max(0.05f, lowerSeconds);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Mathf.Clamp01(t);
            k = k * k;                        // だんだん速く
            _angle = Mathf.Lerp(start, 0f, k);
            yield return null;
        }
        SetAngleNow(0f);

        // 倒れ終わった：音・カメラの揺れ・少し弾む
        PlaySfx(landClip);
        if (landShake > 0f)
        {
            CameraFollow cam = FindObjectOfType<CameraFollow>();
            if (cam != null)
                cam.ShakeCamera(0.2f, landShake);
        }
        if (bounceAngle > 0.01f)
        {
            float bt = 0f;
            while (bt < 1f)
            {
                bt += Time.deltaTime / 0.28f;
                _angle = Sign * bounceAngle * Mathf.Sin(Mathf.Clamp01(bt) * Mathf.PI);
                yield return null;
            }
        }
        SetAngleNow(0f);
        _anim = null;
    }

    private IEnumerator Raise()
    {
        PlaySfx(raiseClip);
        float start = _angle;
        float target = UpAngle();
        float dur = Mathf.Max(0.05f, raiseSeconds);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            _angle = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        SetAngleNow(target);
        _anim = null;
    }

    private void SetAngleNow(float angle)
    {
        _angle = angle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        if (_rb != null)
            _rb.rotation = angle;
    }

    private void FixedUpdate()
    {
        // 物理の時間で、橋の角度を動かす（プレイヤーとの当たりが、なめらかになる）
        if (_rb != null && _anim != null)
            _rb.MoveRotation(_angle);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null)
            AudioManager.PlaySE(clip, volume);
    }

    // ================= シーン画面の目印 =================

    private void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position;
        float dir = extendRight ? 1f : -1f;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);   // 倒れた時の橋
        Vector3 down = p + new Vector3(dir * length, 0f, 0f);
        Gizmos.DrawLine(p, down);
        Gizmos.DrawWireSphere(down, 0.15f);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);   // 立った時の橋
        Vector3 up = p + Quaternion.Euler(0f, 0f, Sign * raisedAngle) * new Vector3(dir * length, 0f, 0f);
        Gizmos.DrawLine(p, up);
        Gizmos.DrawWireSphere(p, 0.12f);
    }
}
