using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - ボス戦の、画面の、名前と体力ゲージ。
///
///   ・画面の下（主人公のハートの体力ゲージは左上なので、重ならない）から、せり上がってくる。
///     atBottom を false にすると、上の中央から、すべり出てくる。
///   ・ゲージが左から満ちる。
///   ・ダメージを受けると、赤いゲージが減り、少しあとに、白い「減った分」が追いかける。
///   ・半分のところに、白い目印。
///   ・後半（SetPhase2）になると、ゲージの色が変わる。
///
/// BossA が BossHpBar.Create(...) で作る。シーンには置かなくてよい。
/// 画面の手前の順番は 2500（死亡の暗転・ムービーの幕よりは、後ろ）。
/// </summary>
public class BossHpBar : MonoBehaviour
{
    private const float BarWidth = 1000f;
    private const float BarHeight = 30f;

    private RectTransform _panel;
    private RectTransform _fillRt;
    private RectTransform _trailRt;
    private Image _fillImg;
    private Color _fillColor = new Color(0.86f, 0.16f, 0.2f, 1f);

    private float _target = 1f;      // 本当の体力の割合
    private float _shown;            // 赤いゲージ
    private float _trail = 1f;       // 白い「減った分」
    private float _trailWait;
    private float _slide;            // 0〜1（すべり出る）
    private bool _hiding;
    private bool _filling = true;    // はじめに、ゲージが満ちている間
    private float _flash;
    private bool _atBottom = true;
    private Canvas _canvas;

    private const float BottomMargin = 44f;   // 画面の下から、ゲージまで
    private const float TopMargin = 36f;
    private const float HiddenOffset = 200f;  // 引っこんでいるときの、画面の外へのずれ

    /// <summary>名前とゲージを作る。</summary>
    public static BossHpBar Create(TMP_FontAsset font, string bossName)
    {
        return Create(font, bossName, true);
    }

    /// <summary>名前とゲージを作る。atBottom が true なら画面の下、false なら上の中央に出す。</summary>
    public static BossHpBar Create(TMP_FontAsset font, string bossName, bool atBottom)
    {
        GameObject go = new GameObject("BossHpBar");
        BossHpBar bar = go.AddComponent<BossHpBar>();
        bar._atBottom = atBottom;
        bar.Build(font, bossName);
        return bar;
    }

    /// <summary>本当の体力の割合（0〜1）を伝える。</summary>
    public void SetRatio(float ratio)
    {
        float r = Mathf.Clamp01(ratio);
        if (r < _target)
        {
            _flash = 0.12f;
            _trailWait = 0.45f;
        }
        _target = r;
    }

    /// <summary>後半に入ったときの、ゲージの色。</summary>
    public void SetPhase2()
    {
        _fillColor = new Color(1f, 0.45f, 0.1f, 1f);
        if (_fillImg != null)
            _fillImg.color = _fillColor;
    }

    /// <summary>ゲージを、引っこめて消す。</summary>
    public void Hide()
    {
        _hiding = true;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // キーコンフィグ・設定が開いている間は、画面の手前に出ないように隠す
        if (_canvas != null)
            _canvas.enabled = !GameOverlayState.IsBlocking;

        // すべり出る／引っこむ
        _slide = Mathf.MoveTowards(_slide, _hiding ? 0f : 1f, dt / (_hiding ? 0.5f : 0.6f));
        float e = 1f - (1f - _slide) * (1f - _slide);
        if (_panel != null)
            _panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(HiddenPosY(), ShownPosY(), e));
        if (_hiding && _slide <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // 赤いゲージ：はじめは左から満ちる。そのあとは、すばやく追いつく
        if (_slide >= 1f)
        {
            float speed = _filling ? 0.9f : 3.5f;
            _shown = Mathf.MoveTowards(_shown, _target, dt * speed);
            if (_filling && Mathf.Abs(_shown - _target) < 0.0001f)
                _filling = false;
        }

        // 白い「減った分」：少し待ってから、ゆっくり追いかける
        if (_filling)
        {
            _trail = _shown;
        }
        else if (_trail > _shown)
        {
            _trailWait -= dt;
            if (_trailWait <= 0f)
                _trail = Mathf.MoveTowards(_trail, _shown, dt * 0.45f);
        }
        else
        {
            _trail = _shown;
        }

        if (_fillRt != null)
            _fillRt.anchorMax = new Vector2(_shown, 1f);
        if (_trailRt != null)
            _trailRt.anchorMax = new Vector2(_trail, 1f);

        // ダメージを受けた瞬間、白くちかっとする
        if (_flash > 0f)
        {
            _flash -= dt;
            if (_fillImg != null)
                _fillImg.color = Color.Lerp(_fillColor, Color.white, Mathf.Clamp01(_flash / 0.12f));
        }
        else if (_fillImg != null && _fillImg.color != _fillColor)
        {
            _fillImg.color = _fillColor;
        }
    }

    private void Build(TMP_FontAsset font, string bossName)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        _canvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2500;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 全体（下の中央。または上の中央）
        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        _panel = (RectTransform)panel.transform;
        float ay = _atBottom ? 0f : 1f;
        _panel.anchorMin = new Vector2(0.5f, ay);
        _panel.anchorMax = new Vector2(0.5f, ay);
        _panel.pivot = new Vector2(0.5f, ay);
        _panel.sizeDelta = new Vector2(BarWidth, 110f);
        _panel.anchoredPosition = new Vector2(0f, HiddenPosY());

        // 名前
        TextMeshProUGUI name = NewText("Name", _panel, font, 54f, Color.white, TextAlignmentOptions.BottomLeft);
        name.text = string.IsNullOrEmpty(bossName) ? "" : bossName.Replace('　', ' ');
        name.fontStyle = FontStyles.Bold;
        RectTransform nr = name.rectTransform;
        nr.anchorMin = new Vector2(0f, 0f);
        nr.anchorMax = new Vector2(1f, 1f);
        nr.offsetMin = new Vector2(6f, BarHeight + 6f);
        nr.offsetMax = new Vector2(0f, 0f);
        Outline no = name.gameObject.AddComponent<Outline>();
        no.effectColor = new Color(0f, 0f, 0f, 0.9f);
        no.effectDistance = new Vector2(3f, -3f);

        // 枠
        Image frame = NewImage("Frame", _panel, new Color(0.04f, 0.04f, 0.07f, 0.95f));
        RectTransform fr = frame.rectTransform;
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(1f, 0f);
        fr.pivot = new Vector2(0.5f, 0f);
        fr.sizeDelta = new Vector2(0f, BarHeight);
        fr.anchoredPosition = Vector2.zero;
        Outline fo = frame.gameObject.AddComponent<Outline>();
        fo.effectColor = new Color(1f, 1f, 1f, 0.9f);
        fo.effectDistance = new Vector2(3f, -3f);

        // 中（4ピクセルの余白）
        GameObject inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(frame.transform, false);
        RectTransform ir = (RectTransform)inner.transform;
        ir.anchorMin = Vector2.zero;
        ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(4f, 4f);
        ir.offsetMax = new Vector2(-4f, -4f);

        // 白い「減った分」
        Image trail = NewImage("Trail", ir, new Color(1f, 0.95f, 0.85f, 0.95f));
        _trailRt = trail.rectTransform;
        _trailRt.anchorMin = Vector2.zero;
        _trailRt.anchorMax = new Vector2(1f, 1f);
        _trailRt.offsetMin = Vector2.zero;
        _trailRt.offsetMax = Vector2.zero;

        // 赤いゲージ
        _fillImg = NewImage("Fill", ir, _fillColor);
        _fillRt = _fillImg.rectTransform;
        _fillRt.anchorMin = Vector2.zero;
        _fillRt.anchorMax = new Vector2(0f, 1f);
        _fillRt.offsetMin = Vector2.zero;
        _fillRt.offsetMax = Vector2.zero;

        // 半分の目印
        Image tick = NewImage("Tick", ir, new Color(1f, 1f, 1f, 0.9f));
        RectTransform tr = tick.rectTransform;
        tr.anchorMin = new Vector2(0.5f, 0f);
        tr.anchorMax = new Vector2(0.5f, 1f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(3f, 0f);
        tr.anchoredPosition = Vector2.zero;

        _trailRt.anchorMax = new Vector2(0f, 1f);
    }

    // 出ているときの位置と、引っこんでいるときの位置（下なら上向き、上なら下向きが「出る」向き）
    private float ShownPosY()
    {
        return _atBottom ? BottomMargin : -TopMargin;
    }

    private float HiddenPosY()
    {
        return _atBottom ? -(110f + HiddenOffset * 0.2f) : HiddenOffset - 40f;
    }

    private static Image NewImage(string objName, Transform parent, Color color)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        Image im = g.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        return im;
    }

    private static TextMeshProUGUI NewText(string objName, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        if (font != null)
            t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }
}
