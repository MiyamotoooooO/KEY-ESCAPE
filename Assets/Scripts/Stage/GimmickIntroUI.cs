using TMPro;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ギミック紹介（GimmickIntroZone）の、説明パネルとSpaceキーの案内を出し入れする部品。
/// プレハブ（Assets/Prefab/GimmickIntroUI.prefab）のルートに付ける。
/// ゲームを止めている間も動くよう、実時間で動く。
/// </summary>
public class GimmickIntroUI : MonoBehaviour
{
    [Tooltip("説明パネル全体。最初は非表示になる。")]
    public GameObject panel;
    [Tooltip("説明の文字。")]
    public TMP_Text messageText;
    [Tooltip("Spaceキーの案内（BlinkingPrompt を付けると点滅する）。最初は非表示になる。")]
    public GameObject spacePrompt;
    [Tooltip("任意。パネルをふわっと出すための CanvasGroup。")]
    public CanvasGroup fadeGroup;
    [Tooltip("パネルが出るまでの秒数。")]
    public float fadeInTime = 0.2f;

    private bool _showing;
    private float _time;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (spacePrompt != null) spacePrompt.SetActive(false);
    }

    /// <summary>説明を出す。Spaceキーの案内は、SetPromptVisible で別に出す。</summary>
    public void Show(string message)
    {
        if (messageText != null)
            messageText.text = message;
        if (spacePrompt != null)
            spacePrompt.SetActive(false);
        if (panel != null)
            panel.SetActive(true);
        _time = 0f;
        _showing = true;
        if (fadeGroup != null)
            fadeGroup.alpha = fadeInTime > 0f ? 0f : 1f;
    }

    public void SetPromptVisible(bool visible)
    {
        if (spacePrompt != null && spacePrompt.activeSelf != visible)
            spacePrompt.SetActive(visible);
    }

    public void Hide()
    {
        _showing = false;
        if (spacePrompt != null) spacePrompt.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (!_showing || fadeGroup == null || fadeInTime <= 0f)
            return;
        _time += Time.unscaledDeltaTime;
        fadeGroup.alpha = Mathf.Clamp01(_time / fadeInTime);
    }
}
