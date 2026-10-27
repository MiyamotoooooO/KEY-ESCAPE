using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ボスを倒したあとの、おいわい。
///
///   ・持っているキーのうち、最大4つ（BossA の Party Key Count）が、画面の右と左から走って登場する。
///   ・位置につくと、ディスコ・ぴょんぴょん・万歳などを、ランダムに切り替えて踊る。
///   ・その間、空にいくつも花火が上がる（最後は連続で打ち上がる）。
///   ・終わると、キーたちはすうっと消える。
///
/// BossA が BossVictoryParty.Play(...) で作る。シーンには置かなくてよい。
/// 終わったら IsDone が true になり、自分で消える。
/// ゲームを止めている間（Tabのキーコンフィグなど）は、時間も止まる。
/// </summary>
public class BossVictoryParty : MonoBehaviour
{
    private class Runner
    {
        public GameObject go;
        public LimbRig rig;
        public Vector3 baseScale;
        public float startX;
        public float targetX;
        public float delay;
        public float speed;
        public bool arrived;
        public bool fromLeft;
        public List<SpriteRenderer> srs = new List<SpriteRenderer>();
        public List<Color> srColors = new List<Color>();
        public List<TMPro.TextMeshPro> tms = new List<TMPro.TextMeshPro>();
        public List<Color> tmColors = new List<Color>();
    }

    private static readonly LimbRig.KeyMotion[] DanceMotions =
    {
        LimbRig.KeyMotion.Disco,
        LimbRig.KeyMotion.Hop,
        LimbRig.KeyMotion.Cheer,
        LimbRig.KeyMotion.Sway,
        LimbRig.KeyMotion.Wave,
    };

    private static readonly Color[] FireColors =
    {
        new Color(1f, 0.35f, 0.35f, 1f),
        new Color(1f, 0.85f, 0.3f, 1f),
        new Color(0.4f, 0.9f, 1f, 1f),
        new Color(0.55f, 1f, 0.55f, 1f),
        new Color(1f, 0.55f, 0.95f, 1f),
        new Color(1f, 0.6f, 0.25f, 1f),
        new Color(0.95f, 0.95f, 1f, 1f),
    };

    private readonly List<Runner> _runners = new List<Runner>();
    private bool _dancing;
    private bool _ended;
    private bool _fireworks;

    private float _centerX;
    private float _camX;
    private float _camY;
    private float _halfW = 9f;
    private float _halfH = 5f;
    private float _groundY;
    private float _danceSeconds;
    private int _order;
    private int _layerId;
    private AudioClip _launchSfx;
    private AudioClip _burstSfx;
    private float _sfxVolume;

    /// <summary>おいわいが終わったか。</summary>
    public bool IsDone { get; private set; }

    /// <summary>
    /// おいわいを始める。
    /// keys=登場させるキー、prefab=手足の生えたキー（KeyNPC_Visual）、scale=大きさの倍率、groundY=床の高さ、
    /// centerX=カメラが無いときの中心、leftLimit/rightLimit=動ける範囲、danceSeconds=おどる秒数、
    /// order/layerId=絵の手前の順番と層、launchSfx/burstSfx=花火の音（空なら鳴らさない）。
    /// </summary>
    public static BossVictoryParty Play(List<KeyDefinition> keys, GameObject prefab, float scale, float groundY,
        float centerX, float leftLimit, float rightLimit, float danceSeconds, int order, int layerId,
        AudioClip launchSfx, AudioClip burstSfx, float sfxVolume)
    {
        GameObject go = new GameObject("BossVictoryParty");
        BossVictoryParty p = go.AddComponent<BossVictoryParty>();
        p._centerX = centerX;
        p._groundY = groundY;
        p._danceSeconds = Mathf.Max(2f, danceSeconds);
        p._order = order;
        p._layerId = layerId;
        p._launchSfx = launchSfx;
        p._burstSfx = burstSfx;
        p._sfxVolume = sfxVolume;
        p.StartCoroutine(p.Run(keys, prefab, scale, leftLimit, rightLimit));
        return p;
    }

    // ================= 全体の流れ =================

    private IEnumerator Run(List<KeyDefinition> keys, GameObject prefab, float scale, float leftLimit, float rightLimit)
    {
        ReadCamera();

        // 登場させるキーを作る
        int n = keys != null ? keys.Count : 0;
        int leftIndex = 0;
        int rightIndex = 0;
        for (int i = 0; i < n && prefab != null; i++)
        {
            bool fromLeft = (i % 2 == 0);
            Runner r = CreateRunner(prefab, keys[i], scale);
            r.fromLeft = fromLeft;
            r.delay = i * 0.28f + Random.Range(0f, 0.15f);
            r.speed = Random.Range(7.5f, 9.5f);

            float offset;
            if (fromLeft)
            {
                offset = 2.8f + 2.3f * leftIndex;
                leftIndex++;
                r.startX = _camX - _halfW - 2.5f;
                r.targetX = _camX - offset;
            }
            else
            {
                offset = 2.8f + 2.3f * rightIndex;
                rightIndex++;
                r.startX = _camX + _halfW + 2.5f;
                r.targetX = _camX + offset;
            }
            r.targetX = Mathf.Clamp(r.targetX, leftLimit + 0.8f, rightLimit - 0.8f);

            r.go.transform.position = new Vector3(r.startX, _groundY, 0f);
            SetFacing(r, r.targetX > r.startX);
            _runners.Add(r);
            StartCoroutine(RunIn(r));
        }

        // 花火を始める
        _fireworks = true;
        StartCoroutine(FireworkLoop());

        // おどる秒数のあいだ待つ（全員が着くまでは、少し延ばす）
        float t = 0f;
        float guard = 0f;
        while (true)
        {
            t += Time.deltaTime;
            guard += Time.deltaTime;
            bool all = AllArrived();
            if (t >= _danceSeconds && all)
                break;
            if (guard >= _danceSeconds + 4f)
                break;
            yield return null;
        }

        // 最後の連続花火
        _fireworks = false;
        for (int i = 0; i < 7; i++)
        {
            LaunchOne(true);
            yield return new WaitForSeconds(0.12f);
        }
        for (int i = 0; i < _runners.Count; i++)
        {
            if (_runners[i].rig != null)
                _runners[i].rig.SetMotion(LimbRig.KeyMotion.Cheer);
        }
        yield return new WaitForSeconds(1.2f);

        // すうっと消える
        _ended = true;
        _dancing = false;
        float fade = 0.8f;
        float ft = 0f;
        while (ft < fade)
        {
            ft += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(ft / fade);
            for (int i = 0; i < _runners.Count; i++)
                SetAlpha(_runners[i], k);
            yield return null;
        }

        for (int i = 0; i < _runners.Count; i++)
        {
            if (_runners[i].go != null)
                Destroy(_runners[i].go);
        }
        _runners.Clear();

        IsDone = true;
        Destroy(gameObject, 0.1f);
    }

    private bool AllArrived()
    {
        for (int i = 0; i < _runners.Count; i++)
        {
            if (!_runners[i].arrived)
                return false;
        }
        return true;
    }

    private void ReadCamera()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            _camX = cam.transform.position.x;
            _camY = cam.transform.position.y;
            _halfH = cam.orthographicSize;
            _halfW = cam.orthographicSize * cam.aspect;
        }
        else
        {
            _camX = _centerX;
            _camY = _groundY + 4f;
        }
    }

    // ================= キーの登場とダンス =================

    private Runner CreateRunner(GameObject prefab, KeyDefinition key, float scale)
    {
        Runner r = new Runner();
        r.go = Instantiate(prefab);
        r.go.name = "PartyKey_" + (key != null ? key.displayName : "?");
        r.go.SetActive(true);

        Vector3 s = prefab.transform.localScale;
        r.baseScale = new Vector3(Mathf.Abs(s.x) * scale, s.y * scale, s.z);
        r.go.transform.localScale = r.baseScale;

        r.rig = r.go.GetComponentInChildren<LimbRig>(true);

        KeyLabel[] labels = r.go.GetComponentsInChildren<KeyLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].keyOverride = key;
            labels[i].textOverride = "";
            labels[i].Refresh();
        }

        Renderer[] rs = r.go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            rs[i].sortingLayerID = _layerId;
            rs[i].sortingOrder = _order + 10 + rs[i].sortingOrder;
        }

        SpriteRenderer[] sa = r.go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sa.Length; i++)
        {
            r.srs.Add(sa[i]);
            r.srColors.Add(sa[i].color);
        }
        TMPro.TextMeshPro[] ta = r.go.GetComponentsInChildren<TMPro.TextMeshPro>(true);
        for (int i = 0; i < ta.Length; i++)
        {
            r.tms.Add(ta[i]);
            r.tmColors.Add(ta[i].color);
        }

        if (r.rig != null)
            r.rig.SetManualVelocity(0f);
        return r;
    }

    // 絵はもともと左向き。右へ向けるときは、横を反転する
    private static void SetFacing(Runner r, bool right)
    {
        if (r.go == null)
            return;
        Vector3 s = r.baseScale;
        s.x = right ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
        r.go.transform.localScale = s;
    }

    private IEnumerator RunIn(Runner r)
    {
        // 画面の外で、順番を待つ
        float w = 0f;
        while (w < r.delay)
        {
            w += Time.deltaTime;
            yield return null;
        }

        // 走って来る
        while (r.go != null && Mathf.Abs(r.go.transform.position.x - r.targetX) > 0.05f)
        {
            float dir = r.targetX > r.go.transform.position.x ? 1f : -1f;
            Vector3 p = r.go.transform.position;
            p.x = Mathf.MoveTowards(p.x, r.targetX, r.speed * Time.deltaTime);
            r.go.transform.position = p;
            SetFacing(r, dir > 0f);
            if (r.rig != null)
                r.rig.SetManualVelocity(dir * r.speed);
            yield return null;
        }
        if (r.go == null)
            yield break;

        r.arrived = true;
        if (r.rig != null)
            r.rig.SetManualVelocity(0f);
        if (_ended)
            yield break;

        // まん中（主人公のほう）を向いて、とびはねてから、おどる
        SetFacing(r, _camX > r.go.transform.position.x);
        BossBits.Burst(new Vector3(r.go.transform.position.x, _groundY + 0.2f, 0f), new Color(1f, 0.95f, 0.7f, 1f),
            8, 5f, 0.14f, 0.6f, _order + 5, 12f);

        _dancing = true;
        while (_dancing && r.go != null)
        {
            if (r.rig != null)
                r.rig.SetMotion(DanceMotions[Random.Range(0, DanceMotions.Length)]);
            float d = Random.Range(1.1f, 2.0f);
            float t = 0f;
            while (t < d && _dancing && r.go != null)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    private static void SetAlpha(Runner r, float k)
    {
        for (int i = 0; i < r.srs.Count; i++)
        {
            if (r.srs[i] == null)
                continue;
            Color c = r.srColors[i];
            c.a *= k;
            r.srs[i].color = c;
        }
        for (int i = 0; i < r.tms.Count; i++)
        {
            if (r.tms[i] == null)
                continue;
            Color c = r.tmColors[i];
            c.a *= k;
            r.tms[i].color = c;
        }
    }

    // ================= 花火 =================

    private IEnumerator FireworkLoop()
    {
        yield return new WaitForSeconds(0.3f);
        while (_fireworks)
        {
            LaunchOne(false);
            // たまに2連発
            if (Random.value < 0.3f)
            {
                yield return new WaitForSeconds(0.12f);
                LaunchOne(false);
            }
            yield return new WaitForSeconds(Random.Range(0.35f, 0.7f));
        }
    }

    private void LaunchOne(bool finale)
    {
        float x = _camX + Random.Range(-_halfW * 0.85f, _halfW * 0.85f);
        float topY = _camY + _halfH * (finale ? 0.7f : 0.6f);
        float lowY = Mathf.Max(_groundY + 4.5f, _camY - _halfH * 0.05f);
        float y = Random.Range(lowY, Mathf.Max(lowY + 0.5f, topY));
        Color c = FireColors[Random.Range(0, FireColors.Length)];
        BossFirework.Launch(new Vector3(x + Random.Range(-1f, 1f), _groundY, 0f), new Vector3(x, y, 0f), c,
            _order + 25, _layerId, _launchSfx, _burstSfx, _sfxVolume, finale);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _runners.Count; i++)
        {
            if (_runners[i] != null && _runners[i].go != null)
                Destroy(_runners[i].go);
        }
    }
}

/// <summary>
/// 花火1発：ひゅーっと上がって、ぱっと開く。BossFirework.Launch(...) で呼ぶ。
/// </summary>
public class BossFirework : MonoBehaviour
{
    private Vector3 _from;
    private Vector3 _to;
    private Color _color;
    private int _order;
    private int _layerId;
    private AudioClip _burstSfx;
    private float _vol;
    private bool _finale;
    private float _t;
    private float _rise;
    private float _trailTimer;
    private SpriteRenderer _spark;

    public static void Launch(Vector3 from, Vector3 to, Color color, int order, int layerId,
        AudioClip launchSfx, AudioClip burstSfx, float volume, bool finale)
    {
        GameObject go = new GameObject("BossFirework");
        go.transform.position = from;
        BossFirework f = go.AddComponent<BossFirework>();
        f._from = from;
        f._to = to;
        f._color = color;
        f._order = order;
        f._layerId = layerId;
        f._burstSfx = burstSfx;
        f._vol = volume;
        f._finale = finale;
        f._rise = Random.Range(0.6f, 0.9f);

        GameObject sg = new GameObject("Spark");
        sg.transform.SetParent(go.transform, false);
        sg.transform.localScale = Vector3.one * 0.5f;
        f._spark = sg.AddComponent<SpriteRenderer>();
        f._spark.sprite = BossSpriteUtil.Radial();
        f._spark.color = new Color(1f, 0.95f, 0.75f, 1f);
        f._spark.sortingLayerID = layerId;
        f._spark.sortingOrder = order;

        BossSpriteUtil.PlaySfx(launchSfx, volume * 0.6f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;
        float u = Mathf.Clamp01(_t / _rise);
        float e = 1f - (1f - u) * (1f - u);   // はじめ速く、だんだんゆっくり
        transform.position = Vector3.Lerp(_from, _to, e);

        _trailTimer -= dt;
        if (_trailTimer <= 0f)
        {
            _trailTimer = 0.045f;
            BossBits.Burst(transform.position, new Color(1f, 0.9f, 0.6f, 1f), 2, 0.6f, 0.08f, 0.35f, _order - 1, 2f);
        }

        if (u >= 1f)
        {
            Explode();
            Destroy(gameObject);
        }
    }

    private void Explode()
    {
        BossSpriteUtil.PlaySfx(_burstSfx, _vol);
        BossFireworkBurst.Spawn(_to, _color, _order, _layerId, _finale);
    }
}

/// <summary>
/// 花火の「ぱっと開く」部分：丸く広がる火花と、ひかり。
/// </summary>
public class BossFireworkBurst : MonoBehaviour
{
    private class Spark
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector2 vel;
        public Color color;
        public float twinkle;
    }

    private readonly List<Spark> _sparks = new List<Spark>();
    private SpriteRenderer _flash;
    private float _t;
    private float _life;

    public static void Spawn(Vector3 pos, Color color, int order, int layerId, bool big)
    {
        GameObject go = new GameObject("BossFireworkBurst");
        go.transform.position = pos;
        BossFireworkBurst b = go.AddComponent<BossFireworkBurst>();
        b._life = big ? 1.9f : 1.6f;

        // 一瞬のひかり
        GameObject fg = new GameObject("Flash");
        fg.transform.SetParent(go.transform, false);
        fg.transform.localScale = Vector3.one * (big ? 9f : 7f);
        b._flash = fg.AddComponent<SpriteRenderer>();
        b._flash.sprite = BossSpriteUtil.Radial();
        b._flash.color = new Color(color.r, color.g, color.b, 0.55f);
        b._flash.sortingLayerID = layerId;
        b._flash.sortingOrder = order - 2;

        // 外側のリング
        int outer = big ? 38 : 30;
        float outerSpeed = big ? Random.Range(8.5f, 10.5f) : Random.Range(7f, 9f);
        b.AddRing(outer, outerSpeed, color, order, layerId, 0.17f);

        // 内側のリング（色を変える）
        Color inner = Color.Lerp(color, Color.white, 0.55f);
        b.AddRing(outer / 2, outerSpeed * 0.55f, inner, order + 1, layerId, 0.13f);
    }

    private void AddRing(int count, float speed, Color color, int order, int layerId, float size)
    {
        float offset = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < count; i++)
        {
            float ang = offset + (Mathf.PI * 2f) * i / count;
            float sp = speed * Random.Range(0.92f, 1.08f);

            GameObject g = new GameObject("Spark");
            g.transform.SetParent(transform, false);
            g.transform.localScale = Vector3.one * (size / 0.64f);
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = BossSpriteUtil.Radial();
            sr.color = color;
            sr.sortingLayerID = layerId;
            sr.sortingOrder = order;

            Spark s = new Spark();
            s.tr = g.transform;
            s.sr = sr;
            s.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
            s.color = color;
            s.twinkle = Random.Range(0f, 6.28f);
            _sparks.Add(s);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;

        float fade = 1f - Mathf.Clamp01((_t - _life * 0.5f) / (_life * 0.5f));

        if (_flash != null)
        {
            float f = 1f - Mathf.Clamp01(_t / 0.3f);
            Color c = _flash.color;
            c.a = 0.55f * f;
            _flash.color = c;
        }

        float drag = Mathf.Exp(-1.8f * dt);
        for (int i = 0; i < _sparks.Count; i++)
        {
            Spark s = _sparks[i];
            if (s.tr == null)
                continue;
            s.vel *= drag;
            s.vel.y -= 4.5f * dt;
            s.tr.position += new Vector3(s.vel.x, s.vel.y, 0f) * dt;

            float tw = 0.7f + 0.3f * Mathf.Sin(_t * 30f + s.twinkle);
            Color c = s.color;
            c.a = fade * tw;
            s.sr.color = c;
        }

        if (_t >= _life)
            Destroy(gameObject);
    }
}
