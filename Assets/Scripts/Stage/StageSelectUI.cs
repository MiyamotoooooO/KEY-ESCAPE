using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - マップ風のステージ選択画面。拠点で、馬（バッグ）に話しかけると開く（StageSelectNPCから）。
/// マップ上のステージのボタン（StageNodeButton）を選んで、「出発」でそのステージのシーンへ移る。
/// 開いている間は、ゲームを止める（Time.timeScale = 0）。
///
/// 注意：このスクリプトは、panelRootの外（常に有効なオブジェクト）に付けること。
/// （SettingsUIと同じ理由：panelRootに付けると、無効の間はUpdateなどが動かない。）
/// 開いている間のEscとTabは無視される（「もどる」ボタンで閉じる）。
/// </summary>
public class StageSelectUI : MonoBehaviour
{
    /// <summary>ステージ選択画面が開いているか。他の画面が、Esc/Tabを無視するかの判断に使う。</summary>
    public static bool IsOpen { get; private set; }

    [Header("画面")]
    [Tooltip("ステージ選択画面全体（暗い背景、マップ、ステージのボタン、情報欄、ボタン）。")]
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
    public Button departButton;
    public Button backButton;

    private StageNodeButton[] _nodes = new StageNodeButton[0];
    private StageNodeButton _selected;

    private float _prevTimeScale = 1f;
    private bool _prevCursorVisible;
    private CursorLockMode _prevCursorLockState;

    private void Start()
    {
        if (panelRoot != null)
        {
            // 無効にする前に、マップ上のボタンを全部集めておく（無効のものも含める）。
            _nodes = panelRoot.GetComponentsInChildren<StageNodeButton>(true);
            panelRoot.SetActive(false);
        }

        if (departButton != null) departButton.onClick.AddListener(Depart);
        if (backButton != null) backButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
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

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (pauseGameWhileOpen)
            Time.timeScale = _prevTimeScale;

        Cursor.visible = _prevCursorVisible;
        Cursor.lockState = _prevCursorLockState;

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

    /// <summary>マップ上のステージのボタンがクリックされた時に、StageNodeButtonから呼ばれる。</summary>
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
        if (departButton != null)
            departButton.interactable = node != null && node.IsUnlocked;
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