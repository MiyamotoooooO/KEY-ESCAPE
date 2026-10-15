using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - ステージ選択のマップ上に置く、ステージ1つ分のボタン。
/// StageIdで、StageCatalogのどのステージかを決める。
/// 表示（ロック・クリア済み・選択中）は、StageSelectUIが自動で更新する。
/// </summary>
[RequireComponent(typeof(Button))]
public class StageNodeButton : MonoBehaviour
{
    [Tooltip("StageCatalogのid（例：stage1）と同じにする。")]
    public string stageId = "stage1";

    [Header("表示（どれも任意）")]
    [Tooltip("ステージ名を表示するテキスト。未解放の時は「？」になる。")]
    public TMP_Text label;
    [Tooltip("未解放の時に表示するロックの絵。")]
    public GameObject lockedIcon;
    [Tooltip("クリア済みの時に表示する印（旗、ハンコなど）。")]
    public GameObject clearedMark;
    [Tooltip("選択中の時に表示する枠。")]
    public GameObject selectedFrame;

    public bool IsUnlocked { get; private set; }
    public bool IsCleared { get; private set; }

    private Button _button;
    private StageSelectUI _owner;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_button != null)
            return;
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        if (_owner != null)
            _owner.Select(this);
    }

    public void Setup(StageCatalog.Entry entry, bool unlocked, bool cleared, StageSelectUI owner)
    {
        EnsureInitialized();
        _owner = owner;
        IsUnlocked = unlocked;
        IsCleared = cleared;

        _button.interactable = unlocked;

        if (label != null)
            label.text = unlocked && entry != null ? entry.displayName : "？";
        if (lockedIcon != null)
            lockedIcon.SetActive(!unlocked);
        if (clearedMark != null)
            clearedMark.SetActive(unlocked && cleared);
        if (selectedFrame != null)
            selectedFrame.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
            selectedFrame.SetActive(selected);
    }
}