using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ボスがダメージを受けたときに、目の前に流れる、川（水のかたまり）。
///
/// 一瞬で消えるが、主人公は、水に流されて、ボスから強制的にはなされる。
/// その間（Push Seconds）、主人公の操作は止まる。
///
/// BossA から BossRiverBurst.Spawn(...) で作る。シーンには置かなくてよい。
/// </summary>
public class BossRiverBurst : MonoBehaviour
{
    private class Wave
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector2 vel;
        public Color color;
        public Vector3 scale;
        public float phase;
    }

    private readonly List<Wave> _waves = new List<Wave>();
    private SpriteRenderer _sheet;
    private SpriteRenderer _foam;
    private Color _sheetColor;
    private Color _foamColor;
    private float _width;
    private float _height;
    private float _dir;
    private float _t;
    private float _seconds;

    private GameObject _player;
    private Rigidbody2D _playerRb;
    private PlayerMovement _playerMove;
    private float _pushDir;
    private float _pushSpeed;
    private float _pushSeconds;
    private bool _locked;

    /// <summary>
    /// 水を流す。feet：ボスの足元の、前の位置　dir：水が流れる向き（右が+1）　pushDir：主人公を飛ばす向き
    /// </summary>
    public static BossRiverBurst Spawn(Vector3 feet, float dir, float width, float height, float seconds,
        GameObject player, float pushDir, float pushSpeed, float pushSeconds, int sortingOrder)
    {
        GameObject go = new GameObject("BossRiverBurst");
        go.transform.position = feet;
        BossRiverBurst b = go.AddComponent<BossRiverBurst>();
        b.Init(dir, width, height, seconds, player, pushDir, pushSpeed, pushSeconds, sortingOrder);
        return b;
    }

    private void Init(float dir, float width, float height, float seconds, GameObject player,
        float pushDir, float pushSpeed, float pushSeconds, int order)
    {
        _dir = dir >= 0f ? 1f : -1f;
        _width = Mathf.Max(0.5f, width);
        _height = Mathf.Max(0.5f, height);
        _seconds = Mathf.Max(0.2f, seconds);
        _player = player;
        _pushDir = pushDir >= 0f ? 1f : -1f;
        _pushSpeed = pushSpeed;
        _pushSeconds = pushSeconds;

        if (_player != null)
        {
            _playerRb = _player.GetComponent<Rigidbody2D>();
            _playerMove = _player.GetComponent<PlayerMovement>();
        }

        // 水の面（低い四角）
        _sheetColor = new Color(0.22f, 0.52f, 0.95f, 0.6f);
        _sheet = MakePart("Sheet", BossSpriteUtil.White(), _sheetColor, order);
        // あわ（上のふち）
        _foamColor = new Color(0.9f, 0.97f, 1f, 0.85f);
        _foam = MakePart("Foam", BossSpriteUtil.White(), _foamColor, order + 2);

        // 波のかたまり
        int n = 16;
        for (int i = 0; i < n; i++)
        {
            Wave w = new Wave();
            GameObject g = new GameObject("Wave");
            g.transform.SetParent(transform, false);
            float fx = Random.Range(0f, _width * 0.6f) * _dir;
            float fy = Random.Range(0.05f, _height * 0.7f);
            g.transform.localPosition = new Vector3(fx, fy, 0f);
            Vector3 sc = new Vector3(Random.Range(0.7f, 1.5f), Random.Range(0.4f, 1.0f), 1f);
            g.transform.localScale = sc;
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = BossSpriteUtil.Round();
            Color c = i % 3 == 0 ? new Color(0.85f, 0.95f, 1f, 0.8f) : new Color(0.25f, 0.6f, 1f, 0.75f);
            sr.color = c;
            sr.sortingOrder = order + 1;
            w.tr = g.transform;
            w.sr = sr;
            w.color = c;
            w.scale = sc;
            w.vel = new Vector2(_dir * Random.Range(_width * 1.4f, _width * 2.6f) / _seconds, Random.Range(0.5f, 2.2f));
            w.phase = Random.Range(0f, 6.28f);
            _waves.Add(w);
        }

        SetShape(0f, 1f);
    }

    private SpriteRenderer MakePart(string name, Sprite sprite, Color color, int order)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(transform, false);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void Start()
    {
        StartCoroutine(PushRoutine());
        Destroy(gameObject, Mathf.Max(_seconds, _pushSeconds) + 0.1f);
    }

    private void OnDestroy()
    {
        Unlock();
    }

    private void Unlock()
    {
        if (_locked && _playerMove != null)
            _playerMove.SetInputLocked(false);
        _locked = false;
    }

    /// <summary>水の面の大きさ。rush：0〜1（水が勢いよく広がる割合）</summary>
    private void SetShape(float rush, float bob)
    {
        float w = _width * rush;
        float h = _height * 0.42f * bob;
        _sheet.transform.localPosition = new Vector3(_dir * w * 0.5f, h * 0.5f, 0f);
        _sheet.transform.localScale = new Vector3(Mathf.Max(0.01f, w), Mathf.Max(0.01f, h), 1f);
        _foam.transform.localPosition = new Vector3(_dir * w * 0.5f, h, 0f);
        _foam.transform.localScale = new Vector3(Mathf.Max(0.01f, w), 0.14f, 1f);
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float u = Mathf.Clamp01(_t / _seconds);
        float rush = Mathf.Clamp01(_t / (_seconds * 0.3f));
        rush = 1f - (1f - rush) * (1f - rush);
        float bob = 1f + 0.18f * Mathf.Sin(_t * 30f);
        SetShape(rush, bob);

        // うしろ半分から、すうっと消える
        float fade = 1f - Mathf.Clamp01((u - 0.4f) / 0.6f);
        Color sc = _sheetColor;
        sc.a *= fade;
        _sheet.color = sc;
        Color fc = _foamColor;
        fc.a *= fade;
        _foam.color = fc;

        for (int i = 0; i < _waves.Count; i++)
        {
            Wave w = _waves[i];
            if (w.tr == null)
                continue;
            Vector3 p = w.tr.localPosition;
            p.x += w.vel.x * Time.deltaTime;
            p.y += (w.vel.y + Mathf.Sin(_t * 18f + w.phase) * 1.2f) * Time.deltaTime;
            w.tr.localPosition = p;
            float s = Mathf.Lerp(1f, 0.6f, u);
            w.tr.localScale = new Vector3(w.scale.x * s, w.scale.y * s, 1f);
            Color c = w.color;
            c.a *= fade;
            w.sr.color = c;
        }
    }

    /// <summary>主人公を、ボスから遠ざかる向きへ、勢いよく流す。</summary>
    private IEnumerator PushRoutine()
    {
        if (_playerRb == null)
            yield break;

        if (_playerMove != null)
        {
            _playerMove.SetInputLocked(true);
            _locked = true;
        }

        float t = 0f;
        while (t < _pushSeconds)
        {
            yield return new WaitForFixedUpdate();
            if (_playerRb == null)
                break;
            t += Time.fixedDeltaTime;
            // だんだん弱まる
            float k = 1f - Mathf.Clamp01(t / _pushSeconds) * 0.5f;
            _playerRb.velocity = new Vector2(_pushDir * _pushSpeed * k, _playerRb.velocity.y);
        }

        Unlock();
    }
}
