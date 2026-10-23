using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - ロード画面。シーンを切り替える前に、暗くして、ヒントを出す。
///
/// 使い方（SceneManager.LoadScene の代わりに呼ぶ）:
///   LoadingScreen.Load("シーン名");   // 別のシーンへ（出発・拠点へ戻る・タイトルから拠点へ）
///   LoadingScreen.Reload();            // 今のシーンをやり直す（やられたとき）
///
/// 流れ:
///   1. ゲームを止めて、画面を暗くしていく。
///   2. 次のシーンを裏で読み込みながら、ヒントを1つ出す。
///   3. 自動で2〜3秒たつか、Spaceを押すと（読み込みが終わっていれば）次のシーンに入る。
///   4. 入ったあとは、ロード画面が消えていく。
/// 文章・画像・時間は、Resources の LoadingScreenSettings アセットで変える。
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    private const string SettingsName = "LoadingScreenSettings";
    private const int SortingOrder = 6000;   // 被弾の演出(3000)・オープニング(5000)よりも手前

    private static bool s_loading;
    private static int s_lastHint = -1;
    private static LoadingScreenSettings s_defaults;

    public static bool IsLoading { get { return s_loading; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_loading = false;
        s_lastHint = -1;
        s_defaults = null;
    }

    /// <summary>別のシーンへ。ロード画面を出す設定がOFFなら、すぐに切り替える。</summary>
    public static void Load(string sceneName)
    {
        if (s_loading)
            return;
        LoadingScreenSettings st = GetSettings();
        if (!st.enabled)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }
        Begin(st, sceneName, -1);
    }

    /// <summary>今のシーンをやり直す（やられたとき）。「やり直しにも出す」がOFFなら、ロード画面なしで読み直す。</summary>
    public static void Reload()
    {
        if (s_loading)
            return;
        int index = SceneManager.GetActiveScene().buildIndex;
        LoadingScreenSettings st = GetSettings();
        if (!st.enabled || !st.showOnRetry)
        {
            SceneManager.LoadSceneAsync(index);
            return;
        }
        Begin(st, null, index);
    }

    private static LoadingScreenSettings GetSettings()
    {
        LoadingScreenSettings st = Resources.Load<LoadingScreenSettings>(SettingsName);
        if (st != null)
            return st;
        // アセットがない時は、初期値で動く。
        if (s_defaults == null)
            s_defaults = ScriptableObject.CreateInstance<LoadingScreenSettings>();
        return s_defaults;
    }

    // ---- 画面を作る -----------------------------------------------------------

    private LoadingScreenSettings _st;
    private CanvasGroup _group;
    private TextMeshProUGUI _hintText;
    private TextMeshProUGUI _loadingText;
    private TextMeshProUGUI _prompt;
    private Image[] _spinner;

    private static void Begin(LoadingScreenSettings st, string sceneName, int index)
    {
        s_loading = true;

        GameObject go = new GameObject("LoadingScreen", typeof(RectTransform));
        DontDestroyOnLoad(go);
        LoadingScreen ls = go.AddComponent<LoadingScreen>();
        ls._st = st;
        ls.Build(go);
        ls.StartCoroutine(ls.Run(sceneName, index));
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        return (RectTransform)g.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, int size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    private void Build(GameObject go)
    {
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();   // 後ろの画面のボタンを、押せなくする
        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        // 背景（単色）。クリックを受け止める。
        RectTransform baseRt = NewRect("Base", go.transform);
        Stretch(baseRt);
        Image baseImg = baseRt.gameObject.AddComponent<Image>();
        baseImg.color = _st.baseColor;
        baseImg.raycastTarget = true;

        // 背景（画像）＋黒い膜
        if (_st.background != null)
        {
            RectTransform bgRt = NewRect("Background", go.transform);
            Stretch(bgRt);
            Image bg = bgRt.gameObject.AddComponent<Image>();
            bg.sprite = _st.background;
            bg.color = _st.backgroundTint;
            bg.raycastTarget = false;

            RectTransform dimRt = NewRect("Dim", go.transform);
            Stretch(dimRt);
            Image dim = dimRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, _st.dimAlpha);
            dim.raycastTarget = false;
        }

        // ヒントの文を選ぶ
        string hint = PickHint();
        TMP_FontAsset font = _st.font != null ? _st.font : ResolveFont(hint + _st.hintLabel + _st.skipLabel);

        // ヒントの見出し・線・文
        TextMeshProUGUI label = NewText("HintLabel", go.transform, font, _st.labelFontSize, _st.labelColor, TextAlignmentOptions.Center);
        label.text = _st.hintLabel;
        SetCenter(label.rectTransform, new Vector2(600f, 50f), new Vector2(0f, 110f));

        RectTransform line = NewRect("HintLine", go.transform);
        Image lineImg = line.gameObject.AddComponent<Image>();
        lineImg.color = new Color(_st.labelColor.r, _st.labelColor.g, _st.labelColor.b, 0.8f);
        lineImg.raycastTarget = false;
        SetCenter(line, new Vector2(220f, 3f), new Vector2(0f, 76f));

        _hintText = NewText("HintText", go.transform, font, _st.hintFontSize, _st.hintColor, TextAlignmentOptions.Top);
        _hintText.enableWordWrapping = true;
        _hintText.text = hint;
        SetCenter(_hintText.rectTransform, new Vector2(1400f, 220f), new Vector2(0f, -50f));

        // 右下：ぐるぐる＋「NOW LOADING」
        RectTransform spin = NewRect("Spinner", go.transform);
        spin.anchorMin = spin.anchorMax = new Vector2(1f, 0f);
        spin.pivot = new Vector2(0.5f, 0.5f);
        spin.sizeDelta = new Vector2(80f, 80f);
        spin.anchoredPosition = new Vector2(-100f, 100f);
        _spinner = new Image[8];
        for (int i = 0; i < _spinner.Length; i++)
        {
            RectTransform d = NewRect("Dot" + i, spin);
            d.sizeDelta = new Vector2(12f, 12f);
            float ang = i * Mathf.PI * 2f / _spinner.Length;
            d.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 30f;
            d.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image im = d.gameObject.AddComponent<Image>();
            im.color = Color.white;
            im.raycastTarget = false;
            _spinner[i] = im;
        }

        _loadingText = NewText("LoadingText", go.transform, font, _st.smallFontSize, Color.white, TextAlignmentOptions.Right);
        _loadingText.text = _st.loadingLabel;
        RectTransform lr = _loadingText.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(1f, 0f);
        lr.pivot = new Vector2(1f, 0.5f);
        lr.sizeDelta = new Vector2(500f, 50f);
        lr.anchoredPosition = new Vector2(-160f, 100f);

        // 下の真ん中：Spaceで進める合図
        _prompt = NewText("SkipPrompt", go.transform, font, _st.smallFontSize + 4, Color.white, TextAlignmentOptions.Center);
        _prompt.text = _st.skipLabel;
        RectTransform pr = _prompt.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(800f, 60f);
        pr.anchoredPosition = new Vector2(0f, 100f);
        _prompt.gameObject.SetActive(false);
    }

    private static void SetCenter(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    private string PickHint()
    {
        string[] hints = _st.hints;
        if (hints == null || hints.Length == 0)
            return "";
        int idx = Random.Range(0, hints.Length);
        if (hints.Length > 1 && idx == s_lastHint)
            idx = (idx + 1) % hints.Length;
        s_lastHint = idx;
        return (hints[idx] ?? "").Replace('　', ' ');
    }

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
                return f;
        }
        return fallback;
    }

    // ---- 流れ -----------------------------------------------------------------

    private IEnumerator Run(string sceneName, int index)
    {
        GamePause.Acquire(this);   // 暗くなっていく間に、キャラが動かないように

        // 1. 暗くなる
        float t = 0f;
        float fadeIn = Mathf.Max(0.01f, _st.fadeInSeconds);
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }
        _group.alpha = 1f;

        // 2. 次のシーンを、裏で読み込む（入るのは、まだ）
        AsyncOperation op = sceneName != null ? SceneManager.LoadSceneAsync(sceneName) : SceneManager.LoadSceneAsync(index);
        if (op == null)
        {
            Debug.LogWarning("[LoadingScreen] シーンを読み込めませんでした。File > Build Settings に、シーンが入っているか確認してください。");
            GamePause.Release(this);
            s_loading = false;
            Destroy(gameObject);
            yield break;
        }
        op.allowSceneActivation = false;

        float auto = Mathf.Clamp(_st.autoMinSeconds + _hintText.text.Length * _st.secondsPerChar,
                                 _st.autoMinSeconds, Mathf.Max(_st.autoMinSeconds, _st.autoMaxSeconds));
        float shown = 0f;
        bool pressed = false;
        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            shown += dt;
            Animate(shown);

            bool canSkip = shown >= _st.spaceEnableSeconds;
            if (canSkip && !_prompt.gameObject.activeSelf)
                _prompt.gameObject.SetActive(true);
            if (canSkip)
                _prompt.alpha = 0.55f + 0.45f * Mathf.Sin(shown * 5f);
            if (canSkip && Input.GetKeyDown(KeyCode.Space))
                pressed = true;

            bool loaded = op.progress >= 0.9f;
            if (loaded && (pressed || shown >= auto))
                break;
            yield return null;
        }

        // 3. 次のシーンに入る
        op.allowSceneActivation = true;
        while (!op.isDone)
        {
            Animate(shown += Time.unscaledDeltaTime);
            yield return null;
        }
        yield return null;
        yield return null;
        GamePause.Release(this);   // 入ったときに、止めた状態は自動で解除されている（念のため）

        // 4. ロード画面が消えていく
        t = 0f;
        float fadeOut = Mathf.Max(0.01f, _st.fadeOutSeconds);
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            yield return null;
        }
        s_loading = false;
        Destroy(gameObject);
    }

    /// <summary>ぐるぐる回る点と、「NOW LOADING...」の点の数。</summary>
    private void Animate(float time)
    {
        for (int i = 0; i < _spinner.Length; i++)
        {
            float phase = Mathf.Repeat(time * 1.4f - (float)i / _spinner.Length, 1f);
            Color c = _spinner[i].color;
            c.a = Mathf.Lerp(1f, 0.15f, phase);
            _spinner[i].color = c;
        }
        int dots = (int)(time * 2.5f) % 4;
        _loadingText.text = _st.loadingLabel + new string('.', dots);
    }

    private void OnDestroy()
    {
        // 途中で壊されても、止めたままにならないように
        GamePause.Release(this);
    }
}
