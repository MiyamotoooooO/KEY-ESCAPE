using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - タイトル画面のメニュー（はじめから／つづきから／設定／終了）。
///
/// ・はじめから：セーブがあれば確認の画面を出し、「はい」でセーブを消して拠点へ進む。セーブが無ければ、確認なしで進む。
/// ・つづきから：セーブがある時だけ押せる。拠点へ進む。
/// ・設定：既存の設定画面（SettingsUI）を開く。
/// ・終了：ゲームを終了する。
///
/// 注意：このスクリプトは、常に有効なオブジェクト（TitleController）に付けること。
/// 注意：TitleSceneには、Bootstrap（KeyInventory／KeyBindingManager）を置かないこと。
///       （置くと、起動時にセーブ内容が復元され、「はじめから」で消したはずのキーが残ってしまう。）
/// </summary>
public class TitleMenu : MonoBehaviour
{
    private static TitleMenu _instance;

    /// <summary>
    /// Escキーで呼ばれる（SettingsUIから）。確認の画面が出ていれば閉じて、trueを返す。
    /// trueの時は、Escで設定画面を開かない。
    /// </summary>
    public static bool CloseConfirmIfOpen()
    {
        if (_instance != null && _instance.IsConfirmOpen)
        {
            _instance.CloseConfirm();
            return true;
        }
        return false;
    }

    [Header("メニューのボタン")]
    public Button newGameButton;
    public Button continueButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("オープニングムービー")]
    [Tooltip("ゲームを起動した最初の1回だけ流す、ストーリームービー。終わると、そのままタイトル画面になる。空なら、流さない。")]
    public OpeningMovie openingMovie;
    [Tooltip("OFFにすると、ムービーを流さない（確認用）。")]
    public bool playOpeningMovie = true;

    [Header("設定画面")]
    [Tooltip("設定画面のSettingsUI（SettingsControllerに付いているもの）。")]
    public SettingsUI settingsUI;

    [Header("「はじめから」の確認の画面")]
    [Tooltip("確認の画面全体。Canvasの中の、一番最後（一番手前）の子にする。")]
    public GameObject confirmPanel;
    public Button confirmYesButton;
    public Button confirmNoButton;

    [Header("そのほか")]
    [Tooltip("任意。バージョン表示（例：v0.1）。Player Settingsのバージョンが入る。")]
    public TMP_Text versionText;
    [Tooltip("キーボード（矢印キー＋Enter）でも選べるよう、最初のボタンを選んでおく。")]
    public bool selectFirstButton = true;

    private bool _hasSave;
    private bool _loading;

    private bool IsConfirmOpen => confirmPanel != null && confirmPanel.activeSelf;

    private void Awake()
    {
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Start()
    {
        // ほかのシーンから戻ってきた時のために、時間とカーソルを通常に戻しておく。
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        _hasSave = HasSaveData();
        if (continueButton != null)
            continueButton.interactable = _hasSave;

        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(StartNewGame);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CloseConfirm);

        if (versionText != null)
            versionText.text = "v" + Application.version;

        // 起動して最初の1回なら、ストーリームービーを流す（終わったら、ここでボタンを選ぶ）。
        if (openingMovie != null)
            openingMovie.BeginTitle(playOpeningMovie, SelectFirstButton);
        else
            SelectFirstButton();
    }

    private void SelectFirstButton()
    {
        if (selectFirstButton)
            SelectButton(_hasSave ? continueButton : newGameButton);
    }

    // ---- セーブがあるか ---------------------------------------------------

    private static bool HasSaveData()
    {
        SaveData d = SaveManager.Data;
        if (d == null)
            return false;
        return d.hasKeyData || (d.clearedStages != null && d.clearedStages.Count > 0);
    }

    // ---- ボタン ---------------------------------------------------

    private void OnNewGameClicked()
    {
        if (_loading)
            return;

        if (_hasSave && confirmPanel != null)
            OpenConfirm();
        else
            StartNewGame();
    }

    private void OnContinueClicked()
    {
        if (_loading || !_hasSave)
            return;
        LoadHub();
    }

    private void OnSettingsClicked()
    {
        if (_loading)
            return;
        if (settingsUI == null)
        {
            Debug.LogWarning("[TitleMenu] Settings UIが割り当てられていません。");
            return;
        }
        if (!SettingsUI.IsOpen)
            settingsUI.Toggle();
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---- 確認の画面 ---------------------------------------------------

    private void OpenConfirm()
    {
        confirmPanel.SetActive(true);
        // うっかりEnterで消してしまわないよう、「もどる」を選んでおく。
        SelectButton(confirmNoButton);
    }

    private void CloseConfirm()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        SelectButton(newGameButton);
    }

    // ---- ゲーム開始 ---------------------------------------------------

    private void StartNewGame()
    {
        if (_loading)
            return;
        SaveManager.DeleteSave();
        LoadHub();
    }

    private void LoadHub()
    {
        StageCatalog catalog = StageCatalog.Instance;
        string sceneName = (catalog != null && !string.IsNullOrEmpty(catalog.hubSceneName))
            ? catalog.hubSceneName
            : "SelectScene";

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[TitleMenu] シーン「" + sceneName + "」を読み込めません。File > Build Settingsに、このシーンを追加してください。");
            return;
        }

        _loading = true;
        SceneManager.LoadScene(sceneName);
    }

    private static void SelectButton(Button b)
    {
        if (b != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(b.gameObject);
    }
}