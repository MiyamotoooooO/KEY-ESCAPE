using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - ステージ選択のマップ上に置く、ステージ1つ分のボタン。
/// StageIdで、StageCatalogのどのステージかを決める。
/// 表示（ロック・クリア済み・選択中）は、StageSelectUIが自動で更新する。
/// ロック中（未解放）のステージを押すと、StageSelectUIがメッセージを表示する。
/// </summary>
[RequireComponent(typeof(Button))]
public class StageNodeButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    [Tooltip("StageCatalogのid（例：stage1）と同じにする。")]
    public string stageId = "stage1";

    [Header("表示（どれも任意）")]
    [Tooltip("ステージ名を表示するテキスト。未解放の時は、下の「Locked Label」の文字になる。")]
    public TMP_Text label;
    [Tooltip("カード用。ステージの番号（例：1-1）を表示するテキスト。未解放の時も、番号は出す。")]
    public TMP_Text numberText;
    [Tooltip("カード用。ステージの小さな絵を表示するImage。StageCatalogの「Thumbnail」が入る。")]
    public Image thumbnail;
    [Tooltip("未解放の時に、ステージ名の代わりに出す文字。")]
    public string lockedLabel = "？？？";
    [Tooltip("未解放の時に表示するロックの絵。")]
    public GameObject lockedIcon;
    [Tooltip("クリア済みの時に表示する印（旗、ハンコなど）。")]
    public GameObject clearedMark;
    [Tooltip("選択中の時に表示する枠。")]
    public GameObject selectedFrame;
    [Tooltip("カード用。解放されたばかりで、まだカーソルを当てていないステージに出す吹き出し。一度カーソルを当てると消える（記録はセーブされる）。")]
    public GameObject newBadge;

    [Header("カード用の見た目")]
    [Tooltip("未解放の時に、カード全体をどれだけ薄くするか（1＝薄くしない）。")]
    [Range(0.1f, 1f)] public float lockedAlpha = 0.6f;
    [Tooltip("選択中の時の大きさの倍率（1＝大きくしない）。")]
    public float selectedScale = 1.06f;

    public bool IsUnlocked { get; private set; }
    public bool IsCleared { get; private set; }

    private Button _button;
    private StageSelectUI _owner;
    private Vector3 _baseScale = Vector3.one;

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
        _baseScale = transform.localScale;
    }

    // 解放済みのステージが押された時（Buttonのクリック）。
    private void OnClicked()
    {
        if (_owner != null)
            _owner.OnNodeClicked(this);
    }

    // ロック中のステージが押された時。押せない状態のButtonはonClickが呼ばれないので、
    // こちらでクリックを受け取って、メッセージを表示させる。
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (!IsUnlocked && _owner != null)
            _owner.OnLockedNodeClicked(this);
    }

    // マウスを乗せた時に、このカードを選ぶ（StageSelectUIが、マウスが動いた時だけ受け付ける）。
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_owner != null)
            _owner.OnNodeHovered(this);
    }

    public void Setup(StageCatalog.Entry entry, bool unlocked, bool cleared, StageSelectUI owner)
    {
        EnsureInitialized();
        _owner = owner;
        IsUnlocked = unlocked;
        IsCleared = cleared;

        _button.interactable = unlocked;

        if (label != null)
            label.text = unlocked && entry != null ? entry.displayName : lockedLabel;
        if (numberText != null)
            numberText.text = entry != null ? entry.stageNumber : "";
        if (thumbnail != null)
        {
            Sprite s = entry != null ? entry.thumbnail : null;
            thumbnail.sprite = s;
            thumbnail.enabled = s != null;
        }
        if (lockedAlpha < 1f)
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = unlocked ? 1f : lockedAlpha;
        }
        if (lockedIcon != null)
            lockedIcon.SetActive(!unlocked);
        if (clearedMark != null)
            clearedMark.SetActive(unlocked && cleared);
        if (selectedFrame != null)
            selectedFrame.SetActive(false);
    }

    /// <summary>「新しい」の吹き出しを、出す／消す。</summary>
    public void SetNew(bool isNew)
    {
        if (newBadge != null)
            newBadge.SetActive(isNew);
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
            selectedFrame.SetActive(selected);
        transform.localScale = selected ? _baseScale * selectedScale : _baseScale;
    }
}