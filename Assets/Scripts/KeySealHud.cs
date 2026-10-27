using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - 封じられたキーを、画面の上に吊るされた檻として見せる。
///
///   ・キーが封じられると、檻が上から落ちてきて、鎖でぶら下がる（ゆらゆら揺れる）。
///   ・檻の中に、封じられたキーの文字。檻の下に、のこり時間のゲージ（のこりが少ないと赤くなり、檻が震える）。
///   ・時間が来て解けると、檻が開いて、キーが飛び出し、檻は上へ消える。
///   ・やられてやり直す時などに、いっせいに解けたときは、演出なしで消える。
///   ・キーが封じられた瞬間に、画面の右上に「Kキーが消えた…?」と知らせが出る（しばらくして消える）。
///
/// KeySeal が、最初にキーを封じたときに自動で作る（シーンには置かなくてよい）。
/// 画面の手前の順番は 2300（ボスの体力ゲージ・死亡の暗転よりは、後ろ）。
/// </summary>
public class KeySealHud : MonoBehaviour
{
    private const float ChainLength = 150f;
    private const float CageW = 124f;
    private const float CageH = 140f;
    private const float Spacing = 168f;
    private const float BaseX = 300f;      // 画面の中央から、右へずらした位置から並べる
    private const float DropFrom = 460f;
    private const float CloseSeconds = 0.65f;

    // 右上の知らせ
    private const float NoticeW = 560f;
    private const float NoticeH = 80f;
    private const float NoticeTop = 36f;
    private const float NoticeRight = 36f;
    private const float NoticeGap = 10f;
    private const float NoticeSlide = 0.35f;
    private const float NoticeHold = 2.8f;
    private const float NoticeFade = 0.5f;

    private static readonly Color Steel = new Color(0.8f, 0.82f, 0.88f, 1f);

    private class Item
    {
        public KeyDefinition key;
        public RectTransform root;
        public RectTransform keycap;
        public Image fill;
        public Image[] bars;
        public CanvasGroup group;
        public float age;
        public float x;
        public float phase;
        public bool closing;
        public float closeT;
    }

    private class Notice
    {
        public RectTransform root;
        public CanvasGroup group;
        public float age;
        public float y;
    }

    private readonly List<Item> _items = new List<Item>();
    private readonly List<Notice> _notices = new List<Notice>();
    private RectTransform _area;
    private Canvas _canvas;

    private void Awake()
    {
        GameObject cgo = new GameObject("KeySealHudCanvas");
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.AddComponent<Canvas>();
        _canvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2300;
        CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        _area = cgo.GetComponent<RectTransform>();

        KeySeal.Changed += Sync;
        KeySeal.Released += OnReleased;
    }

    private void OnDestroy()
    {
        KeySeal.Changed -= Sync;
        KeySeal.Released -= OnReleased;
    }

    // ================= 増えた／減った =================

    private Item Find(KeyDefinition key)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (!_items[i].closing && _items[i].key == key)
                return _items[i];
        }
        return null;
    }

    private int ActiveCount()
    {
        int n = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            if (!_items[i].closing)
                n++;
        }
        return n;
    }

    private void Sync()
    {
        // 新しく封じられたキーの檻を作る
        IReadOnlyList<KeySeal.Entry> entries = KeySeal.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (Find(entries[i].key) == null)
            {
                _items.Add(CreateItem(entries[i].key));
                AddNotice(entries[i].key);   // 「Kキーが消えた…?」
            }
        }

        // なくなったキーの檻を消す（開く演出の最中のものは、そのまま）
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            Item it = _items[i];
            if (it.closing)
                continue;
            if (!KeySeal.IsSealed(it.key))
            {
                if (it.root != null)
                    Destroy(it.root.gameObject);
                _items.RemoveAt(i);
            }
        }
    }

    private void OnReleased(KeyDefinition key)
    {
        Item it = Find(key);
        if (it == null)
            return;
        it.closing = true;
        it.closeT = 0f;
    }

    // ================= 右上の知らせ =================

    private void AddNotice(KeyDefinition key)
    {
        Notice n = new Notice();

        GameObject g = new GameObject("SealNotice", typeof(RectTransform));
        g.transform.SetParent(_area, false);
        RectTransform rt = (RectTransform)g.transform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(NoticeW, NoticeH);
        n.y = -(NoticeTop + _notices.Count * (NoticeH + NoticeGap));
        rt.anchoredPosition = new Vector2(NoticeW + 60f, n.y);
        n.root = rt;
        n.group = g.AddComponent<CanvasGroup>();
        n.group.interactable = false;
        n.group.blocksRaycasts = false;

        Image bg = NewImage("Back", rt, new Color(0.42f, 0.05f, 0.08f, 0.94f));
        Stretch(bg.rectTransform);
        Outline bo = bg.gameObject.AddComponent<Outline>();
        bo.effectColor = new Color(1f, 1f, 1f, 0.9f);
        bo.effectDistance = new Vector2(3f, -3f);

        Image accent = NewImage("Accent", rt, new Color(1f, 0.82f, 0.25f, 1f));
        RectTransform ar = accent.rectTransform;
        ar.anchorMin = new Vector2(0f, 0f);
        ar.anchorMax = new Vector2(0f, 1f);
        ar.pivot = new Vector2(0f, 0.5f);
        ar.sizeDelta = new Vector2(12f, 0f);
        ar.anchoredPosition = Vector2.zero;

        string name = key != null ? (string.IsNullOrEmpty(key.displayName) ? key.keyCode.ToString() : key.displayName) : "?";
        TextMeshProUGUI t = NewText("Text", rt, KeySeal.HudFont, 40f, Color.white);
        t.text = "<color=#FFD84A>" + name + "</color>キーが消えた…?";
        t.richText = true;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;
        t.fontSizeMin = 16f;
        t.fontSizeMax = 40f;
        Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(30f, 6f);
        t.rectTransform.offsetMax = new Vector2(-14f, -6f);
        Outline to = t.gameObject.AddComponent<Outline>();
        to.effectColor = new Color(0f, 0f, 0f, 0.9f);
        to.effectDistance = new Vector2(2f, -2f);

        _notices.Add(n);
    }

    private void UpdateNotices(float dt)
    {
        for (int i = _notices.Count - 1; i >= 0; i--)
        {
            if (_notices[i].root == null)
                _notices.RemoveAt(i);
            else
                _notices[i].age += dt;
        }

        int slot = 0;
        for (int i = 0; i < _notices.Count; i++)
        {
            Notice n = _notices[i];

            if (n.age >= NoticeHold + NoticeFade)
            {
                Destroy(n.root.gameObject);
                _notices.RemoveAt(i);
                i--;
                continue;
            }

            float targetY = -(NoticeTop + slot * (NoticeH + NoticeGap));
            slot++;
            n.y = Mathf.Lerp(n.y, targetY, 1f - Mathf.Exp(-10f * dt));

            // 右からすべりこんで、少し震える
            float u = Mathf.Clamp01(n.age / NoticeSlide);
            float e = 1f - (1f - u) * (1f - u) * (1f - u);
            float x = Mathf.Lerp(NoticeW + 60f, -NoticeRight, e);
            if (n.age < 0.45f)
                x += Mathf.Sin(n.age * 70f) * 8f * (1f - n.age / 0.45f);
            n.root.anchoredPosition = new Vector2(x, n.y);

            // しばらくして、すうっと消える
            if (n.group != null)
                n.group.alpha = 1f - Mathf.Clamp01((n.age - NoticeHold) / NoticeFade);
        }
    }

    // ================= 作る =================

    private Item CreateItem(KeyDefinition key)
    {
        Item it = new Item();
        it.key = key;
        it.x = BaseX + ActiveCount() * Spacing;
        it.phase = Random.Range(0f, 6.28f);

        GameObject rg = new GameObject("SealItem", typeof(RectTransform));
        rg.transform.SetParent(_area, false);
        RectTransform root = (RectTransform)rg.transform;
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = new Vector2(CageW, ChainLength + CageH + 24f);
        root.anchoredPosition = new Vector2(it.x, DropFrom);
        it.root = root;
        it.group = rg.AddComponent<CanvasGroup>();
        it.group.interactable = false;
        it.group.blocksRaycasts = false;

        // 鎖
        Image chain = NewImage("Chain", root, new Color(0.62f, 0.64f, 0.7f, 1f));
        RectTransform cr = chain.rectTransform;
        cr.anchorMin = new Vector2(0.5f, 1f);
        cr.anchorMax = new Vector2(0.5f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(8f, ChainLength);
        cr.anchoredPosition = Vector2.zero;
        AddOutline(chain.gameObject, 0.6f);

        // 鎖のわっか（上）
        Image ring = NewImage("Ring", root, Steel);
        ring.sprite = BossSpriteUtil.Round();
        ring.type = Image.Type.Sliced;
        RectTransform rr = ring.rectTransform;
        rr.anchorMin = new Vector2(0.5f, 1f);
        rr.anchorMax = new Vector2(0.5f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(30f, 30f);
        rr.anchoredPosition = new Vector2(0f, 6f);

        // 檻
        GameObject cg = new GameObject("Cage", typeof(RectTransform));
        cg.transform.SetParent(root, false);
        RectTransform cage = (RectTransform)cg.transform;
        cage.anchorMin = new Vector2(0.5f, 1f);
        cage.anchorMax = new Vector2(0.5f, 1f);
        cage.pivot = new Vector2(0.5f, 1f);
        cage.sizeDelta = new Vector2(CageW, CageH);
        cage.anchoredPosition = new Vector2(0f, -ChainLength);

        Image back = NewImage("Back", cage, new Color(0.06f, 0.07f, 0.12f, 0.8f));
        Stretch(back.rectTransform);

        // 中のキー
        Image cap = NewImage("KeyCap", cage, new Color(0.96f, 0.96f, 0.98f, 1f));
        cap.sprite = BossSpriteUtil.Round();
        cap.type = Image.Type.Sliced;
        RectTransform cap_rt = cap.rectTransform;
        cap_rt.anchorMin = new Vector2(0.5f, 0.5f);
        cap_rt.anchorMax = new Vector2(0.5f, 0.5f);
        cap_rt.pivot = new Vector2(0.5f, 0.5f);
        cap_rt.sizeDelta = new Vector2(70f, 70f);
        cap_rt.anchoredPosition = Vector2.zero;
        AddOutline(cap.gameObject, 0.5f);
        it.keycap = cap_rt;

        TextMeshProUGUI letter = NewText("Letter", cap_rt, KeySeal.HudFont, 44f, new Color(0.1f, 0.1f, 0.16f, 1f));
        letter.text = key != null ? (string.IsNullOrEmpty(key.displayName) ? key.keyCode.ToString() : key.displayName) : "?";
        letter.alignment = TextAlignmentOptions.Center;
        letter.fontStyle = FontStyles.Bold;
        letter.enableAutoSizing = true;
        letter.fontSizeMin = 14f;
        letter.fontSizeMax = 44f;
        Stretch(letter.rectTransform);
        letter.rectTransform.offsetMin = new Vector2(4f, 4f);
        letter.rectTransform.offsetMax = new Vector2(-4f, -4f);

        // 檻の棒（縦）
        const int barCount = 6;
        it.bars = new Image[barCount];
        for (int j = 0; j < barCount; j++)
        {
            float f = (j + 0.5f) / barCount;
            Image bar = NewImage("Bar" + j, cage, Steel);
            RectTransform br = bar.rectTransform;
            br.anchorMin = new Vector2(f, 0f);
            br.anchorMax = new Vector2(f, 1f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(7f, 0f);
            br.anchoredPosition = Vector2.zero;
            AddOutline(bar.gameObject, 0.55f);
            it.bars[j] = bar;
        }

        // 上と下の枠
        Image top = NewImage("Top", cage, Steel);
        RectTransform tr = top.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 13f);
        tr.anchoredPosition = Vector2.zero;
        AddOutline(top.gameObject, 0.6f);

        Image bottom = NewImage("Bottom", cage, Steel);
        RectTransform bt = bottom.rectTransform;
        bt.anchorMin = new Vector2(0f, 0f);
        bt.anchorMax = new Vector2(1f, 0f);
        bt.pivot = new Vector2(0.5f, 0f);
        bt.sizeDelta = new Vector2(0f, 13f);
        bt.anchoredPosition = Vector2.zero;
        AddOutline(bottom.gameObject, 0.6f);

        // のこり時間のゲージ
        Image tback = NewImage("TimerBack", root, new Color(0f, 0f, 0f, 0.65f));
        RectTransform tbr = tback.rectTransform;
        tbr.anchorMin = new Vector2(0.5f, 1f);
        tbr.anchorMax = new Vector2(0.5f, 1f);
        tbr.pivot = new Vector2(0.5f, 1f);
        tbr.sizeDelta = new Vector2(CageW, 14f);
        tbr.anchoredPosition = new Vector2(0f, -(ChainLength + CageH + 6f));
        AddOutline(tback.gameObject, 0.7f);

        Image fill = NewImage("TimerFill", tbr, new Color(1f, 0.75f, 0.2f, 1f));
        RectTransform fr = fill.rectTransform;
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(1f, 1f);
        fr.offsetMin = new Vector2(2f, 2f);
        fr.offsetMax = new Vector2(-2f, -2f);
        it.fill = fill;

        return it;
    }

    // ================= 毎フレーム =================

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        int slot = 0;

        // キーコンフィグ・設定が開いている間は、画面の手前に出ないように隠す
        // （キーコンフィグの中には、タイルと行に、別の檻の絵が出る）
        if (_canvas != null)
            _canvas.enabled = !GameOverlayState.IsBlocking;

        UpdateNotices(dt);

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            Item it = _items[i];
            if (it.root == null)
            {
                _items.RemoveAt(i);
                continue;
            }
            it.age += dt;
        }

        // 並びの順番（前から）で、横の位置を決める
        for (int i = 0; i < _items.Count; i++)
        {
            Item it = _items[i];
            if (!it.closing)
            {
                float tx = BaseX + slot * Spacing;
                it.x = Mathf.Lerp(it.x, tx, 1f - Mathf.Exp(-10f * dt));
                slot++;
            }
        }

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            Item it = _items[i];

            // 落ちてくる → 鎖がのびて、はずむ
            float y;
            if (it.age < 0.4f)
            {
                float u = it.age / 0.4f;
                y = Mathf.Lerp(DropFrom, -22f, u * u);
            }
            else
            {
                float k = it.age - 0.4f;
                y = -22f * Mathf.Exp(-7f * k) * Mathf.Cos(26f * k);
            }

            // ゆらゆら（落ちた直後は大きく、だんだん小さく）
            float angle = 16f * Mathf.Exp(-1.6f * it.age) * Mathf.Sin(9f * it.age + it.phase)
                        + 2.5f * Mathf.Sin(1.7f * it.age + it.phase);

            if (!it.closing)
            {
                float remaining = KeySeal.Remaining(it.key);
                float total = Mathf.Max(0.01f, KeySeal.Total(it.key));
                float ratio = Mathf.Clamp01(remaining / total);

                if (it.fill != null)
                {
                    RectTransform fr = it.fill.rectTransform;
                    fr.anchorMax = new Vector2(Mathf.Max(0.001f, ratio), 1f);
                    Color c = new Color(1f, 0.75f, 0.2f, 1f);
                    if (remaining < 3f)
                    {
                        float blink = 0.5f + 0.5f * Mathf.Sin(it.age * 22f);
                        c = Color.Lerp(new Color(1f, 0.25f, 0.2f, 1f), new Color(1f, 0.7f, 0.6f, 1f), blink);
                    }
                    it.fill.color = c;
                }

                // のこりが少ないと、檻がガタガタ震える
                if (remaining < 3f)
                    angle += Mathf.Sin(it.age * 55f) * 3.5f;
            }
            else
            {
                it.closeT += dt;
                float u = Mathf.Clamp01(it.closeT / CloseSeconds);

                // 棒が左右に開いて、キーがはずむ。そのあと、鎖ごと上へ消える
                float spread = Mathf.Min(1f, u / 0.4f);
                if (it.bars != null)
                {
                    for (int j = 0; j < it.bars.Length; j++)
                    {
                        if (it.bars[j] == null)
                            continue;
                        float sign = (j - (it.bars.Length - 1) * 0.5f) / ((it.bars.Length - 1) * 0.5f);
                        it.bars[j].rectTransform.anchoredPosition = new Vector2(sign * 46f * spread, 0f);
                    }
                }
                if (it.keycap != null)
                    it.keycap.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(Mathf.Min(1f, u / 0.5f) * Mathf.PI));
                y = u * u * 200f;
                if (it.group != null)
                    it.group.alpha = 1f - Mathf.Clamp01((u - 0.45f) / 0.55f);
                if (it.fill != null)
                    it.fill.rectTransform.anchorMax = new Vector2(0.001f, 1f);

                if (it.closeT >= CloseSeconds)
                {
                    Destroy(it.root.gameObject);
                    _items.RemoveAt(i);
                    continue;
                }
            }

            it.root.anchoredPosition = new Vector2(it.x, y);
            it.root.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // ================= 部品 =================

    private static Image NewImage(string objName, Transform parent, Color color)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        Image im = g.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        return im;
    }

    private static TextMeshProUGUI NewText(string objName, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        if (font != null)
            t.font = font;
        t.fontSize = size;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AddOutline(GameObject g, float alpha)
    {
        Outline o = g.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, alpha);
        o.effectDistance = new Vector2(2f, -2f);
    }
}

/// <summary>
/// KEY ESCAPE - キーコンフィグ画面で、「封じられているキー」の上にかぶせる檻の絵。
///
///   ・暗くして、檻の棒を重ね、のこりの秒数を出す。
///   ・KeyGridButtonUI（左の一覧のキー）と ActionBindingRow（「現在の設定」のキー）が、必要なときに作る。
///   ・クリックを受けないので、下のボタンがそのまま反応する（反応を止めるのは、持ち主の側）。
/// </summary>
public class KeyCageGraphic : MonoBehaviour
{
    private KeyDefinition _key;
    private TextMeshProUGUI _time;

    /// <summary>parent いっぱいに広がる檻を、parent の子として作る。</summary>
    public static KeyCageGraphic Attach(RectTransform parent)
    {
        GameObject go = new GameObject("SealCage", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 親に Layout Group があっても、並べ直されないようにする
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        KeyCageGraphic g = go.AddComponent<KeyCageGraphic>();
        g.Build();
        return g;
    }

    /// <summary>どのキーの檻か（のこり時間の表示に使う）。</summary>
    public void Bind(KeyDefinition key)
    {
        _key = key;
        Refresh();
    }

    private void Build()
    {
        RectTransform self = (RectTransform)transform;

        Image dim = NewImage("Dim", self, new Color(0.04f, 0.05f, 0.1f, 0.62f));
        Stretch(dim.rectTransform);

        const int barCount = 5;
        for (int j = 0; j < barCount; j++)
        {
            float f = (j + 0.5f) / barCount;
            Image bar = NewImage("Bar" + j, self, new Color(0.82f, 0.84f, 0.9f, 1f));
            RectTransform br = bar.rectTransform;
            br.anchorMin = new Vector2(f, 0f);
            br.anchorMax = new Vector2(f, 1f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(4f, 0f);
            br.anchoredPosition = Vector2.zero;
            AddOutline(bar.gameObject);
        }

        Image top = NewImage("Top", self, new Color(0.82f, 0.84f, 0.9f, 1f));
        RectTransform tr = top.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 7f);
        tr.anchoredPosition = Vector2.zero;
        AddOutline(top.gameObject);

        Image bottom = NewImage("Bottom", self, new Color(0.82f, 0.84f, 0.9f, 1f));
        RectTransform bt = bottom.rectTransform;
        bt.anchorMin = new Vector2(0f, 0f);
        bt.anchorMax = new Vector2(1f, 0f);
        bt.pivot = new Vector2(0.5f, 0f);
        bt.sizeDelta = new Vector2(0f, 7f);
        bt.anchoredPosition = Vector2.zero;
        AddOutline(bottom.gameObject);

        GameObject tg = new GameObject("Seconds", typeof(RectTransform));
        tg.transform.SetParent(self, false);
        _time = tg.AddComponent<TextMeshProUGUI>();
        if (KeySeal.HudFont != null)
            _time.font = KeySeal.HudFont;
        _time.fontSize = 26f;
        _time.enableAutoSizing = true;
        _time.fontSizeMin = 10f;
        _time.fontSizeMax = 26f;
        _time.fontStyle = FontStyles.Bold;
        _time.color = new Color(1f, 0.85f, 0.35f, 1f);
        _time.alignment = TextAlignmentOptions.Center;
        _time.raycastTarget = false;
        Stretch(_time.rectTransform);
        _time.rectTransform.offsetMin = new Vector2(6f, 6f);
        _time.rectTransform.offsetMax = new Vector2(-6f, -6f);
        Outline o = tg.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(2f, -2f);
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_time == null)
            return;
        if (_key == null)
        {
            _time.text = "";
            return;
        }
        int s = Mathf.CeilToInt(KeySeal.Remaining(_key));
        _time.text = s > 0 ? s.ToString() : "";
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

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AddOutline(GameObject g)
    {
        Outline o = g.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.6f);
        o.effectDistance = new Vector2(1.5f, -1.5f);
    }
}
