using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - 画面の左上に出す、文字だけのヒント（「・掲示板に向かえ」など）の見た目の設定。
/// GimmickIntroZone の Inspector（左上のヒント）に並ぶ。
/// </summary>
[System.Serializable]
public class ObjectiveHintStyle
{
    [Tooltip("文字の大きさ。")]
    public float fontSize = 34f;
    public Color textColor = Color.white;
    [Tooltip("ふちどりの色。")]
    public Color outlineColor = new Color(0f, 0f, 0f, 0.95f);
    [Tooltip("ふちどりの太さ（0でふちどりなし）。")]
    [Range(0f, 0.5f)]
    public float outlineWidth = 0.25f;
    [Tooltip("文の前につける記号。空なら、つけない。")]
    public string bullet = "・";
    [Tooltip("画面の左上から、右・下へのずれ（画面の大きさを 1920x1080 としたときの値）。左上に、HPゲージがあるので、少し下にしてある。")]
    public Vector2 offset = new Vector2(48f, 150f);
    [Tooltip("出る・消えるときの、ふわっとする秒数。")]
    public float fadeSeconds = 0.4f;
    [Tooltip("ヒントが何個か出ているときの、行と行の間のすきま。")]
    public float rowGap = 10f;
}

/// <summary>
/// KEY ESCAPE - 画面の左上に、文字だけのヒントを出す・消す部品（GimmickIntroZone が使う）。
/// 自分でシーンに置かなくていい。最初に使われたときに、自分で現れる。
/// ・ゲームを止めていても動く（実時間）。クリックの邪魔はしない。
/// ・キーコンフィグ画面や設定画面など、ほかの画面の後ろに出る（そういう画面が開いている間は、隠れる）。
/// </summary>
public class ObjectiveHintUI : MonoBehaviour
{
    private class Entry
    {
        public string id;
        public GameObject go;
        public CanvasGroup group;
        public float alpha;
        public float target;
        public float fade = 0.4f;
        public bool removing;
    }

    private static ObjectiveHintUI s_inst;
    private static TMP_FontAsset s_font;

    private readonly List<Entry> _entries = new List<Entry>();
    private RectTransform _container;
    private VerticalLayoutGroup _layout;
    private Vector2 _baseOffset = new Vector2(48f, 150f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_inst = null;
        s_font = null;
    }

    /// <summary>このIDのヒントが、いま出ている（消えかけは数えない）か。</summary>
    public static bool IsShowing(string id)
    {
        if (s_inst == null)
            return false;
        Entry e = s_inst.Find(id);
        return e != null && !e.removing;
    }

    /// <summary>ヒントを出す。同じIDがすでに出ていれば、文字だけ入れかえる。font が空なら、シーンにある日本語のフォントを探して使う。</summary>
    public static void Show(string id, string text, ObjectiveHintStyle style, TMP_FontAsset font)
    {
        if (string.IsNullOrEmpty(id))
            return;
        if (style == null)
            style = new ObjectiveHintStyle();
        ObjectiveHintUI ui = Get();
        ui.ShowInternal(id, text, style, font);
    }

    /// <summary>ヒントを消す（ふわっと消える）。出ていなければ、何もしない。</summary>
    public static void Hide(string id)
    {
        if (s_inst == null)
            return;
        Entry e = s_inst.Find(id);
        if (e != null)
        {
            e.removing = true;
            e.target = 0f;
        }
    }

    private static ObjectiveHintUI Get()
    {
        if (s_inst == null)
        {
            GameObject go = new GameObject("ObjectiveHintUI");
            s_inst = go.AddComponent<ObjectiveHintUI>();
            s_inst.Build();
        }
        return s_inst;
    }

    private Entry Find(string id)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].id == id && !_entries[i].removing)
                return _entries[i];
        }
        return null;
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;   // ほかの画面（キーコンフィグ・設定など）の後ろ

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject box = new GameObject("Hints", typeof(RectTransform));
        box.transform.SetParent(transform, false);
        _container = box.GetComponent<RectTransform>();
        _container.anchorMin = new Vector2(0f, 1f);
        _container.anchorMax = new Vector2(0f, 1f);
        _container.pivot = new Vector2(0f, 1f);
        _container.sizeDelta = Vector2.zero;

        _layout = box.AddComponent<VerticalLayoutGroup>();
        _layout.childAlignment = TextAnchor.UpperLeft;
        _layout.childControlWidth = true;
        _layout.childControlHeight = true;
        _layout.childForceExpandWidth = false;
        _layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = box.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void ShowInternal(string id, string text, ObjectiveHintStyle style, TMP_FontAsset font)
    {
        string body = (style.bullet ?? "") + (text ?? "").Replace('　', ' ');

        _baseOffset = style.offset;
        _container.anchoredPosition = new Vector2(_baseOffset.x, -(_baseOffset.y + ControlGuideUI.ObjectiveOffset));
        _layout.spacing = style.rowGap;

        Entry e = Find(id);
        if (e == null)
        {
            GameObject go = new GameObject("Hint_" + id, typeof(RectTransform));
            go.transform.SetParent(_container, false);
            e = new Entry();
            e.id = id;
            e.go = go;
            e.group = go.AddComponent<CanvasGroup>();
            e.group.alpha = 0f;
            e.group.interactable = false;
            e.group.blocksRaycasts = false;
            go.AddComponent<TextMeshProUGUI>();
            _entries.Add(e);
        }

        TextMeshProUGUI tmp = e.go.GetComponent<TextMeshProUGUI>();
        tmp.font = font != null ? font : ResolveFont(body);
        tmp.text = body;
        tmp.fontSize = style.fontSize;
        tmp.color = style.textColor;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        tmp.outlineWidth = style.outlineWidth;
        tmp.outlineColor = style.outlineColor;

        e.fade = Mathf.Max(0.01f, style.fadeSeconds);
        e.target = 1f;
        e.removing = false;
    }

    private void Update()
    {
        // 左上に操作ガイドが出ているときは、そのぶん下へずらす
        if (_container != null)
            _container.anchoredPosition = new Vector2(_baseOffset.x, -(_baseOffset.y + ControlGuideUI.ObjectiveOffset));

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry e = _entries[i];
            e.alpha = Mathf.MoveTowards(e.alpha, e.target, Time.unscaledDeltaTime / e.fade);
            if (e.group != null)
                e.group.alpha = e.alpha;
            if (e.removing && e.alpha <= 0f)
            {
                if (e.go != null)
                    Destroy(e.go);
                _entries.RemoveAt(i);
            }
        }
    }

    // ---- フォント ------------------------------------------------------------

    /// <summary>シーンにある文字から、この文が表示できる（日本語が出る）フォントを探す。</summary>
    private static TMP_FontAsset ResolveFont(string text)
    {
        char probe = '\0';
        if (!string.IsNullOrEmpty(text))
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= '⺀')
                {
                    probe = text[i];
                    break;
                }
            }
        }

        if (s_font != null && (probe == '\0' || s_font.HasCharacter(probe, true)))
            return s_font;

        TMP_Text[] all = FindObjectsOfType<TMP_Text>(true);
        TMP_FontAsset fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].font == null)
                continue;
            TMP_FontAsset f = all[i].font;
            if (fallback == null)
                fallback = f;
            if (probe == '\0' || f.HasCharacter(probe, true))
            {
                s_font = f;
                return f;
            }
        }
        return fallback;
    }
}
