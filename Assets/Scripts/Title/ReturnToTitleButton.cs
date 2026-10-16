using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - 設定画面の「タイトルへ戻る」「拠点へ戻る」ボタン。
/// 押すと確認の画面が出て、「はい」で戻る。
///   ・Destination = Title … タイトル画面へ戻る。タイトル画面の設定画面では、ボタン自身が非表示になる。
///   ・Destination = Hub   … ステージの途中から、拠点（StageCatalog の Hub Scene Name）へ戻る。
///                           StageCatalog に登録したステージのシーン以外（拠点・タイトル）では、ボタン自身が非表示になる。
///                           クリア扱いにはならない（報酬は付かない）。
///
/// 設定画面（SettingsPanelプレハブ）の中に作ること。
/// 確認の画面などの参照が、プレハブの中で完結するので、どのシーンでも同じように動く。
/// </summary>
[RequireComponent(typeof(Button))]
public class ReturnToTitleButton : MonoBehaviour
{
    /// <summary>戻り先。</summary>
    public enum Destination
    {
        Title,
        Hub,
    }

    // 設定画面に、ボタン（タイトル用・拠点用）が複数あるので、いま開いているものをすべて覚えておく。
    private static readonly System.Collections.Generic.List<ReturnToTitleButton> _actives =
        new System.Collections.Generic.List<ReturnToTitleButton>();

    /// <summary>
    /// Escキーで呼ばれる（SettingsUIから）。確認の画面が出ていれば閉じて、trueを返す。
    /// trueの時は、Escで設定画面を閉じない。
    /// </summary>
    public static bool CloseConfirmIfOpen()
    {
        for (int i = 0; i < _actives.Count; i++)
        {
            ReturnToTitleButton b = _actives[i];
            if (b != null && b.confirmPanel != null && b.confirmPanel.activeSelf)
            {
                b.confirmPanel.SetActive(false);
                b.SelectSelf();
                return true;
            }
        }
        return false;
    }

    [Header("確認の画面")]
    [Tooltip("確認の画面全体（暗い背景＋メッセージ＋ボタン）。SettingsPanelの中の、一番最後（一番手前）の子にする。")]
    public GameObject confirmPanel;
    public Button confirmYesButton;
    public Button confirmNoButton;

    [Header("戻り先")]
    [Tooltip("Title＝タイトル画面へ戻る。Hub＝ステージの途中から拠点へ戻る（ステージのシーンでだけ表示される）。")]
    public Destination destination = Destination.Title;

    [Header("そのほか")]
    [Tooltip("タイトル画面のシーン名。Build Settingsに登録した名前と同じにする。")]
    public string titleSceneName = "TitleScene";
    [Tooltip("任意。空なら、押した時にシーンの中から探す。")]
    public SettingsUI settingsUI;

    private Button _button;
    private bool _leaving;

    private void Awake()
    {
        // タイトル用：タイトル画面の設定画面では、このボタンは要らない。
        // 拠点用：ステージのシーン以外（拠点・タイトルなど）では、このボタンは要らない。
        if (destination == Destination.Title
            ? SceneManager.GetActiveScene().name == titleSceneName
            : !IsStageScene(SceneManager.GetActiveScene().name))
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
        if (!_actives.Contains(this))
            _actives.Add(this);
        // 設定画面を開き直した時は、確認の画面を閉じた状態から始める。
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    private void OnDisable()
    {
        _actives.Remove(this);
    }

    /// <summary>StageCatalog に登録されたステージのシーンか。</summary>
    private static bool IsStageScene(string sceneName)
    {
        StageCatalog catalog = StageCatalog.Instance;
        if (catalog == null || catalog.stages == null)
            return false;
        for (int i = 0; i < catalog.stages.Count; i++)
        {
            if (catalog.stages[i] != null && catalog.stages[i].sceneName == sceneName)
                return true;
        }
        return false;
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

        // 戻り先のシーン名を決める。
        string targetScene;
        if (destination == Destination.Hub)
        {
            StageCatalog catalog = StageCatalog.Instance;
            targetScene = catalog != null ? catalog.hubSceneName : null;
        }
        else
        {
            targetScene = titleSceneName;
        }

        if (string.IsNullOrEmpty(targetScene) || !Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogWarning("[ReturnToTitleButton] シーン「" + targetScene + "」を読み込めません。File > Build Settingsに、このシーンを追加してください（拠点の場合は、StageCatalogのHub Scene Nameも確認してください）。");
            return;
        }
        _leaving = true;

        // 設定画面を閉じる（止めていた時間とカーソルを元に戻す）。
        SettingsUI settings = settingsUI != null ? settingsUI : FindObjectOfType<SettingsUI>();
        if (settings != null && SettingsUI.IsOpen)
            settings.Toggle();
        Time.timeScale = 1f;

        if (destination == Destination.Hub)
        {
            // 拠点へ。途中でやめるので、クリア扱いにはしない。再開位置の記録も消す。
            // キーの仕組みは、ステージクリアで戻る時と同じく、リセットしない。
            CheckpointManager.Clear();
            SceneManager.LoadScene(targetScene);
            return;
        }

        // キーの仕組みをリセットして、タイトルへ。
        GameSession.ResetKeySystem();
        SceneManager.LoadScene(targetScene);
    }
}