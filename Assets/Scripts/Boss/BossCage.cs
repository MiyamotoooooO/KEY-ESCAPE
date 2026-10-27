using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ボスが投げてくる「檻」。当たると、キーが1つ、封じられる（KeySeal）。
///
///   ・ボスの手から、主人公のいた場所へ向かって飛ぶ（空中を弧をえがく）。
///   ・落ちたあとも、少しのあいだ、地面をすべる。
///   ・飛んでいる間と、すべっている間に、主人公に触れると、檻に入ったキー（檻の中に文字が見える）が封じられる。
///   ・ジャンプで上をこえるか、走ってよければ、当たらない（無敵の間も、すり抜ける）。
///   ・当たらなければ、すべり終わって、こわれる。
///
/// BossA が BossCage.Spawn(...) で作る。シーンには置かなくてよい。
/// </summary>
public class BossCage : MonoBehaviour
{
    private KeyDefinition _key;
    private Vector3 _from;
    private float _landX;
    private float _landY;
    private float _groundY;
    private float _size;
    private float _flight;
    private float _slide;
    private float _sealSeconds;
    private float _damage;
    private float _slideSpeed;
    private float _slideDir;
    private int _order;
    private AudioClip _hitSfx;
    private float _sfxVolume;

    private float _t;
    private bool _landed;
    private bool _done;
    private Transform _body;
    private readonly List<SpriteRenderer> _renders = new List<SpriteRenderer>();
    private TextMeshPro _letter;
    private SpriteRenderer _glow;

    /// <summary>
    /// 檻を飛ばす。
    /// from=手の位置、landX=落とす場所のx、groundY=床の高さ、key=入っているキー、
    /// size=檻の大きさ、flight=飛ぶ秒数、slide=落ちてからすべる秒数、sealSeconds=封じる秒数、
    /// damage=当たったときのダメージ（0ならなし）、order=絵の手前の順番、layerId=絵の層。
    /// </summary>
    public static BossCage Spawn(Vector3 from, float landX, float groundY, KeyDefinition key, float size,
        float flight, float slide, float sealSeconds, float damage, int order, int layerId,
        TMP_FontAsset font, AudioClip hitSfx, float sfxVolume)
    {
        GameObject go = new GameObject("BossCage");
        go.transform.position = from;
        BossCage c = go.AddComponent<BossCage>();
        c._key = key;
        c._from = from;
        c._landX = landX;
        c._groundY = groundY;
        c._size = Mathf.Max(0.5f, size);
        c._landY = groundY + c._size * 0.55f;
        c._flight = Mathf.Max(0.3f, flight);
        c._slide = Mathf.Max(0f, slide);
        c._sealSeconds = sealSeconds;
        c._damage = damage;
        c._order = order;
        c._hitSfx = hitSfx;
        c._sfxVolume = sfxVolume;
        float dir = landX - from.x;
        c._slideDir = Mathf.Abs(dir) < 0.01f ? 1f : Mathf.Sign(dir);
        c._slideSpeed = 7.5f;
        c.Build(layerId, font);
        return c;
    }

    // ================= 見た目 =================

    private SpriteRenderer NewSprite(string objName, Transform parent, Vector2 localPos, Vector2 scale, Color color, int orderOffset, int layerId, Sprite sprite)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        g.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerID = layerId;
        sr.sortingOrder = _order + orderOffset;
        _renders.Add(sr);
        return sr;
    }

    private void Build(int layerId, TMP_FontAsset font)
    {
        float s = _size;

        // まわりの赤い光（あぶないしるし）
        GameObject glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(transform, false);
        glowGo.transform.localScale = Vector3.one * (s * 2.6f / 1.28f);
        _glow = glowGo.AddComponent<SpriteRenderer>();
        _glow.sprite = BossSpriteUtil.Radial();
        _glow.color = new Color(1f, 0.25f, 0.2f, 0.45f);
        _glow.sortingLayerID = layerId;
        _glow.sortingOrder = _order + 10;

        // 回る本体（檻）
        GameObject bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(transform, false);
        _body = bodyGo.transform;

        Sprite white = BossSpriteUtil.White();
        Color steel = new Color(0.8f, 0.82f, 0.88f, 1f);
        Color dark = new Color(0.07f, 0.08f, 0.13f, 0.82f);

        NewSprite("Back", _body, Vector2.zero, new Vector2(s, s), dark, 11, layerId, white);

        int bars = 5;
        for (int i = 0; i < bars; i++)
        {
            float f = (i + 0.5f) / bars - 0.5f;
            NewSprite("Bar" + i, _body, new Vector2(f * s, 0f), new Vector2(s * 0.07f, s), steel, 13, layerId, white);
        }
        NewSprite("Top", _body, new Vector2(0f, s * 0.5f - s * 0.06f), new Vector2(s * 1.04f, s * 0.12f), steel, 14, layerId, white);
        NewSprite("Bottom", _body, new Vector2(0f, -s * 0.5f + s * 0.06f), new Vector2(s * 1.04f, s * 0.12f), steel, 14, layerId, white);

        // 檻の中のキー（回らない。どのキーが封じられるか、見えるように）
        GameObject capGo = new GameObject("KeyCap");
        capGo.transform.SetParent(transform, false);
        SpriteRenderer cap = capGo.AddComponent<SpriteRenderer>();
        cap.sprite = BossSpriteUtil.Round();
        cap.color = new Color(0.96f, 0.96f, 0.98f, 1f);
        cap.sortingLayerID = layerId;
        cap.sortingOrder = _order + 12;
        capGo.transform.localScale = Vector3.one * (s * 0.5f / 0.64f);
        _renders.Add(cap);

        GameObject letterGo = new GameObject("Letter");
        letterGo.transform.SetParent(transform, false);
        letterGo.transform.localPosition = new Vector3(0f, 0f, 0f);
        _letter = letterGo.AddComponent<TextMeshPro>();
        if (font != null)
            _letter.font = font;
        _letter.text = _key != null ? (string.IsNullOrEmpty(_key.displayName) ? _key.keyCode.ToString() : _key.displayName) : "?";
        _letter.alignment = TextAlignmentOptions.Center;
        _letter.fontStyle = FontStyles.Bold;
        _letter.fontSize = 4.8f * s;
        _letter.color = new Color(0.1f, 0.1f, 0.16f, 1f);
        _letter.sortingLayerID = layerId;
        _letter.sortingOrder = _order + 13;
        RectTransform lr = letterGo.GetComponent<RectTransform>();
        if (lr != null)
            lr.sizeDelta = new Vector2(s, s);
    }

    // ================= 動き =================

    private void Update()
    {
        if (_done)
            return;

        float dt = Time.deltaTime;
        _t += dt;

        if (!_landed)
        {
            float u = Mathf.Clamp01(_t / _flight);
            float x = Mathf.Lerp(_from.x, _landX, u);
            // 落ちるほど速くなり、途中で少しふくらむ（投げた感じ）
            float y = Mathf.Lerp(_from.y, _landY, u * u) + 1.4f * Mathf.Sin(u * Mathf.PI) * (1f - u);
            transform.position = new Vector3(x, y, 0f);
            if (_body != null)
                _body.localRotation = Quaternion.Euler(0f, 0f, -_slideDir * 720f * u);

            if (u >= 1f)
            {
                _landed = true;
                _t = 0f;
                transform.position = new Vector3(_landX, _landY, 0f);
                BossSpriteUtil.Shake(0.18f, 0.08f);
                BossBits.Burst(new Vector3(_landX, _groundY + 0.15f, 0f), new Color(0.75f, 0.76f, 0.82f, 1f),
                    8, 5f, 0.14f, 0.5f, _order + 20, 14f);
            }
        }
        else
        {
            float u = _slide > 0.01f ? Mathf.Clamp01(_t / _slide) : 1f;
            float speed = _slideSpeed * (1f - u);
            Vector3 p = transform.position;
            p.x += _slideDir * speed * dt;
            transform.position = p;
            if (_body != null)
                _body.Rotate(0f, 0f, -_slideDir * 260f * (1f - u) * dt);

            if (u >= 1f)
            {
                Break(false);
                return;
            }
            // 消えかけは、点滅
            if (u > 0.7f)
                SetAlpha(0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_t * 24f)));
        }

        if (_glow != null)
        {
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 14f);
            Color c = _glow.color;
            c.a = 0.45f * pulse;
            _glow.color = c;
        }

        CheckHit();
    }

    private void SetAlpha(float a)
    {
        for (int i = 0; i < _renders.Count; i++)
        {
            if (_renders[i] == null)
                continue;
            Color c = _renders[i].color;
            c.a = a;
            _renders[i].color = c;
        }
    }

    private void CheckHit()
    {
        Collider2D[] cols = Physics2D.OverlapBoxAll(transform.position, new Vector2(_size * 0.9f, _size * 0.9f), 0f);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || !cols[i].CompareTag("Player"))
                continue;
            CharacterController2D cc = cols[i].GetComponentInParent<CharacterController2D>();
            if (cc == null)
                continue;
            if (cc.invincible || cc.life <= 0f)
                continue;   // 無敵の間は、すり抜ける

            OnHit(cc);
            return;
        }
    }

    private void OnHit(CharacterController2D cc)
    {
        _done = true;

        // 入っているキーが、もう封じられている／割り当てから外れているなら、別のキーにする
        List<KeyDefinition> list = KeySeal.PickableKeys();
        KeyDefinition k = _key;
        if (k == null || !list.Contains(k))
            k = list.Count > 0 ? list[Random.Range(0, list.Count)] : null;

        if (k != null)
            KeySeal.Seal(k, _sealSeconds);

        if (_damage > 0f)
            cc.ApplyDamage(_damage, transform.position);

        BossSpriteUtil.PlaySfx(_hitSfx, _sfxVolume);
        BossSpriteUtil.Shake(0.3f, 0.12f);
        BossBits.Burst(transform.position, new Color(0.85f, 0.87f, 0.95f, 1f), 14, 7f, 0.18f, 0.8f, _order + 20, 12f);
        BossBits.Burst(transform.position, new Color(1f, 0.8f, 0.3f, 1f), 8, 5f, 0.12f, 0.6f, _order + 20, 8f);
        Destroy(gameObject);
    }

    private void Break(bool quiet)
    {
        _done = true;
        if (!quiet)
            BossBits.Burst(transform.position, new Color(0.75f, 0.76f, 0.82f, 1f), 10, 5f, 0.16f, 0.6f, _order + 20, 14f);
        Destroy(gameObject);
    }

    /// <summary>ボスが倒れたときなどに、消す。</summary>
    public void Cancel()
    {
        if (_done)
            return;
        Break(false);
    }
}
