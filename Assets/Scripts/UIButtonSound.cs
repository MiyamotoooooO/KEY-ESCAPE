using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - ボタンに付けて、マウスを乗せた時とクリックした時に効果音を鳴らす。
/// 音はUISoundDefaults（Resources）の標準の音を使う。このボタンだけ別の音にしたい時は、
/// 下のOverrideの欄に割り当てる。
///
/// クリックは Button.onClick に登録するので、キーボードの決定キーでも鳴る。
/// 押せない状態（Interactableがオフ）のボタンでは鳴らさない。
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler
{
    public enum ClickKind
    {
        [Tooltip("通常のクリック音。")]
        Normal,
        [Tooltip("「もどる」用の音。")]
        Back
    }

    [Tooltip("「もどる」「閉じる」のボタンは、Backにする。")]
    public ClickKind clickKind = ClickKind.Normal;
    [Tooltip("マウスを乗せた時に音を鳴らすか。")]
    public bool playHover = true;

    [Header("このボタンだけ別の音にしたい時（空なら標準の音）")]
    public AudioClip hoverOverride;
    public AudioClip clickOverride;

    private Selectable _selectable;
    private Button _button;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _button = _selectable as Button;
        if (_button != null)
            _button.onClick.AddListener(PlayClick);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(PlayClick);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!playHover || _selectable == null || !_selectable.interactable)
            return;

        UISoundDefaults d = UISoundDefaults.Instance;
        AudioClip clip = hoverOverride != null ? hoverOverride : (d != null ? d.hoverClip : null);
        float volume = d != null ? d.hoverVolume : 1f;
        AudioManager.PlaySE(clip, volume);
    }

    private void PlayClick()
    {
        UISoundDefaults d = UISoundDefaults.Instance;

        AudioClip clip = clickOverride;
        if (clip == null && d != null)
        {
            clip = (clickKind == ClickKind.Back && d.backClip != null) ? d.backClip : d.clickClip;
        }
        float volume = d != null ? d.clickVolume : 1f;
        AudioManager.PlaySE(clip, volume);
    }
}