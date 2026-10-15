using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - 設定画面（BGM／SE／スクリーンモード／操作ガイドの表示）。
/// Escキーで開閉する。開いている間はゲームを止める（Time.timeScale = 0）。
/// 開いた時に、看板が上から落ちてくる演出（KeyConfigIntroAnimator）を再生する。
///
/// 値の保存・維持はGameSettings（static）が行うので、このUIはその値を表示して、
/// 変更を書き込むだけ。
///
/// 注意：このスクリプトは、panelRootの外（常に有効なオブジェクト）に付けること。
/// panelRootに付けると、パネルが無効の間はUpdateが動かず、Escキーで開けない。
/// </summary>
public class SettingsUI : MonoBehaviour
{
    /// <summary>設定画面が開いているか。KeyConfigUIが、Tabキーを無視するために参照する。</summary>
    public static bool IsOpen { get; private set; }

    [Header("画面")]
    [Tooltip("設定画面全体（黒い半透明の背景、枠、看板、各項目を含む親）。最初は無効でも有効でもよい（Start()で非表示にする）。")]
    public GameObject panelRoot;
    public KeyCode toggleKey = KeyCode.Escape;
    [Tooltip("この画面が開いている間、ゲームを止める（Time.timeScale = 0）。")]
    public bool pauseGameWhileOpen = true;
    [Tooltip("任意。開いた時の演出（看板が落ちてくる）。panelRootに付けたKeyConfigIntroAnimatorを割り当てる。")]
    public KeyConfigIntroAnimator introAnimator;
    [Tooltip("任意。キーコンフィグ画面が開いている間はEscを無視するために使う。空なら、シーン内から自動で探す。")]
    public KeyConfigUI keyConfigUI;

    [Header("BGM")]
    public Slider bgmSlider;
    public TMP_Text bgmValueText;

    [Header("SE（効果音）")]
    public Slider seSlider;
    public TMP_Text seValueText;
    [Tooltip("任意。SEのスライダーを動かした時に、音量の確認用に鳴らす効果音。")]
    public AudioClip seTestClip;

    [Header("スクリーンモード")]
    public Button fullscreenButton;
    public Button windowedButton;

    [Header("操作ガイドの表示")]
    public Button guideOnButton;
    public Button guideOffButton;

    [Header("ボタン")]
    public Button backButton;
    public Button resetButton;

    [Header("選択中の項目の色（2択のボタン）")]
    public Color selectedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    public Color unselectedColor = Color.white;

    private float _prevTimeScale = 1f;
    private bool _prevCursorVisible;
    private CursorLockMode _prevCursorLockState;
    private bool _refreshing;      // 画面の値を更新している間、変更イベントを無視する
    private float _lastTestSeTime = -10f;

    private void Start()
    {
        if (keyConfigUI == null)
            keyConfigUI = FindObjectOfType<KeyConfigUI>(true);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        // Inspectorでは何も繋がなくてよいように、ここでコードから繋ぐ。
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        if (seSlider != null) seSlider.onValueChanged.AddListener(OnSeSliderChanged);
        if (fullscreenButton != null) fullscreenButton.onClick.AddListener(() => GameSettings.SetFullscreen(true));
        if (windowedButton != null) windowedButton.onClick.AddListener(() => GameSettings.SetFullscreen(false));
        if (guideOnButton != null) guideOnButton.onClick.AddListener(() => GameSettings.SetControlGuideVisible(true));
        if (guideOffButton != null) guideOffButton.onClick.AddListener(() => GameSettings.SetControlGuideVisible(false));
        if (backButton != null) backButton.onClick.AddListener(Close);
        if (resetButton != null) resetButton.onClick.AddListener(GameSettings.ResetToDefaults);

        GameSettings.Changed += RefreshFromSettings;
    }

    private void OnDestroy()
    {
        GameSettings.Changed -= RefreshFromSettings;
        if (IsOpen)
        {
            IsOpen = false;
            if (pauseGameWhileOpen)
                Time.timeScale = _prevTimeScale;
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(toggleKey)) return;
        if (TitleMenu.CloseConfirmIfOpen()) return;
        if (StageSelectUI.IsOpen) { StageSelectUI.CloseIfOpen(); return; }
        if (keyConfigUI != null && keyConfigUI.IsOpen) return;
        Toggle();
    }

    // ---- 開く／閉じる ---------------------------------------------------

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen)
            return;
        IsOpen = true;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (pauseGameWhileOpen)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        // ゲームプレイ中に隠している/ロックしているマウスカーソルを、画面を開いている間だけ表示する。
        _prevCursorVisible = Cursor.visible;
        _prevCursorLockState = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RefreshFromSettings();

        // 無効だったパネルを最初に開いた時だけ、Layout Groupなどが未計算になることがあるので、
        // 強制的に再計算してから、演出を再生する。
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

        GameSettings.Save();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (pauseGameWhileOpen)
            Time.timeScale = _prevTimeScale;

        Cursor.visible = _prevCursorVisible;
        Cursor.lockState = _prevCursorLockState;
    }

    // ---- スライダー・ボタンの操作 -----------------------------------------

    private void OnBgmSliderChanged(float value)
    {
        if (_refreshing)
            return;
        GameSettings.SetBgmVolume(value / 100f);
    }

    private void OnSeSliderChanged(float value)
    {
        if (_refreshing)
            return;
        GameSettings.SetSeVolume(value / 100f);

        // 音量の確認用に、効果音を鳴らす（鳴らしすぎないよう、間隔を空ける）。
        if (seTestClip != null && Time.unscaledTime - _lastTestSeTime > 0.15f)
        {
            _lastTestSeTime = Time.unscaledTime;
            AudioManager.PlaySE(seTestClip);
        }
    }

    // ---- 画面の表示を、現在の設定に合わせる -----------------------------------

    private void RefreshFromSettings()
    {
        _refreshing = true;

        int bgm = Mathf.RoundToInt(GameSettings.BgmVolume * 100f);
        int se = Mathf.RoundToInt(GameSettings.SeVolume * 100f);

        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(bgm);
        if (seSlider != null) seSlider.SetValueWithoutNotify(se);
        if (bgmValueText != null) bgmValueText.text = bgm + "%";
        if (seValueText != null) seValueText.text = se + "%";

        SetSelected(fullscreenButton, GameSettings.Fullscreen);
        SetSelected(windowedButton, !GameSettings.Fullscreen);
        SetSelected(guideOnButton, GameSettings.ControlGuideVisible);
        SetSelected(guideOffButton, !GameSettings.ControlGuideVisible);

        _refreshing = false;
    }

    /// <summary>2択のボタンのうち、選ばれているほうの色を変える。</summary>
    private void SetSelected(Button button, bool selected)
    {
        if (button == null || button.image == null)
            return;
        button.image.color = selected ? selectedColor : unselectedColor;
    }
}