using UnityEngine;

/// <summary>
/// KEY ESCAPE - 落ちてくる石。主人公が真下に来ると、ゆれてから落ちる。
/// 地面か、主人公に当たると、砕けて消える。しばらくすると、元の位置に、ふわっともどる。
///
/// 使い方：
///   1. メニュー「Tools > KEY ESCAPE > ステージ部品 > 落石のプレハブを作る」で、プレハブを作る。
///   2. プレハブを、天井の下など、落とす場所に置く。BoxCollider2D（Is Trigger）の大きさが、石の当たり判定。
///   3. 石の真下の、水色の枠が、「落ちる範囲」。主人公がここに入ると、ゆれ始める。
///      枠の幅は Detect Width で変える。枠の下は、地面まで（Detect Max Distance が上限）。
///
/// 動き：
///   ・待機：天井の下で、じっとしている。
///   ・ゆれる：主人公が真下に来ると、ガタガタ揺れて、パラパラと砂が落ちる。この間に、逃げられる。
///   ・落下：だんだん速くなって落ちる。落ちている間に当たると、ダメージ（トゲと同じ）。
///   ・砕ける：地面か主人公に当たると、砕けて、かけらが飛ぶ。画面が少し揺れる。
///   ・もどる：しばらくすると、元の位置に、ふわっとあらわれる。真下に主人公がいれば、また揺れ始める。
///
/// 効果音（Inspector の「効果音」に、音を入れる。空なら鳴らさない。遠いほど小さくなる）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class FallingStone : MonoBehaviour
{
    [Header("落ちる範囲（石の真下）")]
    [Tooltip("落ちる範囲の幅（ワールド単位）。石の真ん中を中心にして、左右に広がる。")]
    public float detectWidth = 2f;

    [Tooltip("落ちる範囲の、一番長い高さ。地面があれば、そこまで。")]
    public float detectMaxDistance = 14f;

    [Header("動き")]
    [Tooltip("ゆれている時間（秒）。この間に、逃げられる。")]
    public float shakeSeconds = 0.7f;

    [Tooltip("ゆれの大きさ（ワールド単位）。")]
    public float shakeAmount = 0.06f;

    [Tooltip("落ちる時の、加速の強さ。大きいほど、すぐ速くなる。")]
    public float gravity = 38f;

    [Tooltip("落ちる速さの上限。")]
    public float maxFallSpeed = 28f;

    [Header("ダメージ")]
    [Tooltip("落ちている石に当たった時のダメージ。2 で、ハート1個分。")]
    public float damage = 4f;

    [Tooltip("当たったあとの無敵の秒数。0以下なら、いつもの被弾と同じ（1秒）。")]
    public float invincibleTimeAfterHit = -1f;

    [Tooltip("オンなら、真上に弾かれる。オフなら、石から離れる向きに弾かれる。")]
    public bool knockbackUp = true;

    [Tooltip("HPが0になった時の死因（結果画面の一言コメントの種類）。")]
    public DeathCause deathCause = DeathCause.Other;

    [Header("砕けたあと")]
    [Tooltip("砕けてから、元の位置にもどり始めるまでの秒数。")]
    public float respawnSeconds = 4f;

    [Tooltip("もどる時に、ふわっとあらわれるまでの秒数。")]
    public float fadeInSeconds = 0.6f;

    [Tooltip("砕けた時の、カメラの揺れの強さ。0なら、揺らさない。")]
    public float landShake = 0.12f;

    [Header("効果音（空なら鳴らさない）")]
    [Tooltip("ゆれ始めた時の音。")]
    public AudioClip shakeClip;
    [Range(0f, 1f)] public float shakeVolume = 0.8f;

    [Tooltip("砕けた時の音。")]
    public AudioClip breakClip;
    [Range(0f, 1f)] public float breakVolume = 1f;

    [Tooltip("もどった時の音。")]
    public AudioClip respawnClip;
    [Range(0f, 1f)] public float respawnVolume = 0.6f;

    [Tooltip("音が聞こえる距離（ワールド単位）。遠いほど小さくなる。")]
    public float audibleRange = 14f;

    [Header("調べる用")]
    [Tooltip("オンなら、状態が変わるたびに、Console に出す。")]
    public bool debugLog = false;

    private enum State { Idle, Shaking, Falling, Broken, Returning }

    private State _state = State.Idle;
    private float _timer;
    private float _speed;

    private BoxCollider2D _col;
    private SpriteRenderer[] _renders;
    private Transform _visual;
    private Vector3 _visualBase;
    private Vector3 _startPos;
    private float _zoneBottom;       // 落ちる範囲の下のはし（ワールドの高さ）

    private GameObject _player;
    private Collider2D[] _playerCols;
    private float _findTimer;

    private ParticleSystem _bits, _dust;
    private static Material s_squareMat, s_dotMat;

    private readonly RaycastHit2D[] _hits = new RaycastHit2D[16];
    private readonly Collider2D[] _overlap = new Collider2D[16];

    // ================= 初期化 =================

    private void Reset()
    {
        _col = GetComponent<BoxCollider2D>();
        _col.isTrigger = true;
    }

    private void Awake()
    {
        _col = GetComponent<BoxCollider2D>();
        _col.isTrigger = true;
        _renders = GetComponentsInChildren<SpriteRenderer>(true);
        _visual = _renders.Length > 0 ? _renders[0].transform : null;
        if (_visual == transform)
            _visual = null;   // 絵が本体にある時は、ゆれを出せない（絵だけ動かせない）ので、ゆらさない
        if (_visual != null)
            _visualBase = _visual.localPosition;
        _startPos = transform.position;
        BuildParticles();
    }

    private void Start()
    {
        _zoneBottom = FindZoneBottom();
    }

    private static bool EnsureMaterials()
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null)
        {
            Debug.LogWarning("[FallingStone] Sprites/Default のシェーダーが見つからないため、砂やかけらを出せません。");
            return false;
        }
        if (s_squareMat == null)
            s_squareMat = new Material(sh);
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

    private ParticleSystem MakePS(string psName, Material mat, int order)
    {
        GameObject go = new GameObject(psName);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = order;
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        return ps;
    }

    private void BuildParticles()
    {
        if (!EnsureMaterials())
            return;
        int order = (_renders != null && _renders.Length > 0 ? _renders[0].sortingOrder : 0) + 2;

        // かけら（砕けた時に、四角い欠片が飛ぶ）
        _bits = MakePS("Bits (auto)", s_squareMat, order);
        ParticleSystem.MainModule bm = _bits.main;
        bm.loop = false;
        bm.startSpeed = 0f;
        bm.gravityModifier = 2.4f;
        bm.maxParticles = 120;
        ParticleSystem.EmissionModule be = _bits.emission;
        be.enabled = false;
        ParticleSystem.ShapeModule bs = _bits.shape;
        bs.enabled = false;
        ParticleSystem.ColorOverLifetimeModule bc = _bits.colorOverLifetime;
        bc.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        bc.color = new ParticleSystem.MinMaxGradient(g);
        _bits.Play();

        // 砂（ゆれている間、パラパラ落ちる。砕けた時の砂ぼこりにも使う）
        _dust = MakePS("Dust (auto)", s_dotMat, order);
        ParticleSystem.MainModule dm = _dust.main;
        dm.loop = true;
        dm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        dm.startSpeed = 0f;
        dm.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        dm.startColor = new Color(0.62f, 0.62f, 0.6f, 0.9f);
        dm.gravityModifier = 0.6f;
        dm.maxParticles = 80;
        ParticleSystem.EmissionModule de = _dust.emission;
        de.enabled = false;
        ParticleSystem.ShapeModule ds = _dust.shape;
        ds.enabled = false;
        ParticleSystem.ColorOverLifetimeModule dc = _dust.colorOverLifetime;
        dc.enabled = true;
        dc.color = new ParticleSystem.MinMaxGradient(g);
        _dust.Play();
    }

    // ================= 範囲 =================

    private Vector2 BodySize()
    {
        Vector3 s = transform.lossyScale;
        return new Vector2(Mathf.Abs(_col.size.x * s.x), Mathf.Abs(_col.size.y * s.y));
    }

    private Vector2 BodyCenter()
    {
        Vector3 s = transform.lossyScale;
        return (Vector2)transform.position + new Vector2(_col.offset.x * s.x, _col.offset.y * s.y);
    }

    private bool IsOwn(Collider2D c)
    {
        return c != null && c.transform.IsChildOf(transform);
    }

    private bool IsPlayerCollider(Collider2D c)
    {
        return c != null && c.GetComponentInParent<CharacterController2D>() != null;
    }

    /// <summary>真下にある、固い物（地面など）までの高さを調べて、落ちる範囲の下のはしを返す。</summary>
    private float FindZoneBottom()
    {
        Vector2 c = BodyCenter();
        Vector2 size = BodySize();
        float bottom = c.y - size.y * 0.5f;
        float dist = CastDown(c, size, detectMaxDistance);
        return bottom - dist;
    }

    /// <summary>石の大きさの箱を、真下に飛ばして、固い物に当たるまでの距離を返す。当たらなければ maxDist。</summary>
    private float CastDown(Vector2 center, Vector2 size, float maxDist)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        int n = Physics2D.BoxCast(center, size * 0.98f, 0f, Vector2.down, filter, _hits, maxDist);
        float best = maxDist;
        for (int i = 0; i < n; i++)
        {
            Collider2D c = _hits[i].collider;
            if (c == null || c.isTrigger || IsOwn(c) || IsPlayerCollider(c))
                continue;
            if (_hits[i].distance < best)
                best = _hits[i].distance;
        }
        return Mathf.Max(0f, best);
    }

    private static bool Overlap2D(Bounds a, Bounds b)
    {
        return a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.y <= b.max.y && a.max.y >= b.min.y;
    }

    private bool FindPlayer()
    {
        if (_player == null)
        {
            _findTimer -= Time.unscaledDeltaTime;
            if (_findTimer > 0f)
                return false;
            _findTimer = 0.3f;
            _player = NewGameIntro.FindMainPlayerObject();
            _playerCols = _player != null ? _player.GetComponentsInChildren<Collider2D>() : null;
        }
        return _player != null && _playerCols != null;
    }

    /// <summary>主人公の体が、落ちる範囲に入っているか。</summary>
    private bool PlayerInZone()
    {
        if (!FindPlayer())
            return false;
        Vector2 c = BodyCenter();
        Vector2 size = BodySize();
        float top = c.y - size.y * 0.5f;
        float h = Mathf.Max(0.01f, top - _zoneBottom);
        Bounds zone = new Bounds(new Vector3(c.x, top - h * 0.5f, 0f), new Vector3(Mathf.Max(0.1f, detectWidth), h, 10f));
        for (int i = 0; i < _playerCols.Length; i++)
        {
            Collider2D pc = _playerCols[i];
            if (pc == null || !pc.enabled || pc.isTrigger)
                continue;
            Bounds b = pc.bounds;
            b.center = new Vector3(b.center.x, b.center.y, 0f);
            b.size = new Vector3(b.size.x, b.size.y, 10f);
            if (Overlap2D(zone, b))
                return true;
        }
        return false;
    }

    // ================= 時間の流れ =================

    private static bool Frozen()
    {
        return GamePause.IsPaused || LoadingScreen.IsLoading || HitFeedback.IsDying;
    }

    private void SetState(State s)
    {
        _state = s;
        _timer = 0f;
        if (debugLog)
            Debug.Log("[FallingStone] " + name + "：" + s, this);
    }

    private void Update()
    {
        if (Frozen())
            return;

        switch (_state)
        {
            case State.Shaking:
                UpdateShake();
                break;
            case State.Broken:
                _timer += Time.deltaTime;
                if (_timer >= respawnSeconds)
                    BeginReturn();
                break;
            case State.Returning:
                UpdateReturn();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (Frozen())
            return;

        switch (_state)
        {
            case State.Idle:
                if (PlayerInZone())
                    BeginShake();
                break;
            case State.Falling:
                StepFall();
                break;
        }
    }

    // ================= ゆれる =================

    private void BeginShake()
    {
        SetState(State.Shaking);
        PlaySfx(shakeClip, shakeVolume);
        if (_dust != null)
        {
            ParticleSystem.EmissionModule de = _dust.emission;
            de.enabled = true;
            de.rateOverTime = 14f;
        }
    }

    private void UpdateShake()
    {
        _timer += Time.deltaTime;
        float k = Mathf.Clamp01(_timer / Mathf.Max(0.05f, shakeSeconds));

        if (_visual != null)
        {
            float amp = shakeAmount * (0.4f + 0.6f * k);
            _visual.localPosition = _visualBase + new Vector3(Random.Range(-amp, amp), Random.Range(-amp, amp) * 0.5f, 0f);
        }
        if (_dust != null)
        {
            // 砂は、石の底から落ちる
            Vector2 c = BodyCenter();
            Vector2 size = BodySize();
            _dust.transform.position = new Vector3(c.x + Random.Range(-size.x * 0.4f, size.x * 0.4f), c.y - size.y * 0.5f, 0f);
        }

        if (_timer >= shakeSeconds)
            BeginFall();
    }

    private void BeginFall()
    {
        if (_dust != null)
        {
            ParticleSystem.EmissionModule de = _dust.emission;
            de.enabled = false;
        }
        if (_visual != null)
            _visual.localPosition = _visualBase;
        _speed = 0f;
        SetState(State.Falling);
    }

    // ================= 落ちる =================

    private void StepFall()
    {
        _speed = Mathf.Min(maxFallSpeed, _speed + gravity * Time.fixedDeltaTime);
        float step = _speed * Time.fixedDeltaTime;

        Vector2 c = BodyCenter();
        Vector2 size = BodySize();
        float maxD = step + 0.02f;
        float hit = CastDown(c, size, maxD);
        bool landed = hit < maxD - 0.0001f;       // 少し先に、固い物がある
        float move = landed ? hit : step;
        transform.position += Vector3.down * move;

        // 主人公に当たったか
        if (HitPlayer())
        {
            Break();
            return;
        }
        if (landed)
            Break();
    }

    private bool HitPlayer()
    {
        Vector2 c = BodyCenter();
        Vector2 size = BodySize();
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        int n = Physics2D.OverlapBox(c, size * 0.95f, 0f, filter, _overlap);
        for (int i = 0; i < n; i++)
        {
            Collider2D o = _overlap[i];
            if (o == null || o.isTrigger)
                continue;
            CharacterController2D player = o.GetComponentInParent<CharacterController2D>();
            if (player == null)
                continue;

            Vector3 source = knockbackUp ? player.transform.position + Vector3.down : transform.position;
            player.ApplyDamage(damage, source, deathCause, invincibleTimeAfterHit);
            if (debugLog)
                Debug.Log("[FallingStone] " + name + "：主人公に当たった", this);
            return true;
        }
        return false;
    }

    // ================= 砕ける・もどる =================

    private void Break()
    {
        SetState(State.Broken);
        Vector2 c = BodyCenter();
        Vector2 size = BodySize();
        Vector3 bottom = new Vector3(c.x, c.y - size.y * 0.5f, 0f);

        SetRenderersVisible(false);
        SpawnBits(new Vector3(c.x, c.y, 0f), size);
        SpawnDust(bottom, size.x);
        PlaySfx(breakClip, breakVolume);

        if (landShake > 0f)
        {
            CameraFollow cam = FindObjectOfType<CameraFollow>();
            if (cam != null)
                cam.ShakeCamera(0.2f, landShake * DistanceFactor());
        }
    }

    private void BeginReturn()
    {
        transform.position = _startPos;
        if (_visual != null)
            _visual.localPosition = _visualBase;
        SetRenderersVisible(true);
        SetAlpha(0f);
        SetState(State.Returning);
        PlaySfx(respawnClip, respawnVolume);
    }

    private void UpdateReturn()
    {
        _timer += Time.deltaTime;
        float k = fadeInSeconds <= 0.01f ? 1f : Mathf.Clamp01(_timer / fadeInSeconds);
        SetAlpha(Mathf.SmoothStep(0f, 1f, k));
        if (k >= 1f)
        {
            SetAlpha(1f);
            SetState(State.Idle);
        }
    }

    private void SetRenderersVisible(bool visible)
    {
        if (_renders == null)
            return;
        for (int i = 0; i < _renders.Length; i++)
            if (_renders[i] != null)
                _renders[i].enabled = visible;
    }

    private void SetAlpha(float a)
    {
        if (_renders == null)
            return;
        for (int i = 0; i < _renders.Length; i++)
        {
            if (_renders[i] == null)
                continue;
            Color c = _renders[i].color;
            c.a = a;
            _renders[i].color = c;
        }
    }

    private void SpawnBits(Vector3 center, Vector2 size)
    {
        if (_bits == null)
            return;
        Color light = new Color(0.9f, 0.9f, 0.88f, 1f);
        Color mid = new Color(0.62f, 0.62f, 0.6f, 1f);
        Color dark = new Color(0.12f, 0.12f, 0.14f, 1f);
        for (int i = 0; i < 18; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = center + new Vector3(Random.Range(-size.x * 0.4f, size.x * 0.4f), Random.Range(-size.y * 0.4f, size.y * 0.4f), 0f);
            ep.velocity = new Vector3(Random.Range(-4f, 4f), Random.Range(1.5f, 7f), 0f);
            ep.startLifetime = Random.Range(0.6f, 1.1f);
            ep.startSize = Random.Range(0.08f, 0.24f);
            ep.rotation = Random.Range(0f, 360f);
            ep.angularVelocity = Random.Range(-360f, 360f);
            float r = Random.value;
            ep.startColor = r < 0.5f ? light : (r < 0.8f ? mid : dark);
            _bits.Emit(ep, 1);
        }
    }

    private void SpawnDust(Vector3 bottom, float width)
    {
        if (_dust == null)
            return;
        for (int i = 0; i < 10; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = bottom + new Vector3(Random.Range(-width * 0.5f, width * 0.5f), 0.05f, 0f);
            ep.velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(0.3f, 1.8f), 0f);
            ep.startLifetime = Random.Range(0.4f, 0.8f);
            ep.startSize = Random.Range(0.12f, 0.3f);
            ep.startColor = new Color(0.66f, 0.66f, 0.64f, 0.9f);
            _dust.Emit(ep, 1);
        }
    }

    // ================= 効果音 =================

    private float DistanceFactor()
    {
        if (!FindPlayer())
            return 1f;
        float d = Vector2.Distance(_player.transform.position, transform.position);
        float v = Mathf.Clamp01(1f - d / Mathf.Max(0.1f, audibleRange));
        return v * v * (3f - 2f * v);
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;
        float v = DistanceFactor();
        if (v <= 0.01f)
            return;
        AudioManager.PlaySE(clip, volume * v);
    }

    // ================= シーン画面の目印 =================

    private void OnDrawGizmos()
    {
        BoxCollider2D c = _col != null ? _col : GetComponent<BoxCollider2D>();
        if (c == null)
            return;
        Vector3 s = transform.lossyScale;
        Vector2 size = new Vector2(Mathf.Abs(c.size.x * s.x), Mathf.Abs(c.size.y * s.y));
        Vector2 center = (Vector2)(Application.isPlaying ? _startPos : transform.position) + new Vector2(c.offset.x * s.x, c.offset.y * s.y);

        // 石の枠
        Gizmos.color = new Color(0.6f, 0.6f, 0.62f, 0.9f);
        Gizmos.DrawWireCube(center, size);

        // 落ちる範囲（真下）
        float bottom = center.y - size.y * 0.5f;
        float dist = detectMaxDistance;
        if (!Application.isPlaying)
        {
            RaycastHit2D h = Physics2D.BoxCast(center, size * 0.98f, 0f, Vector2.down, detectMaxDistance);
            if (h.collider != null && !h.collider.isTrigger && !h.collider.transform.IsChildOf(transform))
                dist = h.distance;
        }
        else
        {
            dist = bottom - _zoneBottom;
        }
        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(center.x, bottom - dist * 0.5f, 0f), new Vector3(Mathf.Max(0.1f, detectWidth), dist, 0f));
    }
}
