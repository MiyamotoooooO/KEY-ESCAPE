using UnityEngine;

/// <summary>
/// KEY ESCAPE - レーザーの発射口。一定の間隔で、「オフ → 予告 → 発射 → オフ」をくり返す。
///
/// 使い方：
///   1. メニュー「Tools > KEY ESCAPE > ステージ部品 > レーザーのプレハブを作る」で、プレハブを作る。
///   2. プレハブをシーンに置く。レーザーが出る向きは、このオブジェクトの「右」（赤い矢印、Rotation の Z で回す）。
///      下に向けたいときは Z = -90、上は Z = 90、左は Z = 180。
///   3. タイミングは、Inspector の「タイミング」で決める。
///
/// 動き：
///   ・オフ：何も出ない。
///   ・予告：発射口が光って、細い点滅する線が出る。この間にくぐり抜けられる。
///   ・発射：太いレーザーが出る。触れると、トゲと同じようにダメージを受けて、はじかれる。
///   ・レーザーは、地面や壁など、固い物に当たると、そこで止まる（動く橋なども同じ）。主人公は、止めない。
///
/// 効果音（Inspector の「効果音」に、音を入れる。空なら鳴らさない。遠いほど小さくなる）。
/// </summary>
public class LaserEmitter : MonoBehaviour
{
    [Header("タイミング（秒）")]
    [Tooltip("オフの間の長さ。")]
    public float offSeconds = 2f;

    [Tooltip("予告の長さ。発射口が光って、細い線が点滅する。")]
    public float warnSeconds = 0.8f;

    [Tooltip("発射している長さ。")]
    public float fireSeconds = 1.2f;

    [Tooltip("ゲームを始めてから、最初のオフを、この秒数だけ長くする。レーザーが何台かあるとき、タイミングをずらすのに使う。")]
    public float startDelay = 0f;

    [Header("ビーム")]
    [Tooltip("発射口の位置。このオブジェクトの中心から、右へ、この距離。")]
    public float muzzleOffset = 0.6f;

    [Tooltip("レーザーの一番長い長さ（ワールド単位）。固い物に当たらなければ、この長さまで出る。")]
    public float maxLength = 14f;

    [Tooltip("発射中の、レーザーの太さ。当たり判定も、これに合わせる。")]
    public float beamWidth = 0.3f;

    [Tooltip("レーザーを止める物の Layer。初期は、すべて（地面・壁・足場など。主人公と、すり抜ける物は除く）。")]
    public LayerMask blockLayers = ~0;

    [Header("ダメージ")]
    [Tooltip("1回当たった時のダメージ。2 で、ハート1個分。")]
    public float damage = 2f;

    [Tooltip("当たったあとの無敵の秒数。0以下なら、いつもの被弾と同じ（1秒）。")]
    public float invincibleTimeAfterHit = -1f;

    [Tooltip("HPが0になった時の死因（結果画面の一言コメントの種類）。")]
    public DeathCause deathCause = DeathCause.Other;

    [Header("見た目の色")]
    public Color beamColor = new Color(0.93f, 0.27f, 0.24f, 0.95f);
    public Color coreColor = new Color(1f, 0.93f, 0.9f, 1f);
    public Color outlineColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);

    [Tooltip("レーザーの絵が、ほかの物より手前か奥かを決める数字。大きいほど手前。")]
    public int sortingOrder = 30;

    [Header("効果音（空なら鳴らさない）")]
    public AudioClip warnClip;
    [Range(0f, 1f)] public float warnVolume = 0.8f;
    public AudioClip fireClip;
    [Range(0f, 1f)] public float fireVolume = 1f;
    [Tooltip("音が聞こえる距離（ワールド単位）。遠いほど小さくなる。")]
    public float audibleRange = 14f;

    [Header("調べる用")]
    [Tooltip("オンなら、フェーズが変わるたびに、Console に出す。")]
    public bool debugLog = false;

    private enum Phase { Off, Warn, Fire }

    private Phase _phase = Phase.Off;
    private float _time;           // いまのフェーズに入ってからの秒数（startDelay の分だけ、マイナスから始める）

    private GameObject _player;
    private Collider2D[] _playerCols;
    private float _findTimer;

    private LineRenderer _outline, _beam, _core, _warnLine, _glow;
    private ParticleSystem _sparks;
    private readonly RaycastHit2D[] _rayHits = new RaycastHit2D[16];
    private readonly Collider2D[] _overlap = new Collider2D[16];
    private static Material s_mat, s_dotMat;

    // ================= 初期化 =================

    private void Awake()
    {
        _time = -Mathf.Max(0f, startDelay);
        BuildVisuals();
        SetVisible(Phase.Off, 0f);
    }

    private static bool EnsureMaterials()
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null)
        {
            Debug.LogWarning("[LaserEmitter] Sprites/Default のシェーダーが見つからないため、レーザーの見た目を出せません。");
            return false;
        }
        if (s_mat == null)
            s_mat = new Material(sh);
        if (s_dotMat == null)
        {
            s_dotMat = new Material(sh);
            int n = 16;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            float c = (n - 1) * 0.5f;
            Color[] px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    px[y * n + x] = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.5f, 1f, d));
                }
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            s_dotMat.mainTexture = tex;
        }
        return true;
    }

    private LineRenderer MakeLine(string lineName, int order, bool round)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.sharedMaterial = s_mat;
        lr.numCapVertices = round ? 6 : 0;
        lr.alignment = LineAlignment.View;
        lr.sortingOrder = order;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    private void BuildVisuals()
    {
        if (!EnsureMaterials())
            return;

        _outline = MakeLine("BeamOutline (auto)", sortingOrder, false);
        _beam = MakeLine("Beam (auto)", sortingOrder + 1, false);
        _core = MakeLine("BeamCore (auto)", sortingOrder + 2, false);
        _warnLine = MakeLine("Warn (auto)", sortingOrder, false);
        _glow = MakeLine("Glow (auto)", sortingOrder + 3, true);

        // 当たった先の火花
        GameObject sp = new GameObject("Sparks (auto)");
        sp.transform.SetParent(transform, false);
        _sparks = sp.AddComponent<ParticleSystem>();
        _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer r = sp.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = s_dotMat;
        r.sortingOrder = sortingOrder + 4;
        ParticleSystem.MainModule main = _sparks.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(beamColor, coreColor);
        main.gravityModifier = 1.2f;
        main.maxParticles = 60;
        ParticleSystem.EmissionModule em = _sparks.emission;
        em.rateOverTime = 40f;
        ParticleSystem.ShapeModule sh = _sparks.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 0.05f;
        ParticleSystem.ColorOverLifetimeModule col = _sparks.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    // ================= ビームの位置 =================

    private Vector2 Origin()
    {
        return (Vector2)transform.position + (Vector2)transform.right * muzzleOffset;
    }

    private Vector2 Dir()
    {
        Vector2 d = transform.right;
        return d.sqrMagnitude < 0.0001f ? Vector2.right : d.normalized;
    }

    private bool IsPlayerCollider(Collider2D c)
    {
        return c != null && c.GetComponentInParent<CharacterController2D>() != null;
    }

    private bool IsOwn(Collider2D c)
    {
        if (c == null)
            return false;
        if (c.transform.IsChildOf(transform))
            return true;
        return false;
    }

    /// <summary>ビームの長さ。固い物（主人公・すり抜ける物・自分以外）に当たるまで。</summary>
    private float BeamLength(Vector2 origin, Vector2 dir)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.useLayerMask = true;
        filter.layerMask = blockLayers;
        int n = Physics2D.Raycast(origin, dir, filter, _rayHits, maxLength);
        float best = maxLength;
        for (int i = 0; i < n; i++)
        {
            Collider2D c = _rayHits[i].collider;
            if (c == null || IsOwn(c) || IsPlayerCollider(c))
                continue;
            if (_rayHits[i].distance < best)
                best = _rayHits[i].distance;
        }
        return Mathf.Max(0f, best);
    }

    // ================= 時間の流れ =================

    private static bool Frozen()
    {
        return GamePause.IsPaused || LoadingScreen.IsLoading || HitFeedback.IsDying;
    }

    private float PhaseLength(Phase p)
    {
        switch (p)
        {
            case Phase.Off: return Mathf.Max(0.05f, offSeconds);
            case Phase.Warn: return Mathf.Max(0.05f, warnSeconds);
            default: return Mathf.Max(0.05f, fireSeconds);
        }
    }

    private void NextPhase()
    {
        Phase next = _phase == Phase.Off ? Phase.Warn : (_phase == Phase.Warn ? Phase.Fire : Phase.Off);
        _phase = next;
        _time = 0f;
        if (debugLog)
            Debug.Log("[LaserEmitter] " + name + "：" + (next == Phase.Off ? "オフ" : (next == Phase.Warn ? "予告" : "発射")), this);

        if (next == Phase.Warn)
            PlaySfx(warnClip, warnVolume);
        else if (next == Phase.Fire)
            PlaySfx(fireClip, fireVolume);
    }

    private void Update()
    {
        if (Frozen())
            return;

        _time += Time.deltaTime;
        while (_time >= PhaseLength(_phase))
        {
            float rest = _time - PhaseLength(_phase);
            NextPhase();
            _time = rest;
        }

        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        if (_phase != Phase.Fire || Frozen())
            return;
        CheckHit();
    }

    // ================= 当たり =================

    private void CheckHit()
    {
        Vector2 origin = Origin();
        Vector2 dir = Dir();
        float len = BeamLength(origin, dir);
        if (len <= 0.01f)
            return;

        Vector2 mid = origin + dir * (len * 0.5f);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        int n = Physics2D.OverlapBox(mid, new Vector2(len, beamWidth * 0.8f), angle, filter, _overlap);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = _overlap[i];
            if (c == null || c.isTrigger)
                continue;
            CharacterController2D player = c.GetComponentInParent<CharacterController2D>();
            if (player == null)
                continue;

            // はじかれる向き：レーザーの線から、離れる向き
            Vector2 p = player.transform.position;
            float along = Mathf.Clamp(Vector2.Dot(p - origin, dir), 0f, len);
            Vector2 onBeam = origin + dir * along;
            Vector3 source = (p - onBeam).sqrMagnitude < 0.0025f ? (Vector3)(p + Vector2.down) : (Vector3)onBeam;
            player.ApplyDamage(damage, source, deathCause, invincibleTimeAfterHit);
            if (debugLog)
                Debug.Log("[LaserEmitter] " + name + "：主人公に当たった", this);
        }
    }

    // ================= 見た目 =================

    private void SetVisible(Phase p, float t)
    {
        bool warn = p == Phase.Warn;
        bool fire = p == Phase.Fire;
        if (_outline != null) _outline.enabled = fire;
        if (_beam != null) _beam.enabled = fire;
        if (_core != null) _core.enabled = fire;
        if (_warnLine != null) _warnLine.enabled = warn;
        if (_glow != null) _glow.enabled = warn || fire;
        if (_sparks != null)
        {
            if (fire && !_sparks.isPlaying) _sparks.Play();
            if (!fire && _sparks.isPlaying) _sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SetLine(LineRenderer lr, Vector2 a, Vector2 b, float width, Color color)
    {
        if (lr == null)
            return;
        lr.SetPosition(0, new Vector3(a.x, a.y, 0f));
        lr.SetPosition(1, new Vector3(b.x, b.y, 0f));
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
    }

    private void UpdateVisuals()
    {
        SetVisible(_phase, _time);
        if (_phase == Phase.Off)
            return;

        Vector2 origin = Origin();
        Vector2 dir = Dir();
        float len = BeamLength(origin, dir);
        Vector2 end = origin + dir * len;

        if (_phase == Phase.Warn)
        {
            // 点滅：だんだん速くなる
            float k = Mathf.Clamp01(_time / PhaseLength(Phase.Warn));
            float freq = Mathf.Lerp(5f, 16f, k);
            float blink = Mathf.Sin(_time * freq * Mathf.PI * 2f) > -0.2f ? 1f : 0.15f;
            Color wc = beamColor;
            wc.a = beamColor.a * 0.8f * blink;
            SetLine(_warnLine, origin, end, 0.06f + 0.04f * k, wc);

            Color gc = beamColor;
            gc.a = Mathf.Lerp(0.35f, 0.95f, k) * (0.6f + 0.4f * blink);
            float gw = Mathf.Lerp(0.18f, 0.36f, k);
            SetLine(_glow, origin, origin + dir * 0.01f, gw, gc);
        }
        else
        {
            // 発射：少し脈打つ太さ。発射の始めと終わりは、細くなる
            float total = PhaseLength(Phase.Fire);
            float edge = Mathf.Min(0.12f, total * 0.3f);
            float grow = Mathf.Clamp01(Mathf.Min(_time, total - _time) / Mathf.Max(0.01f, edge));
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 60f);
            float w = beamWidth * pulse * Mathf.SmoothStep(0.15f, 1f, grow);

            SetLine(_outline, origin, end, w + 0.12f, outlineColor);
            SetLine(_beam, origin, end, w, beamColor);
            SetLine(_core, origin, end, w * 0.42f, coreColor);

            Color gc = beamColor;
            gc.a = 1f;
            SetLine(_glow, origin, origin + dir * 0.01f, w * 1.5f, gc);

            if (_sparks != null)
            {
                _sparks.transform.position = new Vector3(end.x, end.y, 0f);
            }
        }
    }

    // ================= 効果音 =================

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        _findTimer -= Time.unscaledDeltaTime;
        if (_player == null && _findTimer <= 0f)
        {
            _findTimer = 0.5f;
            _player = NewGameIntro.FindMainPlayerObject();
        }

        float v = 1f;
        if (_player != null)
        {
            float d = Vector2.Distance(_player.transform.position, Origin());
            v = Mathf.Clamp01(1f - d / Mathf.Max(0.1f, audibleRange));
            v = v * v * (3f - 2f * v);
        }
        if (v <= 0.01f)
            return;
        AudioManager.PlaySE(clip, volume * v);
    }

    // ================= シーン画面の目印 =================

    private void OnDrawGizmos()
    {
        Vector2 origin = (Vector2)transform.position + (Vector2)transform.right * muzzleOffset;
        Vector2 dir = ((Vector2)transform.right).normalized;
        float len = maxLength;
        if (Application.isPlaying)
        {
            len = BeamLength(origin, dir);
        }
        else
        {
            RaycastHit2D h = Physics2D.Raycast(origin, dir, maxLength);
            if (h.collider != null && !h.collider.isTrigger && !h.collider.transform.IsChildOf(transform))
                len = h.distance;
        }

        Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.9f);
        Gizmos.DrawLine(origin, origin + dir * len);
        Vector2 end = origin + dir * len;
        Vector2 side = new Vector2(-dir.y, dir.x) * 0.2f;
        Gizmos.DrawLine(end, end - dir * 0.35f + side);
        Gizmos.DrawLine(end, end - dir * 0.35f - side);
        Gizmos.DrawWireSphere(origin, 0.08f);
    }
}
