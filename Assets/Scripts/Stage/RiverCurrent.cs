using UnityEngine;

/// <summary>
/// KEY ESCAPE - 流れる川。川に入ったプレイヤーを、一定の向きに押し流して、水に触れている間は、酸素を減らす。
///
/// 使い方：
///   1. Hierarchy で空のオブジェクトを作り、名前を「River」にして、このスクリプトを付ける。
///      （BoxCollider2D が自動で付いて、Is Trigger がオンになる。この箱の範囲が川）
///      または、メニュー「Tools > KEY ESCAPE > ステージ部品 > 川のプレハブを作る」で作ったプレハブを置く。
///   2. 川の大きさは、BoxCollider2D の Size で決める。上のふちが、水面。
///   3. Layer は、地面（Ground）と同じにする。川底が地面として扱われて、川の中でも立ってジャンプできる。
///   4. 川の中に、足場（普通の Collider2D つきの絵）を置く。足場は River の子にしないこと。
///
/// 動き：
///   ・川の範囲にプレイヤーの体が重なっている間、毎フレーム、flowDirection の向きに flowSpeed の速さでずらされる。
///   ・足場など、川底以外の物の上に立っている間は、流されない。
///   ・水に触れている間は、酸素が減る（PlayerOxygen。HPゲージが青くなり、1マスずつ消えて、0でやられる）。
///   ・下流の端でやられる仕組み（killAtDownstreamEnd）は、初期ではオフ。端には、崖などを置く想定。
///
/// 効果音（Inspector の「効果音」に、音を入れる。空なら鳴らさない。音量は、設定画面の SE の音量に従う）：
///   ・Enter Clip / Exit Clip：プレイヤーが水に入った時、出た時。
///   ・Ambient Clip：川のそばにいる間、鳴り続ける流れの音（ループ）。川の範囲から遠ざかるほど、小さくなる。
///
/// 見た目：仮の絵（波打つ水面・水の中の波線・泡・流れる粒・水しぶき）をコードで出す。
///   showTempVisuals をオフにすると、全部消えて、あとで本物の絵（SpriteRenderer）を、自分で置ける。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class RiverCurrent : MonoBehaviour
{
    [Header("流れ")]
    [Tooltip("流れる向き。(1,0) で右、(-1,0) で左。")]
    public Vector2 flowDirection = Vector2.right;

    [Tooltip("流れる速さ（1秒あたりのユニット数）。プレイヤーの走る速さは、1秒あたり約8。4 なら、流れに逆らって、1秒あたり約4で進める。")]
    public float flowSpeed = 4f;

    [Header("酸素（水に触れている間、HPゲージが青くなって減る）")]
    [Tooltip("オンなら、酸素のしくみを使う。")]
    public bool useOxygen = true;

    [Tooltip("酸素1マスが消えるまでの秒数。")]
    public float oxygenSecondsPerCell = 0.5f;

    [Tooltip("酸素の最大のマス数。HPゲージのマス数（5）に合わせる。5なら、5 x 0.5 = 2.5秒で、やられる。")]
    public int oxygenCells = 5;

    [Tooltip("水から出ている間に、1秒あたり回復する酸素のマス数。1ならゆっくり、4ならほぼ一瞬で戻る。")]
    public float oxygenRefillCellsPerSecond = 1f;

    [Header("川の端")]
    [Tooltip("オンなら、下流の端まで流されたプレイヤーが、やられる（落下死と同じ演出）。初期はオフ。")]
    public bool killAtDownstreamEnd = false;

    [Tooltip("下流の端から、この距離だけ手前に入ったら、やられる。")]
    public float endMargin = 0.3f;

    [Header("調べる用")]
    [Tooltip("オンなら、プレイヤーが流されなかった理由を Console に出す。")]
    public bool debugLog = false;

    [Header("川底")]
    [Tooltip("オンなら、川の下に見えない地面を作る。川の中に落ちても、立ってジャンプできる。")]
    public bool createRiverBed = true;

    [Tooltip("川底の厚さ（ユニット）。")]
    public float riverBedThickness = 0.5f;

    [Header("効果音（空なら鳴らさない）")]
    [Tooltip("プレイヤーが水に入った時の音。")]
    public AudioClip enterClip;
    [Range(0f, 1f)] public float enterVolume = 1f;

    [Tooltip("プレイヤーが水から出た時の音。")]
    public AudioClip exitClip;
    [Range(0f, 1f)] public float exitVolume = 1f;

    [Tooltip("入る音・出る音を、続けて鳴らさない間隔（秒）。")]
    public float sfxMinInterval = 0.3f;

    [Tooltip("川のそばで、鳴り続ける流れの音（ループ）。")]
    public AudioClip ambientClip;
    [Range(0f, 1f)] public float ambientVolume = 0.6f;

    [Tooltip("流れの音が聞こえる距離（川の範囲のふちから、ユニット）。範囲の中が、いちばん大きく、遠ざかるほど小さくなる。")]
    public float ambientRange = 12f;

    [Header("仮の見た目（コードで出す）")]
    public bool showTempVisuals = true;
    public Color waterColor = new Color(0.45f, 0.7f, 1f, 0.28f);
    [Tooltip("水面の波の線の色（手描き風の、黒っぽい線）。")]
    public Color waveLineColor = new Color(0.1f, 0.12f, 0.2f, 0.95f);
    [Tooltip("水の中の、細い波の線の色。")]
    public Color innerLineColor = new Color(1f, 1f, 1f, 0.45f);
    [Tooltip("波の高さ（ユニット）。")]
    public float waveHeight = 0.12f;
    [Tooltip("波の長さ（ユニット）。")]
    public float waveLength = 2.4f;
    [Tooltip("波が動く速さ（1秒あたりの波の数）。")]
    public float waveSpeed = 0.25f;
    [Tooltip("水面の絵が、プレイヤーより手前か奥かを決める数字。大きいほど手前。")]
    public int waterSortingOrder = 20;
    [Tooltip("泡の量（川の幅10ユニットあたり、1秒に出る数）。0なら出さない。")]
    public float bubbleRate = 5f;
    [Tooltip("流れる粒の量（川の面積1平方ユニットあたりの密度）。0なら出さない。")]
    public float flowDotDensity = 0.1f;
    [Tooltip("水に入る・出るときの、水しぶきの数。0なら出さない。")]
    public int splashCount = 14;

    private BoxCollider2D _col;
    private GameObject _bed;
    private float _lastPush = -1f;
    private Vector2 _curPos;
    private Vector2 _lastPos;
    private bool _wasInWater;
    private float _lastSfx = -10f;

    // 見た目
    private Transform _visualRoot;
    private Mesh _mesh;
    private Vector3[] _verts;
    private int _seg;
    private float _w, _h;
    private LineRenderer[] _lines;
    private Vector3[] _linePos;
    private ParticleSystem _bubbles, _dots, _splash;
    private ParticleSystem.Particle[] _dotBuf;

    // 流れの音
    private AudioSource _ambient;
    private SettingsAudioSource _ambientSettings;
    private CharacterController2D _audioPlayer;
    private float _findTimer;

    // 川底のコライダーの一覧（足場と区別して「地面に立っている」と数えないため）
    private static readonly System.Collections.Generic.HashSet<Collider2D> s_beds = new System.Collections.Generic.HashSet<Collider2D>();
    private static Material s_mat, s_ringMat, s_dotMat;

    // ================= 初期化 =================

    private void Reset()
    {
        _col = GetComponent<BoxCollider2D>();
        _col.isTrigger = true;
        if (_col.size.sqrMagnitude < 0.01f)
            _col.size = new Vector2(10f, 3f);
    }

    private void OnValidate()
    {
        BoxCollider2D c = GetComponent<BoxCollider2D>();
        if (c != null)
            c.isTrigger = true;
        if (flowDirection.sqrMagnitude < 0.0001f)
            flowDirection = Vector2.right;
    }

    private void Awake()
    {
        _col = GetComponent<BoxCollider2D>();
        _col.isTrigger = true;
        if (flowDirection.sqrMagnitude < 0.0001f)
            flowDirection = Vector2.right;
        flowDirection.Normalize();

        if (createRiverBed)
            BuildRiverBed();
        if (showTempVisuals)
            BuildVisuals();
        BuildAmbient();
    }

    private void OnDestroy()
    {
        if (_bed != null)
        {
            Collider2D bc = _bed.GetComponent<Collider2D>();
            if (bc != null)
                s_beds.Remove(bc);
        }
    }

    private void BuildRiverBed()
    {
        _bed = new GameObject("RiverBed (auto)");
        _bed.transform.SetParent(transform, false);
        _bed.layer = gameObject.layer;

        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        float t = Mathf.Max(0.05f, riverBedThickness) / scaleY;

        BoxCollider2D bc = _bed.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(_col.size.x, t);
        _bed.transform.localPosition = new Vector3(_col.offset.x, _col.offset.y - _col.size.y * 0.5f - t * 0.5f, 0f);
        s_beds.Add(bc);
    }

    // ================= 流す・酸素・効果音 =================

    private readonly Collider2D[] _hits = new Collider2D[16];

    // OnTriggerStay2D は、プレイヤーが止まって眠ると呼ばれなくなる。そのため、毎回、川の範囲を調べる。
    private void FixedUpdate()
    {
        if (_col == null)
            return;
        if (GamePause.IsPaused || LoadingScreen.IsLoading || HitFeedback.IsDying)
            return;

        Bounds b = _col.bounds;
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        int n = Physics2D.OverlapBox(b.center, b.size, 0f, filter, _hits);

        bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (TryPush(_hits[i]))
                found = true;
        }

        // 水に入った・出た
        if (found && !_wasInWater)
            OnEnterWater(_curPos);
        else if (!found && _wasInWater)
            OnExitWater(_lastPos);

        _wasInWater = found;
        if (found)
            _lastPos = _curPos;
    }

    /// <summary>プレイヤーなら、流して、酸素を知らせて、true を返す（プレイヤーでなければ false）。</summary>
    private bool TryPush(Collider2D other)
    {
        if (other == null || other.isTrigger)
            return false;

        CharacterController2D player = other.GetComponentInParent<CharacterController2D>();
        if (player == null)
            return false;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
            return false;

        // プレイヤーに、コライダーが複数あっても、1回の物理ステップで、1回だけ押す。
        if (Mathf.Approximately(_lastPush, Time.fixedTime))
            return true;
        _lastPush = Time.fixedTime;
        _curPos = rb.position;

        // 水に触れている間は、酸素が減る
        if (useOxygen)
            PlayerOxygen.ReportUnderwater(player, oxygenSecondsPerCell, oxygenCells, oxygenRefillCellsPerSecond);

        // 下流の端：やられる（初期はオフ）
        if (killAtDownstreamEnd && ReachedDownstreamEnd(rb.position))
        {
            HitFeedback.ReloadWithFade(0.35f, DeathCause.Fall);
            return true;
        }

        // 足場の上に立っている間は、流されない
        if (IsStandingOnPlatform(player, other))
        {
            if (debugLog)
                Debug.Log("[RiverCurrent] 足場の上に立っているので、流しません。");
            return true;
        }

        Vector2 dir = flowDirection.normalized;
        rb.position = rb.position + dir * flowSpeed * Time.fixedDeltaTime;
        _curPos = rb.position;
        return true;
    }

    private bool ReachedDownstreamEnd(Vector2 pos)
    {
        Vector2 dir = flowDirection.normalized;
        Bounds b = _col.bounds;
        float half = b.extents.x * Mathf.Abs(dir.x) + b.extents.y * Mathf.Abs(dir.y);
        float along = Vector2.Dot(pos - (Vector2)b.center, dir);
        return along >= half - Mathf.Max(0f, endMargin);
    }

    /// <summary>
    /// 足場（川の中に置いた小さな地面）の上に立っているか。
    /// 川底や、川より広い地面（川を地面に重ねて置いた時の地面）は、足場として数えない。
    /// 足場の条件：①川の底より高い位置まで出ている ②川の幅より狭い。
    /// </summary>
    private bool IsStandingOnPlatform(CharacterController2D player, Collider2D playerCol)
    {
        Bounds pb = playerCol.bounds;
        Vector2 feet = new Vector2(pb.center.x, pb.min.y - 0.05f);
        Bounds rb = _col.bounds;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        Collider2D[] hits = new Collider2D[8];
        int n = Physics2D.OverlapCircle(feet, 0.18f, filter, hits);
        for (int i = 0; i < n; i++)
        {
            Collider2D h = hits[i];
            if (h == null || s_beds.Contains(h))
                continue;
            if (h.transform.IsChildOf(player.transform))
                continue;

            Bounds hb = h.bounds;
            bool higherThanBed = hb.max.y > rb.min.y + 0.2f;
            bool narrowerThanRiver = hb.size.x < rb.size.x - 0.05f;
            if (higherThanBed && narrowerThanRiver)
                return true; // 川の中の足場に立っている

            if (debugLog)
                Debug.Log("[RiverCurrent] 足場として数えない：" + h.name + "（川底より高い=" + higherThanBed + " 川より狭い=" + narrowerThanRiver + "）", h);
        }
        return false;
    }

    private void OnEnterWater(Vector2 pos)
    {
        PlaySfx(enterClip, enterVolume);
        SpawnSplash(pos.x);
    }

    private void OnExitWater(Vector2 pos)
    {
        PlaySfx(exitClip, exitVolume);
        SpawnSplash(pos.x);
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;
        if (Time.unscaledTime - _lastSfx < sfxMinInterval)
            return;
        _lastSfx = Time.unscaledTime;
        AudioManager.PlaySE(clip, volume);
    }

    // ================= 流れの音 =================

    private void BuildAmbient()
    {
        if (ambientClip == null)
            return;
        GameObject go = new GameObject("RiverAmbient (auto)");
        go.transform.SetParent(transform, false);
        _ambient = go.AddComponent<AudioSource>();
        _ambient.clip = ambientClip;
        _ambient.loop = true;
        _ambient.playOnAwake = false;
        _ambient.spatialBlend = 0f;
        _ambient.volume = 0f;
        _ambientSettings = go.AddComponent<SettingsAudioSource>();
        _ambientSettings.kind = SettingsAudioSource.Kind.SE;
        _ambientSettings.BaseVolume = 0f;
    }

    private void UpdateAmbient()
    {
        if (_ambient == null || _ambientSettings == null)
            return;

        if (_audioPlayer == null)
        {
            _findTimer -= Time.unscaledDeltaTime;
            if (_findTimer <= 0f)
            {
                _findTimer = 0.5f;
                CharacterController2D[] all = FindObjectsOfType<CharacterController2D>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].GetComponent<Rigidbody2D>() != null)
                    {
                        _audioPlayer = all[i];
                        break;
                    }
                }
            }
        }

        float v = 0f;
        if (_audioPlayer != null)
        {
            float d = Mathf.Sqrt(_col.bounds.SqrDistance(_audioPlayer.transform.position));
            v = Mathf.Clamp01(1f - d / Mathf.Max(0.1f, ambientRange));
            v = v * v * (3f - 2f * v); // なめらかに
        }

        _ambientSettings.BaseVolume = ambientVolume * v;
        if (v > 0.001f)
        {
            if (!_ambient.isPlaying)
                _ambient.Play();
        }
        else if (_ambient.isPlaying)
        {
            _ambient.Stop();
        }
    }

    // ================= 仮の見た目 =================

    private static Texture2D MakeTexture(int n, bool ring)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float c = (n - 1) * 0.5f;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a;
                if (ring)
                    a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.76f) / 0.17f);   // 輪郭だけの丸
                else
                    a = 1f - Mathf.SmoothStep(0.5f, 1f, d);                  // やわらかい点
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static bool EnsureMaterials()
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null)
        {
            Debug.LogWarning("[RiverCurrent] Sprites/Default のシェーダーが見つからないため、仮の見た目を出しません。");
            return false;
        }
        if (s_mat == null)
            s_mat = new Material(sh);
        if (s_ringMat == null)
        {
            s_ringMat = new Material(sh);
            s_ringMat.mainTexture = MakeTexture(32, true);
        }
        if (s_dotMat == null)
        {
            s_dotMat = new Material(sh);
            s_dotMat.mainTexture = MakeTexture(16, false);
        }
        return true;
    }

    private static float Wave(float x, float phase, float amp, float len)
    {
        float l = Mathf.Max(0.1f, len);
        float a = Mathf.Sin(6.2831853f * (x / l + phase));
        float b = Mathf.Sin(6.2831853f * (x / (l * 0.47f) - phase * 1.3f) + 1.7f);
        return amp * (0.7f * a + 0.3f * b);
    }

    private LineRenderer MakeLine(string name, Color color, float width, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_visualRoot, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = _seg + 1;
        lr.sharedMaterial = s_mat;
        lr.startColor = color;
        lr.endColor = color;
        lr.widthMultiplier = width;
        // 手描き風：はしを細く、まん中を太く
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.08f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0.3f));
        lr.numCapVertices = 3;
        lr.alignment = LineAlignment.View;
        lr.sortingOrder = order;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    private ParticleSystem MakePS(string name, Material mat, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_visualRoot, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = order;
        return ps;
    }

    private static void FadeInOut(ParticleSystem ps, float peak)
    {
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.12f), new GradientAlphaKey(peak, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private void BuildVisuals()
    {
        if (!EnsureMaterials())
            return;

        // 見た目用の入れ物：スケールの影響を打ち消して、中の長さを、ワールドのユニットにそろえる
        GameObject root = new GameObject("TempWater (auto)");
        root.transform.SetParent(transform, false);
        root.layer = gameObject.layer;
        root.transform.localPosition = new Vector3(_col.offset.x, _col.offset.y, 0f);
        Vector3 ls = transform.lossyScale;
        root.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f);
        _visualRoot = root.transform;

        _w = _col.size.x * Mathf.Abs(ls.x);
        _h = _col.size.y * Mathf.Abs(ls.y);
        _seg = Mathf.Clamp(Mathf.CeilToInt(_w / 0.25f), 4, 400);
        int order = waterSortingOrder;

        // --- 水の本体（上のふちが、波に沿って動く）
        GameObject water = new GameObject("Water");
        water.transform.SetParent(_visualRoot, false);
        MeshFilter mf = water.AddComponent<MeshFilter>();
        MeshRenderer mr = water.AddComponent<MeshRenderer>();
        mr.sharedMaterial = s_mat;
        mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        _mesh = new Mesh();
        _mesh.name = "RiverWater";
        _verts = new Vector3[(_seg + 1) * 2];
        Color[] cols = new Color[_verts.Length];
        int[] tris = new int[_seg * 6];
        Color topC = waterColor;
        Color botC = new Color(waterColor.r, waterColor.g, waterColor.b, waterColor.a * 0.55f);
        for (int i = 0; i <= _seg; i++)
        {
            float x = -_w * 0.5f + _w * i / _seg;
            _verts[2 * i] = new Vector3(x, _h * 0.5f, 0f);
            _verts[2 * i + 1] = new Vector3(x, -_h * 0.5f, 0f);
            cols[2 * i] = topC;
            cols[2 * i + 1] = botC;
        }
        for (int i = 0; i < _seg; i++)
        {
            int a = 2 * i, b = 2 * i + 1, c = 2 * i + 2, d = 2 * i + 3;
            int t = i * 6;
            tris[t] = a; tris[t + 1] = c; tris[t + 2] = b;
            tris[t + 3] = b; tris[t + 4] = c; tris[t + 5] = d;
        }
        _mesh.vertices = _verts;
        _mesh.colors = cols;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
        mf.sharedMesh = _mesh;

        // --- 波の線：水面の手描き風の2本（前と後ろ）、水の中の細い2本
        _lines = new LineRenderer[4];
        _lines[0] = MakeLine("SurfaceFront", waveLineColor, 0.075f, order + 2);
        Color back = new Color(waveLineColor.r, waveLineColor.g, waveLineColor.b, waveLineColor.a * 0.6f);
        _lines[1] = MakeLine("SurfaceBack", back, 0.045f, order + 2);
        _lines[2] = MakeLine("Inner1", innerLineColor, 0.035f, order + 1);
        _lines[3] = MakeLine("Inner2", innerLineColor, 0.03f, order + 1);
        _linePos = new Vector3[_seg + 1];

        BuildParticles(order + 3);
        UpdateWaves(0f);
    }

    private void BuildParticles(int order)
    {
        Vector2 dir = flowDirection.normalized;

        // --- 泡：川底から、水面へ向かって上がる（輪郭だけの丸）
        if (bubbleRate > 0f)
        {
            _bubbles = MakePS("Bubbles", s_ringMat, order);
            float rise = 1f;
            ParticleSystem.MainModule main = _bubbles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Max(0.4f, _h * 0.5f / rise), Mathf.Max(0.8f, _h * 0.95f / rise));
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.26f);
            main.startColor = new Color(1f, 1f, 1f, 0.85f);
            main.maxParticles = 300;

            ParticleSystem.EmissionModule em = _bubbles.emission;
            em.rateOverTime = Mathf.Max(0.1f, bubbleRate * _w / 10f);

            ParticleSystem.ShapeModule sh = _bubbles.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(_w * 0.98f, 0.1f, 0.1f);
            sh.position = new Vector3(0f, -_h * 0.5f + 0.15f, 0f);

            ParticleSystem.VelocityOverLifetimeModule vel = _bubbles.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            vel.y = new ParticleSystem.MinMaxCurve(rise, rise);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            FadeInOut(_bubbles, 0.85f);
            _bubbles.Play();
        }

        // --- 流れる粒：流れの向きに、小さな白い点が流れる
        if (flowDotDensity > 0f)
        {
            _dots = MakePS("FlowDots", s_dotMat, order);
            float speed = Mathf.Max(0.3f, flowSpeed) * 0.9f;
            ParticleSystem.MainModule main = _dots.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            main.maxParticles = 250;

            ParticleSystem.EmissionModule em = _dots.emission;
            em.rateOverTime = Mathf.Clamp(_w * _h * flowDotDensity, 1f, 120f);

            ParticleSystem.ShapeModule sh = _dots.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(_w, _h * 0.9f, 0.1f);

            ParticleSystem.VelocityOverLifetimeModule vel = _dots.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(dir.x * speed * 0.85f, dir.x * speed * 1.15f);
            vel.y = new ParticleSystem.MinMaxCurve(dir.y * speed * 0.85f, dir.y * speed * 1.15f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            FadeInOut(_dots, 0.8f);
            _dotBuf = new ParticleSystem.Particle[main.maxParticles];
            _dots.Play();
        }

        // --- 水しぶき：水に入った時・出た時に、水面から跳ねる（ワールド座標で動かす）
        if (splashCount > 0)
        {
            _splash = MakePS("Splash", s_dotMat, order + 1);
            ParticleSystem.MainModule main = _splash.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.6f;
            main.startSpeed = 0f;
            main.maxParticles = 200;
            ParticleSystem.EmissionModule em = _splash.emission;
            em.enabled = false;
            ParticleSystem.ShapeModule sh = _splash.shape;
            sh.enabled = false;

            ParticleSystem.ColorOverLifetimeModule col = _splash.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            _splash.Play();
        }
    }

    private void SpawnSplash(float worldX)
    {
        if (_splash == null || _col == null)
            return;
        float surfaceY = _col.bounds.max.y;
        for (int i = 0; i < splashCount; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = new Vector3(worldX + Random.Range(-0.3f, 0.3f), surfaceY, 0f);
            ep.velocity = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(3f, 6f), 0f);
            ep.startLifetime = Random.Range(0.5f, 0.9f);
            ep.startSize = Random.Range(0.08f, 0.2f);
            ep.startColor = new Color(1f, 1f, 1f, 0.95f);
            _splash.Emit(ep, 1);
        }
    }

    private void UpdateWaves(float t)
    {
        if (_mesh == null || _lines == null)
            return;

        float dirSign = flowDirection.x >= 0f ? 1f : -1f;
        float ph = t * waveSpeed * dirSign;
        float top = _h * 0.5f;

        // 水の本体の、上のふち
        for (int i = 0; i <= _seg; i++)
        {
            float x = _verts[2 * i].x;
            _verts[2 * i].y = top + Wave(x, ph, waveHeight, waveLength);
        }
        _mesh.vertices = _verts;
        _mesh.RecalculateBounds();

        SetLine(_lines[0], top, 0f, ph, waveHeight, waveLength);
        SetLine(_lines[1], top - 0.07f, 0f, ph + 0.31f, waveHeight * 0.8f, waveLength * 1.1f);
        SetLine(_lines[2], top - _h * 0.33f, 0f, ph * 0.6f + 0.2f, waveHeight * 0.55f, waveLength * 1.3f);
        SetLine(_lines[3], top - _h * 0.66f, 0f, ph * 0.5f + 0.6f, waveHeight * 0.5f, waveLength * 1.5f);
    }

    private void SetLine(LineRenderer lr, float baseY, float z, float phase, float amp, float len)
    {
        for (int i = 0; i <= _seg; i++)
        {
            float x = -_w * 0.5f + _w * i / _seg;
            _linePos[i] = new Vector3(x, baseY + Wave(x, phase, amp, len), z);
        }
        lr.SetPositions(_linePos);
    }

    private void KeepDotsInside()
    {
        if (_dots == null || _dotBuf == null)
            return;
        int n = _dots.GetParticles(_dotBuf);
        bool changed = false;
        float hx = _w * 0.5f, hy = _h * 0.5f;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = _dotBuf[i].position;
            if (Mathf.Abs(p.x) > hx || Mathf.Abs(p.y) > hy)
            {
                _dotBuf[i].remainingLifetime = 0f;
                changed = true;
            }
        }
        if (changed)
            _dots.SetParticles(_dotBuf, n);
    }

    private void Update()
    {
        if (_visualRoot != null && !GamePause.IsPaused)
        {
            UpdateWaves(Time.time);
            KeepDotsInside();
        }
        UpdateAmbient();
    }

    // ================= シーン画面の目印 =================

    private void OnDrawGizmos()
    {
        BoxCollider2D c = GetComponent<BoxCollider2D>();
        if (c == null)
            return;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
        Bounds b = c.bounds;
        Gizmos.DrawWireCube(b.center, b.size);

        Vector2 dir = flowDirection.sqrMagnitude > 0.0001f ? flowDirection.normalized : Vector2.right;
        Vector3 a = b.center - (Vector3)(dir * Mathf.Min(b.extents.x, 1.5f));
        Vector3 e = b.center + (Vector3)(dir * Mathf.Min(b.extents.x, 1.5f));
        Gizmos.DrawLine(a, e);
        Vector3 side = new Vector3(-dir.y, dir.x, 0f) * 0.25f;
        Gizmos.DrawLine(e, e - (Vector3)(dir * 0.4f) + side);
        Gizmos.DrawLine(e, e - (Vector3)(dir * 0.4f) - side);
    }
}
