using System.Collections;
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

    private StageNodeButton[] _nodes = new StageNodeButton[0];
    private StageNodeButton _selected;
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
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

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
        if (panelRoot != null)
            panelRoot.SetActive(true);
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

        RefreshNodes(catalog);

        if (panelRoot != null)
        {
            var rt = panelRoot.GetComponent<RectTransform>();
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        if (introAnimator != null)
            introAnimator.PlayIntro();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
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
        _selected = node;

        foreach (StageNodeButton n in _nodes)
        {
            if (n != null)
                n.SetSelected(n == node);
        }

        StageCatalog catalog = StageCatalog.Instance;
        StageCatalog.Entry entry = (node != null && catalog != null) ? catalog.Find(node.stageId) : null;

        if (stageNameText != null)
            stageNameText.text = entry != null ? entry.displayName : "";
        if (stageDescriptionText != null)
            stageDescriptionText.text = entry != null ? entry.description : "";
        if (stageStateText != null)
        {
            if (node == null) stageStateText.text = "";
            else if (node.IsCleared) stageStateText.text = "クリア済み";
            else stageStateText.text = "挑戦できる";
        }
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

        // 止めていた時間とカーソルの状態を戻してから、シーンを切り替える。
        Close();
        SceneManager.LoadScene(entry.sceneName);
    }
}