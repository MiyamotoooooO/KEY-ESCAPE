using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - マップ風のステージ選択画面。拠点で、掲示板（馬、バッグなど）に話しかけると開く（StageSelectNPCから）。
///
/// 流れ：
///   1. マップ上のステージのボタン（StageNodeButton）を押すと、そのステージが選ばれ、
///      確認の画面（「このステージに挑戦しますか？」／はい／もどる）が出る。
///      ロック中（未解放）のステージを押すと、短いメッセージが出る。
///   2. 「はい」を押すと、そのステージのシーンへ移る。
///      「もどる」を押す（またはEscキー）と、確認の画面を閉じて、ステージ選択の画面に戻る。
/// 開いている間は、ゲームを止める（Time.timeScale = 0）。
///
/// カード式（ページめくり）の表示：
///   「Card Template」を割り当てると、StageCatalogのステージの数だけカードを自動で作って並べる。
///   1ページに並べる数は「Cards Per Page」。矢印ボタンか、←→キーでページをめくる。
///   カードは、マウスを乗せるか、←→キーで選ぶ。クリックか、Enterキーで決定する（Zキーでは決定しない）。
///   「戻る」はEscキー（SettingsUIが CloseIfOpen を呼ぶ）。
///   「Card Template」が空なら、これまでの、マップ上のボタン（StageNodeButton）を使う表示になる。
///
/// 注意：このスクリプトは、panelRootの外（常に有効なオブジェクト）に付けること。
/// （SettingsUIと同じ理由：panelRootに付けると、無効の間はUpdateなどが動かない。）
/// 閉じるには、「もどる」ボタンか、Escキー（Escの処理はSettingsUIが行い、CloseIfOpenを呼ぶ）。
/// 確認の画面が出ている間のEscは、確認の画面だけを閉じる。
/// </summary>
public class StageSelectUI : MonoBehaviour
{
    /// <summary>ステージ選択画面が開いているか。他の画面が、Esc/Tabの扱いを決めるのに使う。</summary>
    public static bool IsOpen { get; private set; }

    private static StageSelectUI _instance;

    private static bool _backLocked;

    /// <summary>
    /// ONの間は、ステージ選択画面を閉じられない（戻るボタンも、Escキーも効かない）。ステージを選んで出発するまで、確実に挑戦させたいときに使う。
    /// 確認の画面（「挑戦しますか？」）の「いいえ」やEscは、これまでどおり効く。出発するとき、画面が破棄されるときは、自動でOFFに戻る。
    /// </summary>
    public static bool BackLocked
    {
        get { return _backLocked; }
        set
        {
            _backLocked = value;
            if (_instance != null)
                _instance.ApplyBackButton();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _backLocked = false;
    }

    /// <summary>戻れないときは、「戻る」ボタンも隠す。</summary>
    private void ApplyBackButton()
    {
        if (backButton != null)
            backButton.gameObject.SetActive(!_backLocked);
    }

    /// <summary>
    /// 「戻る」操作（Escキー）で呼ばれる。確認の画面が出ていれば、それだけを閉じる。
    /// 出ていなければ、ステージ選択の画面を閉じる。
    /// </summary>
    public static void CloseIfOpen()
    {
        if (IsOpen && _instance != null)
            _instance.GoBack();
    }

    [Header("画面")]
    [Tooltip("ステージ選択画面全体（暗い背景、マップ、ステージのボタン、情報欄、ボタン、確認の画面）。")]
    public GameObject panelRoot;
    [Tooltip("任意。開いた時の演出（看板が落ちてくる）。panelRootに付けたKeyConfigIntroAnimatorを割り当てる。")]
    public KeyConfigIntroAnimator introAnimator;
    public bool pauseGameWhileOpen = true;

    [Header("情報欄")]
    public TMP_Text stageNameText;
    public TMP_Text stageStateText;
    [Tooltip("任意。ステージの説明。")]
    public TMP_Text stageDescriptionText;
    [Tooltip("任意。ベストタイム（例：「ベストタイム 01:23.45」）。空なら、状態の文字の横に出す。")]
    public TMP_Text stageBestTimeText;

    [Header("ボタン")]
    public Button backButton;

    [Header("確認の画面（「このステージに挑戦しますか？」）")]
    [Tooltip("確認の画面全体。StageSelectPanelの中の、一番最後（一番手前）の子にする。空なら、確認なしですぐに出発する。")]
    public GameObject confirmPanel;
    [Tooltip("「はい」ボタン。")]
    public Button confirmYesButton;
    [Tooltip("「もどる」ボタン。")]
    public Button confirmNoButton;
    [Tooltip("任意。確認の画面に、選んだステージの名前を表示するテキスト。")]
    public TMP_Text confirmStageNameText;

    [Header("ロック中のステージを押した時のメッセージ")]
    [Tooltip("任意。ロック中のステージを押した時に出す表示（吹き出し、帯など）。Raycast Targetはオフにしておく。空なら、何も出さない。")]
    public GameObject lockedMessage;
    [Tooltip("任意。メッセージのテキスト。割り当てると「○○をクリアすると挑戦できます」と自動で書き換える。空なら、lockedMessageに作った文字や絵をそのまま出す。")]
    public TMP_Text lockedMessageText;
    [Tooltip("メッセージを表示しておく秒数。")]
    public float lockedMessageSeconds = 2f;

    [Header("カード式（ページめくり）の表示 ― 使わないなら、ここはすべて空のままにする")]
    [Tooltip("カード1枚分の見本（StageNodeButton）。割り当てると、StageCatalogのステージの数だけカードを自動で作って並べる。見本自身は、自動で非表示になる。")]
    public StageNodeButton cardTemplate;
    [Tooltip("カードを並べる入れ物（HorizontalLayoutGroupを付けておく）。空なら、見本と同じ親に並べる。")]
    public RectTransform cardContainer;
    [Tooltip("1ページに並べるカードの数。")]
    public int cardsPerPage = 4;
    [Tooltip("任意。選んでいるカードの上に出す印（▽）。ピボットは「下の真ん中」にして、カードを並べる入れ物の外に置く。")]
    public RectTransform cursor;
    [Tooltip("カードの上端から、印を浮かせる距離（基準解像度でのピクセル）。")]
    public float cursorOffsetY = 12f;
    [Tooltip("任意。前のページへ戻る矢印ボタン（左の矢印）。")]
    public Button previousPageButton;
    [Tooltip("任意。次のページへ進む矢印ボタン（右の矢印）。")]
    public Button nextPageButton;
    [Tooltip("任意。ページの点を並べる入れ物（HorizontalLayoutGroupを付けておく）。")]
    public RectTransform dotContainer;
    [Tooltip("点1つ分の見本（Image）。見本自身は、自動で非表示になる。")]
    public Image dotTemplate;
    [Tooltip("いまのページの点の絵。")]
    public Sprite dotOnSprite;
    [Tooltip("ほかのページの点の絵。")]
    public Sprite dotOffSprite;
    [Tooltip("ページが1つだけの時は、矢印と点を隠す。")]
    public bool hideNavigationIfSinglePage = true;

    private StageNodeButton[] _nodes = new StageNodeButton[0];
    private readonly List<StageNodeButton> _cardClones = new List<StageNodeButton>();
    private readonly List<Image> _dots = new List<Image>();
    private int _page;
    private int _openFrame = -1;
    private int _confirmOpenFrame = -1;
    private Vector3 _lastMousePosition;
    private float _lastMouseMoveTime = -10f;
    private StageNodeButton _selected;
    private bool _silentSelect; // ページをめくる時の選び直しで、カードを動かした音を鳴らさないため
    private Coroutine _lockedMessageRoutine;

    private float _prevTimeScale = 1f;
    private bool _prevCursorVisible;
    private CursorLockMode _prevCursorLockState;

    private bool IsConfirmOpen => confirmPanel != null && confirmPanel.activeSelf;

    private void Awake()
    {
        _instance = this;
    }

    private void Start()
    {
        if (panelRoot != null)
        {
            // 無効にする前に、マップ上のボタンを全部集めておく（無効のものも含める）。
            _nodes = panelRoot.GetComponentsInChildren<StageNodeButton>(true);
            panelRoot.SetActive(false);
        }
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        if (lockedMessage != null)
            lockedMessage.SetActive(false);

        if (backButton != null) backButton.onClick.AddListener(Close);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(Depart);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CloseConfirm);

        // カード式の表示の準備。
        if (cardTemplate != null)
            cardTemplate.gameObject.SetActive(false);
        if (dotTemplate != null)
            dotTemplate.gameObject.SetActive(false);
        if (previousPageButton != null) previousPageButton.onClick.AddListener(PreviousPage);
        if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            _backLocked = false;
        }

        if (IsOpen)
        {
            IsOpen = false;
            if (pauseGameWhileOpen)
                Time.timeScale = _prevTimeScale;
        }
    }

    // ---- 開く／閉じる ---------------------------------------------------

    public void Open()
    {
        if (IsOpen)
            return;

        StageCatalog catalog = StageCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogWarning("[StageSelectUI] StageCatalogが見つかりません。Resourcesフォルダに「StageCatalog」を作ってください。");
            return;
        }

        IsOpen = true;
        _openFrame = Time.frameCount;
        if (panelRoot != null)
            panelRoot.SetActive(true);
        ApplyBackButton();
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        HideLockedMessage();

        if (pauseGameWhileOpen)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        _prevCursorVisible = Cursor.visible;
        _prevCursorLockState = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 前に選んだボタンが残っていると、Enterキーでそのボタンが押されてしまうことがあるので、選択を外す。
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (CardMode)
        {
            BuildCards(catalog);
            _page = 0;
        }

        // 前に選んでいたカードは、覚えておかない。
        _selected = null;

        RefreshNodes(catalog);

        if (CardMode)
            SetPage(_selected != null ? PageOf(_selected) : 0);

        if (panelRoot != null)
        {
            var rt = panelRoot.GetComponent<RectTransform>();
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        if (CardMode)
            PositionCursor();

        if (introAnimator != null)
            introAnimator.PlayIntro();

        SoundEffects.Play(SEId.StageSelectOpen);
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        if (_backLocked)
            return;   // 戻れない間は、閉じない（戻るボタン・Esc）
        IsOpen = false;

        HideLockedMessage();
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (pauseGameWhileOpen)
            Time.timeScale = _prevTimeScale;

        Cursor.visible = _prevCursorVisible;
        Cursor.lockState = _prevCursorLockState;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>戻る操作（Esc）：確認の画面が出ていればそれを閉じ、出ていなければ全体を閉じる。</summary>
    private void GoBack()
    {
        if (IsConfirmOpen) CloseConfirm();
        else Close();
    }

    // ---- ステージのボタンが押された時 ------------------------------------------

    /// <summary>解放済みのステージのボタンが押された時に、StageNodeButtonから呼ばれる。</summary>
    public void OnNodeClicked(StageNodeButton node)
    {
        HideLockedMessage();
        Select(node);
        if (node == null || !node.IsUnlocked)
            return;

        SoundEffects.Play(SEId.StageSelectDecide);

        if (confirmPanel == null)
        {
            // 確認の画面を作っていない場合は、確認なしですぐに出発する。
            Depart();
            return;
        }
        OpenConfirm();
    }

    /// <summary>ロック中（未解放）のステージのボタンが押された時に、StageNodeButtonから呼ばれる。</summary>
    public void OnLockedNodeClicked(StageNodeButton node)
    {
        SoundEffects.Play(SEId.StageSelectLocked);

        if (lockedMessage == null)
            return;

        if (lockedMessageText != null)
        {
            string message = "まだ挑戦できません";
            StageCatalog catalog = StageCatalog.Instance;
            if (catalog != null && node != null)
            {
                int index = catalog.IndexOf(node.stageId);
                if (index > 0)
                    message = "「" + catalog.stages[index - 1].displayName + "」をクリアすると挑戦できます";
            }
            lockedMessageText.text = message;
        }

        lockedMessage.SetActive(true);
        lockedMessage.transform.SetAsLastSibling(); // カードなどより、手前に出す

        if (_lockedMessageRoutine != null)
            StopCoroutine(_lockedMessageRoutine);
        _lockedMessageRoutine = StartCoroutine(HideLockedMessageAfterDelay());
    }

    private IEnumerator HideLockedMessageAfterDelay()
    {
        // ゲームを止めている間も進むよう、Realtimeを使う。
        yield return new WaitForSecondsRealtime(lockedMessageSeconds);
        _lockedMessageRoutine = null;
        if (lockedMessage != null)
            lockedMessage.SetActive(false);
    }

    private void HideLockedMessage()
    {
        if (_lockedMessageRoutine != null)
        {
            StopCoroutine(_lockedMessageRoutine);
            _lockedMessageRoutine = null;
        }
        if (lockedMessage != null)
            lockedMessage.SetActive(false);
    }

    // ---- 確認の画面 ---------------------------------------------------

    private void OpenConfirm()
    {
        if (confirmPanel == null)
            return;

        if (confirmStageNameText != null && _selected != null)
        {
            StageCatalog catalog = StageCatalog.Instance;
            StageCatalog.Entry entry = catalog != null ? catalog.Find(_selected.stageId) : null;
            confirmStageNameText.text = entry != null ? entry.displayName : "";
        }

        confirmPanel.SetActive(true);
        confirmPanel.transform.SetAsLastSibling(); // カードなどより、一番手前に出す
        _confirmOpenFrame = Time.frameCount;

        // Enterキーで、裏のボタンが押されてしまわないように、選択を外す。
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>確認の画面を閉じて、ステージ選択の画面に戻る。</summary>
    private void CloseConfirm()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // ---- 表示の更新 ---------------------------------------------------

    private void RefreshNodes(StageCatalog catalog)
    {
        StageNodeButton defaultNode = null;
        StageNodeButton lastUnlocked = null;

        foreach (StageNodeButton node in _nodes)
        {
            if (node == null)
                continue;

            StageCatalog.Entry entry = catalog.Find(node.stageId);
            bool unlocked = entry != null && StageProgress.IsUnlocked(node.stageId);
            bool cleared = entry != null && StageProgress.IsCleared(node.stageId);
            node.Setup(entry, unlocked, cleared, this);

            if (unlocked)
            {
                lastUnlocked = node;
                // 解放済みで、まだクリアしていない一番手前のステージを、最初の選択にする。
                if (defaultNode == null && !cleared)
                    defaultNode = node;
            }
        }

        // 全部クリア済みなら、最後に解放されたステージを選んでおく。
        Select(defaultNode != null ? defaultNode : lastUnlocked);
    }

    /// <summary>ステージを選んで、情報欄を更新する（確認の画面は出さない）。</summary>
    public void Select(StageNodeButton node)
    {
        StageNodeButton previous = _selected;
        _selected = node;

        if (!_silentSelect && previous != null && node != null && previous != node)
            SoundEffects.Play(SEId.StageSelectMove);

        foreach (StageNodeButton n in _nodes)
        {
            if (n != null)
                n.SetSelected(n == node);
        }

        StageCatalog catalog = StageCatalog.Instance;
        StageCatalog.Entry entry = (node != null && catalog != null) ? catalog.Find(node.stageId) : null;

        // ロック中のステージは、名前も説明も隠す（カードの表示と合わせる）。
        bool locked = node != null && !node.IsUnlocked;
        if (stageNameText != null)
            stageNameText.text = entry == null ? "" : (locked ? node.lockedLabel : entry.displayName);
        if (stageDescriptionText != null)
            stageDescriptionText.text = (entry == null || locked) ? "" : entry.description;
        if (stageStateText != null)
        {
            if (node == null) stageStateText.text = "";
            else if (locked) stageStateText.text = "挑戦できない";
            else if (node.IsCleared) stageStateText.text = "クリア済み";
            else stageStateText.text = "挑戦できる";
        }

        // ベストタイム：クリア済みで、ロックされていないステージだけ出す。
        string bestLabel = "";
        if (node != null && !locked && node.IsCleared)
        {
            float best;
            bestLabel = "ベストタイム " + (StageProgress.TryGetBestTime(node.stageId, out best)
                ? StageProgress.FormatTime(best)
                : "--:--.--");
        }
        if (stageBestTimeText != null)
        {
            stageBestTimeText.text = bestLabel;
        }
        else if (stageStateText != null && bestLabel.Length > 0)
        {
            stageStateText.text += "　" + bestLabel;
        }

        // カード式の表示：選んだカードのあるページを出して、印（▽）を移す。
        if (CardMode)
        {
            if (node != null && PageOf(node) != _page)
                SetPage(PageOf(node));
            else
                PositionCursor();
        }
    }

    // ---- カード式（ページめくり）の表示 --------------------------------------

    private bool CardMode => cardTemplate != null;

    private int CardsPerPageSafe => Mathf.Max(1, cardsPerPage);

    private int PageCount => Mathf.Max(1, (_nodes.Length + CardsPerPageSafe - 1) / CardsPerPageSafe);

    private int PageOf(StageNodeButton node)
    {
        int index = System.Array.IndexOf(_nodes, node);
        return index < 0 ? 0 : index / CardsPerPageSafe;
    }

    /// <summary>StageCatalogのステージの数だけ、カードを作って並べる（数が同じ間は、作り直さない）。</summary>
    private void BuildCards(StageCatalog catalog)
    {
        Transform parent = cardContainer != null ? (Transform)cardContainer : cardTemplate.transform.parent;
        int count = catalog.stages.Count;

        if (_cardClones.Count != count)
        {
            for (int i = 0; i < _cardClones.Count; i++)
            {
                if (_cardClones[i] != null)
                {
                    _cardClones[i].gameObject.SetActive(false);
                    Destroy(_cardClones[i].gameObject);
                }
            }
            _cardClones.Clear();

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(cardTemplate.gameObject, parent);
                go.SetActive(false); // 表示は、ページごとに決める
                _cardClones.Add(go.GetComponent<StageNodeButton>());
            }
        }

        for (int i = 0; i < count; i++)
        {
            StageNodeButton card = _cardClones[i];
            StageCatalog.Entry entry = catalog.stages[i];
            card.stageId = entry != null ? entry.id : "";
            card.gameObject.name = "Card_" + card.stageId;
        }
        _nodes = _cardClones.ToArray();
    }

    /// <summary>指定したページのカードだけを表示して、矢印・点・印を更新する。</summary>
    private void SetPage(int page)
    {
        _page = Mathf.Clamp(page, 0, PageCount - 1);

        int per = CardsPerPageSafe;
        for (int i = 0; i < _nodes.Length; i++)
        {
            if (_nodes[i] != null)
                _nodes[i].gameObject.SetActive(i / per == _page);
        }

        RefreshNavigation();

        if (cardContainer != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
        }
        PositionCursor();
    }

    private void PreviousPage()
    {
        ChangePage(-1);
    }

    private void NextPage()
    {
        ChangePage(1);
    }

    /// <summary>ページをめくる。選んでいるカードは、新しいページの同じ位置のカードに移す。</summary>
    private void ChangePage(int delta)
    {
        if (!IsOpen || IsConfirmOpen || !CardMode)
            return;

        int newPage = Mathf.Clamp(_page + delta, 0, PageCount - 1);
        if (newPage == _page)
            return;

        HideLockedMessage();

        int per = CardsPerPageSafe;
        int slot = 0;
        if (_selected != null)
        {
            int current = System.Array.IndexOf(_nodes, _selected);
            if (current >= 0)
                slot = current % per;
        }
        int index = Mathf.Min(newPage * per + slot, _nodes.Length - 1);

        SetPage(newPage);
        SoundEffects.Play(SEId.StageSelectPage);
        if (index >= 0 && index < _nodes.Length)
        {
            _silentSelect = true; // ページをめくる音だけ鳴らす
            Select(_nodes[index]);
            _silentSelect = false;
        }
    }

    private void RefreshNavigation()
    {
        int pages = PageCount;
        bool show = !(hideNavigationIfSinglePage && pages <= 1);

        if (previousPageButton != null)
        {
            previousPageButton.gameObject.SetActive(show);
            previousPageButton.interactable = _page > 0;
        }
        if (nextPageButton != null)
        {
            nextPageButton.gameObject.SetActive(show);
            nextPageButton.interactable = _page < pages - 1;
        }

        if (dotContainer != null && dotTemplate != null)
        {
            dotContainer.gameObject.SetActive(show);

            while (_dots.Count < pages)
            {
                Image dot = Instantiate(dotTemplate, dotContainer);
                dot.gameObject.SetActive(true);
                _dots.Add(dot);
            }
            while (_dots.Count > pages)
            {
                int last = _dots.Count - 1;
                if (_dots[last] != null)
                {
                    _dots[last].gameObject.SetActive(false);
                    Destroy(_dots[last].gameObject);
                }
                _dots.RemoveAt(last);
            }

            for (int i = 0; i < _dots.Count; i++)
            {
                Sprite s = (i == _page) ? dotOnSprite : dotOffSprite;
                if (s != null)
                    _dots[i].sprite = s;
            }
        }
    }

    /// <summary>印（▽）を、選んでいるカードの上に移す。</summary>
    private void PositionCursor()
    {
        if (cursor == null)
            return;

        if (_selected == null || !_selected.gameObject.activeInHierarchy)
        {
            cursor.gameObject.SetActive(false);
            return;
        }

        cursor.gameObject.SetActive(true);
        RectTransform card = (RectTransform)_selected.transform;
        Vector3 top = card.TransformPoint(new Vector3(card.rect.center.x, card.rect.yMax, 0f));
        cursor.position = top + new Vector3(0f, cursorOffsetY * cursor.lossyScale.y, 0f);
    }

    /// <summary>マウスを乗せたカードを選ぶ（StageNodeButtonから）。ページをめくった時に、止まっているマウスの下のカードを選んでしまわないよう、マウスが動いた時だけ受け付ける。</summary>
    public void OnNodeHovered(StageNodeButton node)
    {
        if (!IsOpen || IsConfirmOpen || !CardMode || node == null || node == _selected)
            return;
        if (Time.unscaledTime - _lastMouseMoveTime > 0.15f)
            return;
        Select(node);
    }

    private void Update()
    {
        if (!IsOpen || !CardMode)
            return;

        // マウスが動いたかを覚えておく（ホバーの判定に使う）。
        if ((Input.mousePosition - _lastMousePosition).sqrMagnitude > 0.25f)
            _lastMouseMoveTime = Time.unscaledTime;
        _lastMousePosition = Input.mousePosition;

        // 開いたのと同じフレームの入力は、無視する。
        if (Time.frameCount == _openFrame)
            return;

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        if (IsConfirmOpen)
        {
            // 確認の画面：Enterで「はい」。開いたのと同じフレームのEnterは無視する。
            if (enter && Time.frameCount != _confirmOpenFrame)
                Depart();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            MoveSelection(-1);
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            MoveSelection(1);
        else if (enter)
            ActivateSelected();
    }

    /// <summary>←→キー：選ぶカードを1つ動かす（ページの端を越えたら、ページもめくる）。</summary>
    private void MoveSelection(int delta)
    {
        if (_nodes.Length == 0)
            return;

        int current = _selected != null ? System.Array.IndexOf(_nodes, _selected) : 0;
        int index = Mathf.Clamp(current + delta, 0, _nodes.Length - 1);
        if (index == current && _selected != null)
            return;

        HideLockedMessage();
        Select(_nodes[index]);
    }

    /// <summary>Enterキー：選んでいるカードを、クリックした時と同じに扱う。</summary>
    private void ActivateSelected()
    {
        if (_selected == null)
            return;
        if (_selected.IsUnlocked)
            OnNodeClicked(_selected);
        else
            OnLockedNodeClicked(_selected);
    }

    // ---- 出発 ---------------------------------------------------

    private void Depart()
    {
        if (_selected == null || !_selected.IsUnlocked)
            return;

        StageCatalog catalog = StageCatalog.Instance;
        StageCatalog.Entry entry = catalog != null ? catalog.Find(_selected.stageId) : null;
        if (entry == null || string.IsNullOrEmpty(entry.sceneName))
        {
            Debug.LogWarning("[StageSelectUI] このステージのシーン名が、StageCatalogに設定されていません。");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(entry.sceneName))
        {
            Debug.LogWarning("[StageSelectUI] シーン「" + entry.sceneName + "」を読み込めません。File > Build Settingsに、このシーンを追加してください。");
            return;
        }

        // 止めていた時間とカーソルの状態を戻してから、シーンを切り替える。（出発するときは、戻れない設定もOFFに戻す）
        BackLocked = false;
        Close();
        LoadingScreen.Load(entry.sceneName);
    }
}