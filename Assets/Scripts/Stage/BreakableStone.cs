using UnityEngine;

/// <summary>
/// KEY ESCAPE - 攻撃で壊せる石。道をふさぐ。
/// 主人公が攻撃を当てるたびに、石がゆれて、かけらが少し飛び、ひびが増える。
/// 決めた回数（初期値は3回）当てると、砕けて消える。画面も少しゆれる。
///
/// ・壊れたあとは、そのまま消える。やられてチェックポイントから再開した時も、壊れたまま（レバーと同じ仕組みで覚える）。
///   チェックポイントを通らずに、最初からやり直した時は、石も元にもどる。
/// ・大きさ（Size）は、Inspector で決める。絵と当たり判定が、いっしょに伸び縮みする。
///   置く位置は、石の「足もと（下の真ん中）」。地面の上に、そのまま置けばよい。
/// ・攻撃が当たったかの判定は、Attack.cs が行い、この石の Hit() を呼ぶ。
///
/// 使い方：
///   1. メニュー「Tools > KEY ESCAPE > ステージ部品 > 壊せる石のプレハブを作る」で、プレハブを作る。
///   2. 道をふさぐ場所に置く。Size で、大きさを変える。
///
/// 絵の組み立て（プレハブ）：
///   BreakableStone … このスクリプト・BoxCollider2D（固い箱。ジャンプでは越えられない）
///     └ Visual … 石の絵
///         ├ Crack1 … 1回目のあとに重なる、ひび
///         └ Crack2 … 2回目のあとに重なる、ひび（Crack1 のかわりに出る）
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BreakableStone : MonoBehaviour
{
    [Header("基本")]
    [Tooltip("何回、攻撃を当てると壊れるか。")]
    public int hitsToBreak = 3;
    [Tooltip("石の大きさ（横, 縦）。ワールドの単位。絵と当たり判定が、この大きさに合わせて伸び縮みする。")]
    public Vector2 size = new Vector2(1.6f, 2.2f);
    [Tooltip("一度攻撃を受けたあと、次を受け付けるまでの秒数（一振りで、二回当たらないため）。")]
    public float hitCooldown = 0.2f;
    [Tooltip("ONなら、壊したあと、やられてチェックポイントから再開しても、壊れたままにする。")]
    public bool rememberBroken = true;
    [Tooltip("石の名前（IDと呼ぶ）。壊れたことを覚えるために使う。空なら、オブジェクトの名前と位置から自動で決まる。")]
    public string stoneId = "";

    [Header("絵")]
    [Tooltip("石の絵（SpriteRenderer）。ゆれる時は、この絵だけ動く。")]
    public SpriteRenderer visual;
    [Tooltip("ひびの絵。1つ目は、はじめて当てたあと、2つ目は、さらに当てたあとに出る（いちばん新しいものだけ出る）。")]
    public SpriteRenderer[] crackLayers;

    [Header("演出")]
    [Tooltip("当たった時に、石がゆれる秒数。")]
    public float shakeSeconds = 0.18f;
    [Tooltip("当たった時の、石のゆれの大きさ。")]
    public float shakeAmount = 0.08f;
    [Tooltip("当たった時の、画面のゆれの大きさ。0ならゆらさない。")]
    public float hitCameraShake = 0.03f;
    [Tooltip("砕けた時の、画面のゆれの大きさ。0ならゆらさない。")]
    public float breakCameraShake = 0.12f;

    [Header("効果音（空なら鳴らさない）")]
    public AudioClip hitClip;
    [Range(0f, 1f)] public float hitVolume = 0.8f;
    public AudioClip breakClip;
    [Range(0f, 1f)] public float breakVolume = 1f;

    [Header("調べる用")]
    [Tooltip("オンなら、当たるたびに Console に出す。")]
    public bool debugLog = false;

    private BoxCollider2D _col;
    private int _hits;
    private bool _broken;
    private float _lastHit = -10f;
    private float _shakeT;
    private string _id;

    private ParticleSystem _bits, _dust;
    private static Material s_squareMat, s_dotMat;

    // ================= 初期化 =================

    private void Reset()
    {
        _col = GetComponent<BoxCollider2D>();
        _col.isTrigger = false;
        ApplyShape();
    }

    private void OnValidate()
    {
        if (hitsToBreak < 1)
            hitsToBreak = 1;
        if (size.x < 0.2f)
            size.x = 0.2f;
        if (size.y < 0.2f)
            size.y = 0.2f;
        ApplyShape();
    }

    private void Awake()
    {
        _col = GetComponent<BoxCollider2D>();
        _col.isTrigger = false;
        ApplyShape();
        UpdateCracks();
        BuildParticles();
    }

    private void Start()
    {
        // すでに壊したことがあるなら、最初から無い
        if (rememberBroken)
        {
            bool broken;
            if (LeverState.TryGet(Id(), out broken) && broken)
            {
                _broken = true;
                Destroy(gameObject);
            }
        }
    }

    private string Id()
    {
        if (_id != null)
            return _id;
        if (!string.IsNullOrEmpty(stoneId))
        {
            _id = "stone:" + stoneId;
        }
        else
        {
            Vector3 p = transform.position;
            _id = "stone:" + name + ":" + p.x.ToString("F1") + ":" + p.y.ToString("F1");
        }
        return _id;
    }

    /// <summary>大きさに合わせて、絵と当たり判定を整える。足もと（下の真ん中）が、このオブジェクトの位置。</summary>
    public void ApplyShape()
    {
        BoxCollider2D col = _col != null ? _col : GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.size = size;
            col.offset = new Vector2(0f, size.y * 0.5f);
        }
        if (visual == null)
            return;

        Vector2 spriteSize = visual.sprite != null ? (Vector2)visual.sprite.bounds.size : Vector2.one;
        if (spriteSize.x < 0.0001f || spriteSize.y < 0.0001f)
            spriteSize = Vector2.one;

        Transform vt = visual.transform;
        if (vt != transform)
        {
            vt.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            vt.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        }
    }

    // ================= 攻撃を受ける =================

    /// <summary>攻撃が当たった。from は、攻撃の位置（かけらが飛ぶ向きに使う）。壊れたら true。</summary>
    public bool Hit(Vector3 from)
    {
        if (_broken)
            return false;
        if (Time.time - _lastHit < hitCooldown)
            return false;
        _lastHit = Time.time;

        _hits++;
        if (debugLog)
            Debug.Log("[BreakableStone] " + name + " に当たった（" + _hits + "/" + hitsToBreak + "）", this);

        if (_hits >= hitsToBreak)
        {
            Break(from);
            return true;
        }

        _shakeT = shakeSeconds;
        UpdateCracks();
        SpawnBits(from, 5, 0.12f);
        if (hitClip != null)
            AudioManager.PlaySE(hitClip, hitVolume);
        ShakeCamera(0.1f, hitCameraShake);
        return false;
    }

    private void UpdateCracks()
    {
        if (crackLayers == null)
            return;
        int layers = crackLayers.Length;
        int level = 0;
        if (layers > 0 && hitsToBreak > 1)
            level = Mathf.Clamp(Mathf.CeilToInt(_hits * layers / (float)(hitsToBreak - 1)), 0, layers);
        for (int i = 0; i < layers; i++)
        {
            if (crackLayers[i] != null)
                crackLayers[i].enabled = (i == level - 1);
        }
    }

    private void Break(Vector3 from)
    {
        _broken = true;
        if (visual != null)
            visual.enabled = false;
        if (crackLayers != null)
        {
            for (int i = 0; i < crackLayers.Length; i++)
                if (crackLayers[i] != null)
                    crackLayers[i].enabled = false;
        }
        if (_col != null)
            _col.enabled = false;

        SpawnBits(from, 26, 0.26f);
        SpawnDust();
        if (breakClip != null)
            AudioManager.PlaySE(breakClip, breakVolume);
        ShakeCamera(0.2f, breakCameraShake);

        if (rememberBroken)
            LeverState.Set(Id(), true);

        // かけらが落ちきるまで待ってから、消す
        Destroy(gameObject, 2.5f);
    }

    private void ShakeCamera(float seconds, float amount)
    {
        if (amount <= 0f)
            return;
        CameraFollow cam = FindObjectOfType<CameraFollow>();
        if (cam != null)
            cam.ShakeCamera(seconds, amount);
    }

    private void Update()
    {
        if (visual == null || visual.transform == transform)
            return;
        if (_broken)
            return;

        if (_shakeT > 0f)
        {
            _shakeT -= Time.deltaTime;
            float k = Mathf.Clamp01(_shakeT / Mathf.Max(0.01f, shakeSeconds));
            Vector3 basePos = new Vector3(0f, size.y * 0.5f, 0f);
            visual.transform.localPosition = basePos + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.4f, 0.4f), 0f) * (shakeAmount * k);
        }
        else if (visual.transform.localPosition != new Vector3(0f, size.y * 0.5f, 0f))
        {
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        }
    }

    // ================= かけら・砂 =================

    private static bool EnsureMaterials()
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null)
        {
            Debug.LogWarning("[BreakableStone] Sprites/Default のシェーダーが見つからないため、かけらを出せません。");
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
        int order = (visual != null ? visual.sortingOrder : 0) + 3;

        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });

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
        bc.color = new ParticleSystem.MinMaxGradient(g);
        _bits.Play();

        _dust = MakePS("Dust (auto)", s_dotMat, order);
        ParticleSystem.MainModule dm = _dust.main;
        dm.loop = false;
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

    /// <summary>石の中から、かけらを飛ばす。攻撃の位置から、石の中心へ向かって（石の反対がわへ）飛ぶ。</summary>
    private void SpawnBits(Vector3 from, int count, float maxSize)
    {
        if (_bits == null)
            return;
        Vector3 c = transform.position + new Vector3(0f, size.y * 0.5f, 0f);
        float dir = c.x >= from.x ? 1f : -1f;
        Color light = new Color(0.9f, 0.9f, 0.88f, 1f);
        Color mid = new Color(0.62f, 0.62f, 0.6f, 1f);
        Color dark = new Color(0.12f, 0.12f, 0.14f, 1f);
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = c + new Vector3(Random.Range(-size.x * 0.4f, size.x * 0.4f), Random.Range(-size.y * 0.4f, size.y * 0.4f), 0f);
            ep.velocity = new Vector3(dir * Random.Range(0.5f, 4.5f) + Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 6f), 0f);
            ep.startLifetime = Random.Range(0.6f, 1.1f);
            ep.startSize = Random.Range(maxSize * 0.35f, maxSize);
            ep.rotation = Random.Range(0f, 360f);
            ep.angularVelocity = Random.Range(-360f, 360f);
            float r = Random.value;
            ep.startColor = r < 0.5f ? light : (r < 0.8f ? mid : dark);
            _bits.Emit(ep, 1);
        }
    }

    private void SpawnDust()
    {
        if (_dust == null)
            return;
        Vector3 bottom = transform.position;
        for (int i = 0; i < 14; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = bottom + new Vector3(Random.Range(-size.x * 0.5f, size.x * 0.5f), Random.Range(0.05f, size.y * 0.5f), 0f);
            ep.velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(0.3f, 1.8f), 0f);
            ep.startLifetime = Random.Range(0.4f, 0.8f);
            ep.startSize = Random.Range(0.12f, 0.3f);
            ep.startColor = new Color(0.66f, 0.66f, 0.64f, 0.9f);
            _dust.Emit(ep, 1);
        }
    }

    // ================= シーン画面の目印 =================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.9f);
        Vector3 c = transform.position + new Vector3(0f, size.y * 0.5f, 0f);
        Gizmos.DrawWireCube(c, new Vector3(size.x, size.y, 0f));
    }
}
