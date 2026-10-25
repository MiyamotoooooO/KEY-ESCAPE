using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - 画面の左上（HPゲージの下）に、いま使える操作を出す「操作ガイド」。
///
/// 「[V] 左移動」「[K] 右移動」「[Y] ジャンプ」のように、キーの絵と操作の名前を、縦に並べる。
/// ・出すのは、解放済みで、キーが設定されている操作だけ。キーを付け替えたり、新しい操作が解放されたりすると、すぐ変わる。
/// ・設定画面の「操作ガイド」ON/OFF（GameSettings.ControlGuideVisible）に従う。OFFなら、出さない。
/// ・シーンやプレハブには何も置かなくてよい。ゲーム開始時に自分で現れて、シーンにプレイヤーがいる間だけ出る。
/// ・会話やギミック紹介でゲームが止まっている間、ロード中、やられている間は、ふわっと消える。
/// ・キーの絵は、キー（KeyDefinition）の Key Sprite。空なら、文字（Display Name）を四角で囲んで出す。
/// ・左上の「・〜へ行こう」のヒント（ObjectiveHintUI）は、このガイドの高さぶん、下へずれる（ObjectiveOffset）。
///
/// 見た目の数値は、すぐ下の「調整用」の定数を書き換える。
/// </summary>
public class ControlGuideUI : MonoBehaviour
{
    // ================= 調整用の数値（基準解像度 1920×1080 での値） =================

    const float MarginLeft = 30f;     // 画面の左端からの距離（HPゲージと同じ）
    const float Top = 158f;           // 画面の上端から、ガイドの上端までの距離（HPゲージの下）
    const float KeyHeight = 52f;      // キーの絵の高さ
    const float RowGap = 8f;          // 行と行のすきま
    const float KeyLabelGap = 14f;    // キーの絵と、操作の名前のすきま
    const float FontSize = 30f;       // 操作の名前の文字の大きさ
    const float ObjectiveGap = 18f;   // ガイドの下と、左上のヒントとのすきま（ヒントの初期の位置を基準にしたずれ）
    const float FadeSeconds = 0.3f;   // 出る・消えるときの秒数
    const float ShiftSpeed = 900f;    // 左上のヒントが、下へずれる速さ（1秒あたり）
    const int SortingOrder = -100;    // 描く順（HPゲージと同じ。設定画面・キーコンフィグなどの後ろに回す）

    static readonly Color TextColor = Color.white;
    static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.95f);
    const float OutlineWidth = 0.25f;

    // 順番：左移動、右移動、ジャンプ、攻撃、ダッシュ、遠距離攻撃（GameAction の並び）
    static readonly GameAction[] Order =
    {
        GameAction.MoveLeft, GameAction.MoveRight, GameAction.Jump,
        GameAction.Attack, GameAction.Dash, GameAction.RangedAttack,
    };

    // ==============================================================================

    private static ControlGuideUI _instance;

    /// <summary>左上の「・〜へ行こう」のヒントを、下へずらす量（基準解像度の値）。ガイドが出ていなければ 0。</summary>
    public static float ObjectiveOffset { get; private set; }

    private CanvasGroup _group;
    private RectTransform _list;
    private TMP_FontAsset _font;
    private CharacterController2D _player;
    private float _retryTimer;
    private float _refreshTimer;
    private string _signature = "";
    private int _rowCount;
    private float _contentHeight;
    private float _alpha;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        ObjectiveOffset = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
            return;
        GameObject go = new GameObject("ControlGuideUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ControlGuideUI>();
    }

    private void Awake()
    {
        BuildCanvas();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this)
        {
            _instance = null;
            ObjectiveOffset = 0f;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        // 新しいシーンのプレイヤーを探し直す
        _player = null;
        _retryTimer = 0f;
        _signature = "";
    }

    // ================= 画面の入れ物 =================

    private void BuildCanvas()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        GameObject box = new GameObject("Guide", typeof(RectTransform));
        box.transform.SetParent(transform, false);
        _list = (RectTransform)box.transform;
        _list.anchorMin = new Vector2(0f, 1f);
        _list.anchorMax = new Vector2(0f, 1f);
        _list.pivot = new Vector2(0f, 1f);
        _list.anchoredPosition = new Vector2(MarginLeft, -Top);
        _list.sizeDelta = Vector2.zero;

        VerticalLayoutGroup v = box.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperLeft;
        v.spacing = RowGap;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;

        ContentSizeFitter fitter = box.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // ================= 毎フレーム =================

    private void Update()
    {
        if (_player == null)
        {
            // プレイヤーがまだいない（読み込み中・タイトルなど）。少しずつ探す。
            _retryTimer -= Time.unscaledDeltaTime;
            if (_retryTimer <= 0f)
            {
                _retryTimer = 0.25f;
                _player = FindMainPlayer();
            }
        }

        // 操作とキーが変わっていないか、0.2秒ごとに見る（変わったら、作り直す）
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = 0.2f;
            Refresh();
        }

        bool want = _player != null
            && _rowCount > 0
            && GameSettings.ControlGuideVisible
            && !GamePause.IsPaused
            && !LoadingScreen.IsLoading
            && !HitFeedback.IsDying;

        float dt = Time.unscaledDeltaTime;
        _alpha = Mathf.MoveTowards(_alpha, want ? 1f : 0f, dt / Mathf.Max(0.01f, FadeSeconds));
        _group.alpha = _alpha;

        // 左上のヒントをずらす量：ガイドを出したい間（会話などで一瞬消えても、ずらしたまま）。
        bool occupying = _player != null && _rowCount > 0 && GameSettings.ControlGuideVisible;
        float targetOffset = occupying ? _contentHeight + ObjectiveGap : 0f;
        ObjectiveOffset = Mathf.MoveTowards(ObjectiveOffset, targetOffset, ShiftSpeed * dt);
    }

    private static CharacterController2D FindMainPlayer()
    {
        // Rigidbody2Dを持つものが、本物のプレイヤー（キーコンフィグのプレビュー用キャラなどは除く）。
        CharacterController2D[] all = FindObjectsOfType<CharacterController2D>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetComponent<Rigidbody2D>() != null)
                return all[i];
        }
        return null;
    }

    // ================= 行を作る =================

    private void Refresh()
    {
        KeyBindingManager mgr = KeyBindingManager.Instance;

        // 出す操作：解放済みで、キーが設定されているもの
        List<GameAction> actions = new List<GameAction>();
        List<KeyDefinition> keys = new List<KeyDefinition>();
        StringBuilder sig = new StringBuilder();
        if (mgr != null)
        {
            for (int i = 0; i < Order.Length; i++)
            {
                GameAction a = Order[i];
                if (!mgr.IsActionUnlocked(a))
                    continue;
                KeyDefinition k = mgr.GetBinding(a);
                if (k == null)
                    continue;
                actions.Add(a);
                keys.Add(k);
                sig.Append((int)a).Append(':').Append(k.GetInstanceID()).Append(';');
            }
        }

        string s = sig.ToString();
        if (s == _signature)
            return;
        _signature = s;

        for (int i = _list.childCount - 1; i >= 0; i--)
            Destroy(_list.GetChild(i).gameObject);

        _rowCount = actions.Count;
        _contentHeight = _rowCount > 0 ? _rowCount * KeyHeight + (_rowCount - 1) * RowGap : 0f;
        if (_rowCount == 0)
            return;

        if (_font == null)
            _font = ResolveFont("左右移動ジャンプ攻撃ダッシュ遠距離");

        for (int i = 0; i < actions.Count; i++)
            BuildRow(actions[i], keys[i]);
    }

    private void BuildRow(GameAction action, KeyDefinition key)
    {
        GameObject row = new GameObject("Row_" + action, typeof(RectTransform));
        row.transform.SetParent(_list, false);

        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.spacing = KeyLabelGap;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        LayoutElement rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = KeyHeight;
        rowLe.preferredHeight = KeyHeight;

        BuildKeyCap(row.transform, key);

        // 操作の名前
        GameObject lb = new GameObject("Label", typeof(RectTransform));
        lb.transform.SetParent(row.transform, false);
        TextMeshProUGUI tmp = lb.AddComponent<TextMeshProUGUI>();
        if (_font != null)
            tmp.font = _font;
        tmp.text = GameActionLabel.Get(action).Replace('　', ' ');
        tmp.fontSize = FontSize;
        tmp.color = TextColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        tmp.outlineWidth = OutlineWidth;
        tmp.outlineColor = OutlineColor;
    }

    /// <summary>キーの絵。KeyDefinition の Key Sprite があればそれ、なければ、文字を四角で囲んだもの。</summary>
    private void BuildKeyCap(Transform parent, KeyDefinition key)
    {
        if (key.keySprite != null)
        {
            Rect r = key.keySprite.rect;
            float w = KeyHeight * (r.width / Mathf.Max(1f, r.height));

            GameObject go = new GameObject("KeyCap", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = key.keySprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.minWidth = w;
            le.preferredHeight = KeyHeight;
            le.minHeight = KeyHeight;
            return;
        }

        // 絵がないとき：黒いふち＋白い面＋文字
        string label = string.IsNullOrEmpty(key.displayName) ? key.keyCode.ToString() : key.displayName;
        float width = Mathf.Max(KeyHeight, 24f * label.Length + 24f);

        GameObject outer = new GameObject("KeyCap", typeof(RectTransform));
        outer.transform.SetParent(parent, false);
        Image oi = outer.AddComponent<Image>();
        oi.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        oi.raycastTarget = false;
        LayoutElement ole = outer.AddComponent<LayoutElement>();
        ole.preferredWidth = width;
        ole.minWidth = width;
        ole.preferredHeight = KeyHeight;
        ole.minHeight = KeyHeight;

        GameObject inner = new GameObject("Face", typeof(RectTransform));
        inner.transform.SetParent(outer.transform, false);
        RectTransform irt = (RectTransform)inner.transform;
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(4f, 4f);
        irt.offsetMax = new Vector2(-4f, -4f);
        Image ii = inner.AddComponent<Image>();
        ii.color = new Color(0.96f, 0.96f, 0.94f, 1f);
        ii.raycastTarget = false;

        GameObject tx = new GameObject("Text", typeof(RectTransform));
        tx.transform.SetParent(inner.transform, false);
        RectTransform trt = (RectTransform)tx.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        TextMeshProUGUI t = tx.AddComponent<TextMeshProUGUI>();
        if (_font != null)
            t.font = _font;
        t.text = label;
        t.fontSize = 30f;
        t.enableAutoSizing = true;
        t.fontSizeMin = 12f;
        t.fontSizeMax = 30f;
        t.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
    }

    // ================= フォント =================

    /// <summary>シーンにある文字から、この文が表示できる（日本語が出る）フォントを探す。</summary>
    private static TMP_FontAsset ResolveFont(string text)
    {
        TMP_Text[] all = FindObjectsOfType<TMP_Text>(true);
        TMP_FontAsset best = null;
        int bestCount = -1;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].font == null)
                continue;
            TMP_FontAsset f = all[i].font;
            if (f == best)
                continue;
            int count = 0;
            for (int k = 0; k < text.Length; k++)
            {
                if (f.HasCharacter(text[k], true))
                    count++;
            }
            if (count == text.Length)
                return f;
            if (count > bestCount)
            {
                bestCount = count;
                best = f;
            }
        }
        return best;
    }
}
