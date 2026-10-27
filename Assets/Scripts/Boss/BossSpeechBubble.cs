using TMPro;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ボスの頭の上に出る、せりふ（吹き出し）。
///
///   ・ぽんっと出て、文字が1文字ずつ出る（数文字ごとに、しゃべる音）。
///   ・ボスが動いても、頭の上についていく。ボスが左右を向いても、文字は裏返らない。
///   ・しばらくして、すうっと消える。
///
/// BossA が BossSpeechBubble.Show(...) で作る。シーンには置かなくてよい。
/// ゲームを止めている間は、時間も止まる。
/// </summary>
public class BossSpeechBubble : MonoBehaviour
{
    private const float PopSeconds = 0.22f;
    private const float FadeSeconds = 0.45f;

    private Transform _follow;
    private Vector3 _offset;
    private float _seconds;
    private float _charsPerSecond;
    private AudioClip _talkSfx;
    private float _volume;

    private TextMeshPro _text;
    private SpriteRenderer[] _renders;
    private Color[] _colors;
    private Color _textColor;

    private float _t;
    private float _shown;
    private int _total;
    private int _lastCount;
    private int _soundCounter;
    private bool _hiding;
    private float _hideT;

    /// <summary>
    /// せりふを出す。
    /// follow=ついていく物（ボス）、offset=そこからの位置（頭の上）、seconds=出しておく秒数、
    /// charsPerSecond=1秒に出る文字数、order/layerId=絵の手前の順番と層、talkSfx=しゃべる音（空なら、会話の音）。
    /// </summary>
    public static BossSpeechBubble Show(Transform follow, Vector3 offset, string message, TMP_FontAsset font,
        float seconds, float charsPerSecond, int order, int layerId, AudioClip talkSfx, float volume)
    {
        GameObject go = new GameObject("BossSpeechBubble");
        BossSpeechBubble b = go.AddComponent<BossSpeechBubble>();
        b._follow = follow;
        b._offset = offset;
        b._seconds = Mathf.Max(1f, seconds);
        b._charsPerSecond = charsPerSecond;
        b._talkSfx = talkSfx;
        b._volume = volume;
        b.Build(message, font, order, layerId);
        return b;
    }

    /// <summary>すぐに消し始める。</summary>
    public void Hide()
    {
        if (_hiding)
            return;
        _hiding = true;
        _hideT = 0f;
    }

    private void Build(string message, TMP_FontAsset font, int order, int layerId)
    {
        // 文字（先に全文を入れて、大きさを測る）
        GameObject tg = new GameObject("Text");
        tg.transform.SetParent(transform, false);
        _text = tg.AddComponent<TextMeshPro>();
        if (font != null)
            _text.font = font;
        _text.text = message;
        _text.fontSize = 6f;
        _text.fontStyle = FontStyles.Bold;
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableWordWrapping = true;
        _text.color = Color.white;
        _text.sortingLayerID = layerId;
        _text.sortingOrder = order + 3;
        RectTransform tr = tg.GetComponent<RectTransform>();
        tr.sizeDelta = new Vector2(11f, 3f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = Vector2.zero;
        _text.ForceMeshUpdate();
        Vector2 size = _text.textBounds.size;
        _total = _text.textInfo.characterCount;
        _textColor = _text.color;

        float w = Mathf.Max(3f, size.x + 1.2f);
        float h = Mathf.Max(1.3f, size.y + 0.8f);
        Vector3 center = _text.textBounds.center;
        tg.transform.localPosition = new Vector3(-center.x, -center.y, 0f);

        Sprite round = BossSpriteUtil.Round();
        Sprite white = BossSpriteUtil.White();

        // ふち（赤）
        SpriteRenderer edge = NewSprite("Edge", round, new Color(0.95f, 0.2f, 0.2f, 1f), layerId, order);
        edge.transform.localScale = new Vector3((w + 0.3f) / 0.64f, (h + 0.3f) / 0.64f, 1f);
        // 中（黒）
        SpriteRenderer back = NewSprite("Back", round, new Color(0.07f, 0.03f, 0.05f, 0.95f), layerId, order + 1);
        back.transform.localScale = new Vector3(w / 0.64f, h / 0.64f, 1f);

        // しっぽ（下むきの三角のかわりに、ななめにした四角）
        SpriteRenderer tailEdge = NewSprite("TailEdge", white, new Color(0.95f, 0.2f, 0.2f, 1f), layerId, order);
        tailEdge.transform.localPosition = new Vector3(0f, -h * 0.5f - 0.05f, 0f);
        tailEdge.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tailEdge.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
        SpriteRenderer tail = NewSprite("Tail", white, new Color(0.07f, 0.03f, 0.05f, 0.95f), layerId, order + 1);
        tail.transform.localPosition = new Vector3(0f, -h * 0.5f + 0.02f, 0f);
        tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.transform.localScale = new Vector3(0.45f, 0.45f, 1f);

        _renders = new SpriteRenderer[] { edge, back, tailEdge, tail };
        _colors = new Color[_renders.Length];
        for (int i = 0; i < _renders.Length; i++)
            _colors[i] = _renders[i].color;

        // 文字は、1文字ずつ出す
        if (_charsPerSecond > 0f && _total > 0)
            _text.maxVisibleCharacters = 0;
        else
            _text.maxVisibleCharacters = int.MaxValue;

        transform.localScale = Vector3.zero;
        UpdatePosition();
    }

    private SpriteRenderer NewSprite(string objName, Sprite sprite, Color color, int layerId, int order)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(transform, false);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerID = layerId;
        sr.sortingOrder = order;
        return sr;
    }

    private void UpdatePosition()
    {
        if (_follow != null)
            transform.position = _follow.position + _offset;
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;

        // ぽんっと出る（少し大きくなってから、おさまる）
        float scale;
        if (_hiding)
            scale = 1f;
        else
        {
            float u = Mathf.Clamp01(_t / PopSeconds);
            if (u < 1f)
                scale = 1.12f * Mathf.Sin(u * Mathf.PI * 0.5f);
            else
                scale = 1f + 0.12f * Mathf.Exp(-10f * (_t - PopSeconds)) * Mathf.Cos(24f * (_t - PopSeconds));
        }
        transform.localScale = new Vector3(scale, scale, 1f);

        // 文字が1文字ずつ出る
        if (_charsPerSecond > 0f && _text != null && _text.maxVisibleCharacters < _total && _t >= PopSeconds * 0.6f)
        {
            _shown += _charsPerSecond * dt;
            int count = Mathf.Min(_total, Mathf.FloorToInt(_shown));
            if (count > _lastCount)
            {
                TalkSound(_lastCount, count);
                _lastCount = count;
                _text.maxVisibleCharacters = count;
            }
        }

        // しばらくして消える
        if (!_hiding && _t >= _seconds)
            Hide();

        if (_hiding)
        {
            _hideT += dt;
            float k = 1f - Mathf.Clamp01(_hideT / FadeSeconds);
            for (int i = 0; i < _renders.Length; i++)
            {
                if (_renders[i] == null)
                    continue;
                Color c = _colors[i];
                c.a *= k;
                _renders[i].color = c;
            }
            if (_text != null)
            {
                Color tc = _textColor;
                tc.a *= k;
                _text.color = tc;
            }
            if (_hideT >= FadeSeconds)
                Destroy(gameObject);
        }
    }

    private void TalkSound(int from, int to)
    {
        TMP_TextInfo info = _text.textInfo;
        for (int i = from; i < to && i < info.characterCount; i++)
        {
            if (!info.characterInfo[i].isVisible)
                continue;
            _soundCounter++;
            if (_soundCounter >= 2)
            {
                _soundCounter = 0;
                if (_talkSfx != null)
                    BossSpriteUtil.PlaySfx(_talkSfx, _volume);
                else
                    SoundEffects.Play(SEId.DialogueTalk, 0.03f);
            }
        }
    }
}
