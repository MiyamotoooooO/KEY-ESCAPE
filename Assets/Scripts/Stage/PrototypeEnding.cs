using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - 試作品の最後の演出。
///
/// 最後のステージ（ボス）の結果画面が終わったあとに、
///   暗い画面から、モニターのアップが出る → 画面がつく → 「試作品はここまでです。…」の文章が出る
///   → しばらくして暗くなる → タイトル画面へ戻る
/// という流れで流れる。StageGoal が、PrototypeEnding.Create(...).Begin() で始める。シーンには置かなくてよい。
///
/// モニターの絵は、Resources/Ending/MonitorFront（Assets/Resources/Ending/MonitorFront.png）から読む。
/// 無ければ、コードで描いた仮のモニターになる。
/// </summary>
public class PrototypeEnding : MonoBehaviour
{
    /// <summary>演出が流れている間 true（設定画面・キーコンフィグを開かないために使う）。</summary>
    public static bool IsPlaying { get; private set; }

    private const string MonitorResource = "Ending/MonitorFront";
    // 絵の中の「画面」の位置と大きさ（絵全体を 0〜1、左下が原点）。「モニター正面」の絵に合わせてある
    private static readonly Rect ScreenRect = new Rect(0.055f, 0.266f, 0.890f, 0.619f);

    private TMP_FontAsset _font;
    private Sprite _monitorSprite;
    private string _message;
    private float _hold;
    private string _titleScene;

    private Image _art;
    private Image _screen;
    private Image _black;
    private TextMeshProUGUI _text;
    private float _skipTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPlaying = false;
    }

    private void OnDestroy()
    {
        IsPlaying = false;
    }

    /// <summary>
    /// 演出の部品を作る（まだ流れない。Begin() で始める）。
    /// font=文章のフォント（空なら、シーン内の文字から借りる）、monitorSprite=モニターの絵（空なら、Resourcesから読む）、
    /// message=文章（改行は \n）、holdSeconds=文章を出しておく秒数、titleScene=戻るシーン名。
    /// </summary>
    public static PrototypeEnding Create(TMP_FontAsset font, Sprite monitorSprite, string message, float holdSeconds, string titleScene)
    {
        GameObject go = new GameObject("PrototypeEnding");
        PrototypeEnding e = go.AddComponent<PrototypeEnding>();
        e._font = font;
        e._monitorSprite = monitorSprite;
        e._message = string.IsNullOrEmpty(message) ? "試作品はここまでです。\n遊んでいただきありがとうございました。" : message;
        e._hold = Mathf.Max(1f, holdSeconds);
        e._titleScene = string.IsNullOrEmpty(titleScene) ? "TitleScene" : titleScene;
        e.Build();
        return e;
    }

    /// <summary>演出をはじめる。</summary>
    public void Begin()
    {
        if (IsPlaying)
            return;
        StartCoroutine(Run());
    }

    // ================= 画面を作る =================

    private static TMP_FontAsset FindFontInScene()
    {
        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font != null)
                return texts[i].font;
        }
        return null;
    }

    private static RectTransform NewRect(string objName, Transform parent)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        return g.GetComponent<RectTransform>();
    }

    private static Image NewImage(string objName, Transform parent, Color color)
    {
        RectTransform rt = NewRect(objName, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = transform;

        // 暗い部屋
        Image room = NewImage("Room", root, new Color(0.05f, 0.05f, 0.08f, 1f));
        Stretch(room.rectTransform);

        // モニターの絵
        Sprite sp = _monitorSprite;
        if (sp == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(MonitorResource);
            if (tex != null)
                sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        float aspect = 1.5f;
        if (sp != null && sp.rect.height > 0.01f)
            aspect = sp.rect.width / sp.rect.height;
        float height = 960f;
        float width = height * aspect;
        if (width > 1760f)
        {
            width = 1760f;
            height = width / aspect;
        }

        RectTransform mon = NewRect("Monitor", root);
        mon.anchorMin = mon.anchorMax = new Vector2(0.5f, 0.5f);
        mon.pivot = new Vector2(0.5f, 0.5f);
        mon.sizeDelta = new Vector2(width, height);
        mon.anchoredPosition = Vector2.zero;

        Color artOff = new Color(0.45f, 0.45f, 0.5f, 1f);
        _art = NewImage("Art", mon, artOff);
        Stretch(_art.rectTransform);
        if (sp != null)
        {
            _art.sprite = sp;
            _art.preserveAspect = false;
        }
        else
        {
            // 仮のモニター（黒いふち）
            _art.color = new Color(0.11f, 0.11f, 0.14f, 1f);
        }

        // 画面（ぱっとつく）
        _screen = NewImage("Screen", mon, new Color(0.94f, 0.96f, 0.99f, 0f));
        RectTransform sr = _screen.rectTransform;
        sr.anchorMin = new Vector2(ScreenRect.x, ScreenRect.y);
        sr.anchorMax = new Vector2(ScreenRect.x + ScreenRect.width, ScreenRect.y + ScreenRect.height);
        sr.offsetMin = Vector2.zero;
        sr.offsetMax = Vector2.zero;

        // 文章
        RectTransform tr = NewRect("Text", sr);
        Stretch(tr);
        tr.offsetMin = new Vector2(40f, 30f);
        tr.offsetMax = new Vector2(-40f, -30f);
        _text = tr.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = _font != null ? _font : FindFontInScene();
        if (f != null)
            _text.font = f;
        _text.text = _message;
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableWordWrapping = true;
        _text.enableAutoSizing = true;
        _text.fontSizeMin = 28f;
        _text.fontSizeMax = 64f;
        _text.lineSpacing = 24f;
        _text.color = new Color(0.14f, 0.14f, 0.2f, 0f);
        _text.raycastTarget = false;

        // 最後に、暗幕（いちばん手前）
        _black = NewImage("Black", root, Color.black);
        Stretch(_black.rectTransform);
    }

    // ================= 流れ =================

    private static IEnumerator Tween(float seconds, System.Action<float> apply)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            apply(Mathf.Clamp01(t / seconds));
            yield return null;
        }
        apply(1f);
    }

    private static float Smooth(float k)
    {
        return k * k * (3f - 2f * k);
    }

    private IEnumerator Run()
    {
        IsPlaying = true;
        Time.timeScale = 1f;

        // 暗いなかから、はじまる
        _black.color = Color.black;
        yield return Tween(0.7f, k => _black.color = new Color(0f, 0f, 0f, 1f - Smooth(k)));
        yield return new WaitForSecondsRealtime(0.7f);

        // モニターの画面が、ぱっとつく。文章が出る
        Color artOff = new Color(0.45f, 0.45f, 0.5f, 1f);
        Color screenOn = new Color(0.94f, 0.96f, 0.99f, 1f);
        Color textOn = new Color(0.14f, 0.14f, 0.2f, 1f);
        bool hasArt = _art.sprite != null;
        yield return Tween(0.6f, k =>
        {
            float e = Smooth(k);
            _screen.color = new Color(screenOn.r, screenOn.g, screenOn.b, e);
            if (hasArt)
                _art.color = Color.Lerp(artOff, Color.white, e);
            _text.color = new Color(textOn.r, textOn.g, textOn.b, e);
        });

        // しばらく見せる（少したってから、Space・Enter・クリックで先へ進める）
        float t = 0f;
        while (t < _hold)
        {
            t += Time.unscaledDeltaTime;
            if (t >= 1.5f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
                break;
            yield return null;
        }

        // 暗くなって、タイトルへ
        yield return Tween(0.8f, k => _black.color = new Color(0f, 0f, 0f, Smooth(k)));
        yield return new WaitForSecondsRealtime(0.3f);

        GoToTitle();
    }

    private void GoToTitle()
    {
        Time.timeScale = 1f;
        if (!Application.CanStreamedLevelBeLoaded(_titleScene))
        {
            Debug.LogWarning("[PrototypeEnding] タイトルのシーン「" + _titleScene + "」を読み込めません。Build Settings を確認してください。");
            return;
        }
        // キーの仕組みをリセットして、タイトルへ（設定画面の「タイトルへ戻る」と同じ）
        GameSession.ResetKeySystem();
        SceneManager.LoadScene(_titleScene);
    }
}
