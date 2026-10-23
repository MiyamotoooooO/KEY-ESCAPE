using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// キーコンフィグ画面の「キーを設定したときの演出」の設定。KeyConfigUIのInspectorで調整する。
/// </summary>
[System.Serializable]
public class KeyBindEffectSettings
{
    [Tooltip("OFFにすると、演出は出ない（効果音と、右下のプレビューだけ）。")]
    public bool enabled = true;

    [Header("火花（粒）")]
    public bool sparks = true;
    [Tooltip("粒の数。")]
    public int sparkCount = 16;
    [Tooltip("粒が散って消えるまでの長さ（秒）。")]
    public float sparkSeconds = 0.5f;
    [Tooltip("粒が飛び出す速さ（画面の単位／秒）。大きいほど遠くまで飛ぶ。")]
    public float sparkSpeed = 320f;
    [Tooltip("粒の大きさ。")]
    public float sparkSize = 14f;
    [Tooltip("粒が下に落ちる強さ。0なら、落ちずに広がるだけ。")]
    public float sparkGravity = 500f;
    [Tooltip("ONなら、キーの性格ごとに粒の色が変わる（まじめ＝金色、ぶきよう＝橙、非情＝紫、らんぼう＝赤、おくびょう＝水色、きまぐれ＝桃色）。OFFなら、下のSpark Colorの色。")]
    public bool tintByPersonality = true;
    public Color sparkColor = new Color(1f, 0.88f, 0.35f, 1f);

    [Header("キーキャップがはずむ")]
    public bool keycapPop = true;
    [Tooltip("はずむときに、一瞬、何倍まで大きくなるか。")]
    public float keycapPopScale = 1.25f;

    [Header("キーの一言")]
    public bool quip = true;
    [Tooltip("一言を出している長さ（秒）。")]
    public float quipSeconds = 1.3f;
    public int quipFontSize = 30;
    [Tooltip("一言を出す位置。キーキャップの中心からのずれ（右が＋X、上が＋Y）。")]
    public Vector2 quipOffset = new Vector2(0f, 90f);
    public Color quipTextColor = Color.white;
    public Color quipOutlineColor = Color.black;
    [Tooltip("文字の後ろの、黒い帯の色（透明度も）。")]
    public Color quipBackColor = new Color(0f, 0f, 0f, 0.65f);
    [Tooltip("空なら、アクションの名前の文字（左の「ジャンプ」など）と同じフォントを使う。")]
    public TMP_FontAsset quipFont;
}

/// <summary>
/// KEY ESCAPE - キーを設定した瞬間の演出（火花の粒・キーキャップがはずむ・キーの一言）。
/// KeyConfigUI.TryBindByDrag から KeyBindEffect.Play() で呼ばれる。ゲームが止まっていても動く（実時間で動かす）。
/// 画面のキャンバスの一番上に、一時的なオブジェクトを作って出し、終わったら消える。
/// </summary>
public class KeyBindEffect : MonoBehaviour
{
    private static KeyBindEffect s_current;

    private RectTransform _anchor;
    private Vector3 _anchorBaseScale = Vector3.one;
    private bool _popped;

    public static void Play(ActionBindingRow row, KeyDefinition key, GameAction action, KeyBindEffectSettings s)
    {
        if (s == null || !s.enabled || row == null || key == null)
            return;
        Canvas parent = row.GetComponentInParent<Canvas>();
        if (parent == null)
            return;
        parent = parent.rootCanvas;

        // 前の演出が残っていたら、すぐ片づける（キーキャップの大きさも元に戻る）。
        if (s_current != null)
        {
            s_current.RestoreScale();
            Destroy(s_current.gameObject);
            s_current = null;
        }

        RectTransform anchor = row.EffectAnchor;
        GameObject go = new GameObject("KeyBindEffect", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        Canvas c = go.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = parent.sortingOrder + 20;
        RectTransform root = (RectTransform)go.transform;
        root.sizeDelta = Vector2.zero;
        root.position = anchor.TransformPoint(anchor.rect.center);

        KeyBindEffect fx = go.AddComponent<KeyBindEffect>();
        s_current = fx;
        fx._anchor = anchor;
        fx._anchorBaseScale = anchor.localScale;

        float total = 0.1f;

        if (s.sparks && s.sparkCount > 0)
        {
            fx.StartCoroutine(fx.Sparks(root, anchor, key, s));
            total = Mathf.Max(total, s.sparkSeconds);
        }
        if (s.keycapPop)
        {
            fx.StartCoroutine(fx.Pop(s.keycapPopScale));
            total = Mathf.Max(total, 0.3f);
        }
        if (s.quip)
        {
            string line = PickLine(key, action);
            if (!string.IsNullOrEmpty(line))
            {
                TMP_FontAsset font = s.quipFont != null ? s.quipFont : (row.actionLabel != null ? row.actionLabel.font : null);
                fx.StartCoroutine(fx.Quip(root, line, font, s));
                total = Mathf.Max(total, s.quipSeconds);
            }
        }
        fx.StartCoroutine(fx.Life(total + 0.1f));
    }

    private IEnumerator Life(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        RestoreScale();
        if (s_current == this)
            s_current = null;
    }

    private void RestoreScale()
    {
        if (_popped && _anchor != null)
            _anchor.localScale = _anchorBaseScale;
        _popped = false;
    }

    // ---- 火花 ----------------------------------------------------------------

    private struct Spark
    {
        public RectTransform rt;
        public Image img;
        public Vector2 pos;
        public Vector2 vel;
        public float life;
        public Color color;
    }

    private IEnumerator Sparks(RectTransform root, RectTransform anchor, KeyDefinition key, KeyBindEffectSettings s)
    {
        Color baseColor = s.tintByPersonality ? PersonalityColor(key) : s.sparkColor;
        Vector2 half = anchor.rect.size * 0.5f * 0.7f;
        Spark[] list = new Spark[s.sparkCount];
        for (int i = 0; i < list.Length; i++)
        {
            GameObject g = new GameObject("Spark", typeof(RectTransform));
            g.transform.SetParent(root, false);
            Image img = g.AddComponent<Image>();
            img.raycastTarget = false;
            float size = s.sparkSize * Random.Range(0.5f, 1f);
            RectTransform rt = (RectTransform)g.transform;
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);   // ひし形
            Color col = Color.Lerp(baseColor, Color.white, Random.Range(0f, 0.6f));

            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.9f + 0.35f);   // 少し上向き
            list[i].rt = rt;
            list[i].img = img;
            list[i].pos = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            list[i].vel = dir.normalized * s.sparkSpeed * Random.Range(0.45f, 1f);
            list[i].life = s.sparkSeconds * Random.Range(0.7f, 1f);
            list[i].color = col;
            rt.anchoredPosition = list[i].pos;
            img.color = col;
        }

        float t = 0f;
        float end = Mathf.Max(0.05f, s.sparkSeconds);
        while (t < end)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].rt == null)
                    continue;
                float k = Mathf.Clamp01(t / list[i].life);
                list[i].vel.y -= s.sparkGravity * dt;
                list[i].pos += list[i].vel * dt;
                list[i].rt.anchoredPosition = list[i].pos;
                list[i].rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, k);
                Color col = list[i].color;
                col.a = 1f - k * k;
                list[i].img.color = col;
            }
            yield return null;
        }
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].rt != null)
                Destroy(list[i].rt.gameObject);
        }
    }

    private static Color PersonalityColor(KeyDefinition key)
    {
        if (key.isSpecialKey)
            return new Color(1f, 0.88f, 0.35f, 1f);
        switch (key.personality)
        {
            case KeyPersonality.Clumsy: return new Color(1f, 0.65f, 0.25f, 1f);     // 橙
            case KeyPersonality.Ruthless: return new Color(0.7f, 0.35f, 0.95f, 1f); // 紫
            case KeyPersonality.Rough: return new Color(1f, 0.3f, 0.25f, 1f);       // 赤
            case KeyPersonality.Cowardly: return new Color(0.4f, 0.85f, 1f, 1f);    // 水色
            case KeyPersonality.Fickle: return new Color(1f, 0.55f, 0.8f, 1f);      // 桃色
            default: return new Color(1f, 0.88f, 0.35f, 1f);                        // 金
        }
    }

    // ---- キーキャップがはずむ -------------------------------------------------

    private IEnumerator Pop(float peak)
    {
        if (_anchor == null)
            yield break;
        _popped = true;
        const float up = 0.08f;
        const float down = 0.17f;
        float t = 0f;
        while (t < up + down)
        {
            t += Time.unscaledDeltaTime;
            if (_anchor == null)
                yield break;
            float k = t < up ? t / up : 1f - (t - up) / down;
            _anchor.localScale = _anchorBaseScale * Mathf.Lerp(1f, peak, Mathf.Clamp01(k));
            yield return null;
        }
        RestoreScale();
    }

    // ---- キーの一言 ----------------------------------------------------------

    private IEnumerator Quip(RectTransform root, string line, TMP_FontAsset font, KeyBindEffectSettings s)
    {
        GameObject g = new GameObject("Quip", typeof(RectTransform));
        g.transform.SetParent(root, false);
        RectTransform rt = (RectTransform)g.transform;
        CanvasGroup group = g.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image back = g.AddComponent<Image>();
        back.color = s.quipBackColor;
        back.raycastTarget = false;

        GameObject tg = new GameObject("Text", typeof(RectTransform));
        tg.transform.SetParent(rt, false);
        TextMeshProUGUI tmp = tg.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = line.Replace('　', ' ');
        tmp.fontSize = s.quipFontSize;
        tmp.color = s.quipTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = s.quipOutlineColor;

        Vector2 pref = tmp.GetPreferredValues(tmp.text);
        RectTransform trt = (RectTransform)tg.transform;
        trt.sizeDelta = pref;
        trt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = pref + new Vector2(40f, 18f);
        Vector2 start = s.quipOffset;
        rt.anchoredPosition = start;

        float total = Mathf.Max(0.3f, s.quipSeconds);
        const float fadeIn = 0.1f;
        const float fadeOut = 0.3f;
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Min(t / fadeIn, (total - t) / fadeOut);
            group.alpha = Mathf.Clamp01(a);
            float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / total), 2f);
            rt.anchoredPosition = start + new Vector2(0f, 24f * rise);
            yield return null;
        }
        group.alpha = 0f;
    }

    // ---- セリフ --------------------------------------------------------------

    private static int s_lastIndex = -1;

    private static string PickLine(KeyDefinition key, GameAction action)
    {
        string[] pool = key.bindQuips;
        if (pool == null || pool.Length == 0)
            pool = DefaultLines(key, action);
        if (pool == null || pool.Length == 0)
            return "";
        int idx = Random.Range(0, pool.Length);
        if (pool.Length > 1 && idx == s_lastIndex)
            idx = (idx + 1) % pool.Length;
        s_lastIndex = idx;
        return pool[idx];
    }

    private static string[] DefaultLines(KeyDefinition key, GameAction action)
    {
        if (key.isSpecialKey)
            return new[] { "まかせてください！" };

        // 非情は、攻撃ではない場所に置かれると、力を出せない。
        if (key.personality == KeyPersonality.Ruthless && key.GetPersonalityEffect(action).noBenefitForThisAction)
            return new[] { "…ここでは、力を出せないな", "攻撃じゃないのか…" };

        switch (key.personality)
        {
            case KeyPersonality.Clumsy:
                return new[] { "が、がんばるよ…！", "たぶん、できる…！", "しっぱいしたら、ごめんね…" };
            case KeyPersonality.Ruthless:
                return new[] { "…やるべきことは、やる", "邪魔するものは、消す", "まかせろ" };
            case KeyPersonality.Rough:
                return new[] { "おう、やってやる！", "まかせとけ！", "ぶっとばすぜ！" };
            case KeyPersonality.Cowardly:
                return new[] { "ひ、ひぃ…でも、がんばる…", "こ、こわくないよ…！", "ち、近づかないでね…" };
            case KeyPersonality.Fickle:
                return new[] { "気が向いたらねー", "今日は調子いいかも！", "どうなるかなー？" };
            default:
                return new[] { "まかせてください！", "しっかりやります！", "よろしくお願いします！" };
        }
    }
}
