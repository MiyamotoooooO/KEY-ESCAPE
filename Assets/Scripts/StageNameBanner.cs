using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - ステージの名前を、画面の右上に出す部品。
///
/// 使い方：ステージのシーンに空のオブジェクト（名前は「StageName」など）を作り、このスクリプトを付ける。
/// Inspector の「Stage Name」に、表示したい文字（例：1-1　草原のはじまり）を書く。
/// 「Banner Sprite」に、背景の画像（Assets/Art/StageName/StageNameBanner.png）を入れる。
/// （空のときは、Resources/StageName/StageNameBanner を探す）
///
/// ステージが始まって少し待ってから、右上に横から出てきて、数秒後にまた横へ戻って消える。
/// 倒れてシーンが読み直された時も、同じように出る。
/// 拠点やタイトルなど、出したくないシーンには付けない。
/// 画面の部品は、このスクリプトが自分で作る（Canvasなどを用意しなくてよい）。
/// </summary>
public class StageNameBanner : MonoBehaviour
{
    [Header("表示する文字")]
    [Tooltip("ステージの名前。例：1-1　草原のはじまり")]
    public string stageName = "1-1　ステージの名前";

    [Header("見た目（どちらも任意）")]
    [Tooltip("背景の画像。空なら Resources/StageName/StageNameBanner を探す。")]
    public Sprite bannerSprite;
    [Tooltip("文字のフォント（TextMeshProのフォントアセット）。空なら、シーン内にある文字のフォントを使う。")]
    public TMP_FontAsset font;
    [Tooltip("文字の色。")]
    public Color textColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    [Header("動き")]
    [Tooltip("シーンが始まってから、出始めるまでの待ち時間（秒）。暗転が明けるのを待つため。")]
    public float startDelay = 0.7f;
    [Tooltip("出しておく時間（秒）。")]
    public float holdTime = 3f;
    [Tooltip("横から出る／戻る時間（秒）。")]
    public float slideTime = 0.5f;

    // ---- 画面上の置き場所（基準解像度 1920×1080 での大きさ） ----
    private const float BannerWidth = 560f;      // 画像全体の幅（枠の外の余白を含む）
    private const float MarginRight = -17f;      // 右端からの位置（画像の余白の分、枠が画面の端から少し離れる）
    private const float MarginTop = 13f;         // 上端からの位置（上の余白の分、少し上へずらす）
    private const int SortingOrder = -90;        // 描く順。HPゲージ（-100）より手前、設定画面などより後ろ

    // 枠の中（文字を入れる範囲）。画像全体に対する割合。
    private const float TextLeft = 0.08f;
    private const float TextRight = 0.92f;
    private const float TextBottom = 0.28f;
    private const float TextTop = 0.78f;

    private const float FontMax = 46f;
    private const float FontMin = 18f;
    private const float MaxDt = 0.05f;

    private GameObject _canvasObj;
    private RectTransform _banner;

    private void Start()
    {
        Sprite sprite = bannerSprite != null
            ? bannerSprite
            : Resources.Load<Sprite>("StageName/StageNameBanner");
        if (sprite == null)
        {
            Debug.LogWarning("[StageNameBanner] 背景の画像がありません。Banner Sprite に StageNameBanner を入れてください。", this);
            return;
        }
        if (string.IsNullOrEmpty(stageName))
            return;

        Build(sprite);
        StartCoroutine(Play());
    }

    private void OnDestroy()
    {
        if (_canvasObj != null)
            Destroy(_canvasObj);
    }

    // ================= 動き =================

    private IEnumerator Play()
    {
        float visibleX = MarginRight;
        float hiddenX = MarginRight + BannerWidth + 60f; // 画面の右の外
        SetX(hiddenX);
        _canvasObj.SetActive(false);

        yield return WaitUnscaled(startDelay);

        _canvasObj.SetActive(true);
        yield return Slide(hiddenX, visibleX);
        yield return WaitUnscaled(holdTime);
        yield return Slide(visibleX, hiddenX);

        Destroy(_canvasObj);
        _canvasObj = null;
    }

    private IEnumerator Slide(float from, float to)
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, slideTime);
        while (t < dur)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxDt);
            float k = Mathf.Clamp01(t / dur);
            float eased = 1f - Mathf.Pow(1f - k, 3f); // はじめは速く、終わりはゆっくり
            SetX(Mathf.Lerp(from, to, eased));
            yield return null;
        }
        SetX(to);
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxDt);
            yield return null;
        }
    }

    private void SetX(float x)
    {
        if (_banner != null)
            _banner.anchoredPosition = new Vector2(x, MarginTop);
    }

    // ================= 画面の部品を作る =================

    private void Build(Sprite sprite)
    {
        _canvasObj = new GameObject("StageNameBannerCanvas");
        _canvasObj.transform.SetParent(transform, false);
        Canvas canvas = _canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = _canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 背景の画像（右上に置く）
        float aspect = sprite.rect.height / sprite.rect.width;
        GameObject bannerObj = new GameObject("Banner", typeof(RectTransform));
        bannerObj.transform.SetParent(_canvasObj.transform, false);
        _banner = (RectTransform)bannerObj.transform;
        _banner.anchorMin = new Vector2(1f, 1f);
        _banner.anchorMax = new Vector2(1f, 1f);
        _banner.pivot = new Vector2(1f, 1f);
        _banner.sizeDelta = new Vector2(BannerWidth, BannerWidth * aspect);

        Image img = bannerObj.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;

        // 文字（枠の中に収める）
        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(bannerObj.transform, false);
        RectTransform tr = (RectTransform)textObj.transform;
        tr.anchorMin = new Vector2(TextLeft, TextBottom);
        tr.anchorMax = new Vector2(TextRight, TextTop);
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset useFont = font != null ? font : FindSceneFont();
        if (useFont != null)
            text.font = useFont;
        text.text = stageName;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = FontMin;
        text.fontSizeMax = FontMax;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    /// <summary>シーン内の文字から、フォントを借りる（日本語が出るフォントを、自分で入れなくて済むように）。</summary>
    private static TMP_FontAsset FindSceneFont()
    {
        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font != null)
                return texts[i].font;
        }
        return null;
    }
}
