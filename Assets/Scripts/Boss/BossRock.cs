using UnityEngine;

/// <summary>
/// KEY ESCAPE - ボスが降らせる、落石。
///
/// 流れ：
///   1) 床に、赤い印が出る（ここに落ちる）。石は天井のあたりで、ぶるぶる震える。
///   2) 少しあと、石が落ちてくる（だんだん速くなる）。
///   3) 主人公に当たるとダメージ。床に着くと、砕けて消える。
///
/// BossA から BossRock.Spawn(...) で作る。シーンには置かなくてよい。
/// </summary>
public class BossRock : MonoBehaviour
{
    private enum State { Warn, Fall, Done }

    private State _state = State.Warn;
    private float _t;
    private float _warn;
    private float _groundY;
    private float _size;
    private float _damage;
    private float _fallSpeed;
    private float _gravity = 42f;
    private bool _hit;
    private float _spin;
    private Vector3 _startPos;

    private SpriteRenderer _sr;
    private GameObject _marker;
    private SpriteRenderer _markerSr;
    private float _markerBaseW;

    private AudioClip _breakSfx;
    private float _sfxVolume = 1f;

    /// <summary>
    /// 落石をひとつ作る。
    /// x：落ちる場所　topY：石が最初にいる高さ　groundY：床の高さ　size：石の大きさ　warn：印が出てから落ちるまでの秒数
    /// </summary>
    public static BossRock Spawn(Sprite sprite, float x, float topY, float groundY, float size, float warn,
        float damage, AudioClip breakSfx, float sfxVolume)
    {
        GameObject go = new GameObject("BossRock");
        go.transform.position = new Vector3(x, topY, 0f);
        BossRock r = go.AddComponent<BossRock>();
        r.Init(sprite, groundY, size, warn, damage, breakSfx, sfxVolume);
        return r;
    }

    private void Init(Sprite sprite, float groundY, float size, float warn, float damage, AudioClip breakSfx, float sfxVolume)
    {
        _groundY = groundY;
        _size = Mathf.Max(0.3f, size);
        _warn = Mathf.Max(0.1f, warn);
        _damage = damage;
        _breakSfx = breakSfx;
        _sfxVolume = sfxVolume;
        _startPos = transform.position;
        _spin = Random.Range(-120f, 120f);

        // 石の絵（無ければ、灰色の丸い四角）
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = 20;
        if (sprite != null)
        {
            _sr.sprite = sprite;
            float big = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            transform.localScale = Vector3.one * (_size / Mathf.Max(0.01f, big));
        }
        else
        {
            _sr.sprite = BossSpriteUtil.Round();
            _sr.color = new Color(0.5f, 0.5f, 0.55f, 1f);
            transform.localScale = Vector3.one * (_size / 0.64f);
        }

        // 床の印
        _marker = new GameObject("BossRock_Marker");
        _marker.transform.position = new Vector3(transform.position.x, _groundY + 0.06f, 0f);
        _markerSr = _marker.AddComponent<SpriteRenderer>();
        _markerSr.sprite = BossSpriteUtil.Radial();
        _markerSr.color = new Color(0.95f, 0.15f, 0.15f, 0f);
        _markerSr.sortingOrder = 4;
        _markerBaseW = _size * 2.0f / 1.28f;
        _marker.transform.localScale = new Vector3(_markerBaseW * 0.5f, _markerBaseW * 0.22f, 1f);
    }

    /// <summary>ボスが倒れたときなどに、静かに消す。</summary>
    public void Cancel()
    {
        if (_marker != null)
            Destroy(_marker);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_marker != null)
            Destroy(_marker);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (_state == State.Warn)
        {
            _t += dt;
            float u = Mathf.Clamp01(_t / _warn);
            // 印：だんだん大きく、濃くなる（ちかちか点滅）
            float blink = 0.75f + 0.25f * Mathf.Sin(_t * 28f);
            float a = Mathf.Lerp(0.15f, 0.75f, u) * blink;
            _markerSr.color = new Color(0.95f, 0.15f, 0.15f, a);
            float w = _markerBaseW * Mathf.Lerp(0.5f, 1f, u);
            _marker.transform.localScale = new Vector3(w, w * 0.22f, 1f);
            // 石：ぶるぶる震える
            float shake = 0.03f + 0.07f * u;
            transform.position = _startPos + new Vector3(Mathf.Sin(_t * 70f) * shake, Mathf.Cos(_t * 53f) * shake * 0.5f, 0f);
            if (_t >= _warn)
            {
                _state = State.Fall;
                transform.position = _startPos;
                _fallSpeed = 0f;
            }
            return;
        }

        if (_state == State.Fall)
        {
            _fallSpeed += _gravity * dt;
            Vector3 p = transform.position;
            p.y -= _fallSpeed * dt;
            transform.position = p;
            transform.Rotate(0f, 0f, _spin * dt);

            if (!_hit)
                CheckPlayer();

            if (p.y - _size * 0.5f <= _groundY)
                Break();
        }
    }

    private void CheckPlayer()
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, _size * 0.45f);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || !cols[i].CompareTag("Player"))
                continue;
            CharacterController2D cc = cols[i].GetComponentInParent<CharacterController2D>();
            if (cc == null)
                continue;
            _hit = true;
            cc.ApplyDamage(_damage, transform.position);
            Break();
            return;
        }
    }

    private void Break()
    {
        if (_state == State.Done)
            return;
        _state = State.Done;

        BossBits.Burst(new Vector3(transform.position.x, Mathf.Max(transform.position.y, _groundY + 0.2f), 0f),
            new Color(0.55f, 0.55f, 0.6f, 1f), 12, 6f, 0.2f, 0.9f, 21, 16f);
        BossSpriteUtil.Shake(0.2f, 0.1f);
        BossSpriteUtil.PlaySfx(_breakSfx, _sfxVolume);

        if (_marker != null)
            Destroy(_marker);
        Destroy(gameObject);
    }
}
