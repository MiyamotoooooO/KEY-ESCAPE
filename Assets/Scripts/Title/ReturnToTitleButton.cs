using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - 設定画面の「タイトルへ戻る」ボタン。
/// 押すと確認の画面が出て、「はい」でタイトル画面へ戻る。
/// タイトル画面の設定画面では、ボタン自身が非表示になる。
///
/// 設定画面（SettingsPanelプレハブ）の中に作ること。
/// 確認の画面などの参照が、プレハブの中で完結するので、どのシーンでも同じように動く。
/// </summary>
[RequireComponent(typeof(Button))]
public class ReturnToTitleButton : MonoBehaviour
{
    private static ReturnToTitleButton _active;

    /// <summary>
    /// Escキーで呼ばれる（SettingsUIから）。確認の画面が出ていれば閉じて、trueを返す。
    /// trueの時は、Escで設定画面を閉じない。
    /// </summary>
    public static bool CloseConfirmIfOpen()
    {
        if (_active != null && _active.confirmPanel != null && _active.confirmPanel.activeSelf)
        {
            _active.confirmPanel.SetActive(false);
            _active.SelectSelf();
            return true;
        }
        return false;
    }

    [Header("確認の画面")]
    [Tooltip("確認の画面全体（暗い背景＋メッセージ＋ボタン）。SettingsPanelの中の、一番最後（一番手前）の子にする。")]
    public GameObject confirmPanel;
    public Button confirmYesButton;
    public Button confirmNoButton;

    [Header("そのほか")]
    [Tooltip("タイトル画面のシーン名。Build Settingsに登録した名前と同じにする。")]
    public string titleSceneName = "TitleScene";
    [Tooltip("任意。空なら、押した時にシーンの中から探す。")]
    public SettingsUI settingsUI;

    private Button _button;
    private bool _leaving;

    private void Awake()
    {
        // タイトル画面の設定画面では、このボタンは要らない。
        if (SceneManager.GetActiveScene().name == titleSceneName)
        {
            gameObject.SetActive(false);
            return;
        }

        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClicked);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(Leave);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CloseConfirm);
    }

    private void OnEnable()
    {
        _active = this;
        // 設定画面を開き直した時は、確認の画面を閉じた状態から始める。
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (_active == this)
            _active = null;
    }

    private void OnClicked()
    {
        if (_leaving)
            return;

        if (confirmPanel == null)
        {
            Leave();
            return;
        }

        confirmPanel.SetActive(true);

        // うっかりEnterで戻ってしまわないよう、「もどる」を選んでおく。
        if (EventSystem.current != null && confirmNoButton != null)
            EventSystem.current.SetSelectedGameObject(confirmNoButton.gameObject);
    }

    private void CloseConfirm()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        SelectSelf();
    }

    private void SelectSelf()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Leave()
    {
        if (_leaving)
            return;

        if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            Debug.LogWarning("[ReturnToTitleButton] シーン「" + titleSceneName + "」を読み込めません。File > Build Settingsに、このシーンを追加してください。");
            return;
        }
        _leaving = true;

        // 設定画面を閉じる（止めていた時間とカーソルを元に戻す）。
        SettingsUI settings = settingsUI != null ? settingsUI : FindObjectOfType<SettingsUI>();
        if (settings != null && SettingsUI.IsOpen)
            settings.Toggle();
        Time.timeScale = 1f;

        // キーの仕組みをリセットして、タイトルへ。
        GameSession.ResetKeySystem();
        SceneManager.LoadScene(titleSceneName);
    }
}