using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// KEY ESCAPE - one row of the Key Config screen, representing a single
/// GameAction (spec Ver.0.1 §49 / Ver.0.2 §20). Chapter 1 has exactly 6
/// bindable actions, so this is meant to be placed 6 times by hand in the
/// scene (one per GameAction) rather than instantiated from a prefab - see
/// シーン構築ガイド.md.
///
/// ドラッグ&ドロップで割り当てる：左のキー一覧（KeyGridButtonUI）からキーを
/// ドラッグして、この行の「キースロット」（子のKeyDropSlot）にドロップすると、
/// HandleDrop()が呼ばれ、owner.TryBindByDrag(action, key)を試みる。
/// 行全体ではなく、キーのスプライトの位置（未設定の時はその同じ位置）だけが
/// ドロップ先になる。
///
/// キーをつかんでいる間は、ロックされていない行のキースロットの後ろに、黒い半透明の
/// 背景（dragBackdrop）を出して、ドロップ先を分かりやすくする
/// （KeyDragStateの通知を受けて切り替える）。さらに、つかんだキーがキースロットの
/// 上に重なっている間は、その背景の色をdragBackdropHoverColorに変えて、
/// 「ここに置ける」ことを知らせる（KeyDropSlotからSetDropHover()が呼ばれる）。
///
/// この行自体をクリックすると、右下の「キーのプレビュー」欄でそのアクションの
/// 動作（ジャンプ・攻撃など）をアニメーションで再生する
/// （owner.PlayActionPreview(action)経由。実体はKeyConfigPreviewPlayer）。
/// </summary>
public class ActionBindingRow : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Which action this row represents.")]
    public GameAction action;

    [Header("UI references")]
    public TMP_Text actionLabel;
    [Tooltip("現在のキーの文字（例：\"A\"、\"Space\"）。Current Key Backgroundの上に重ねて表示する。ロック中・未設定の時は空文字になり、代わりにLocked Indicator / Unset Indicatorの文字が表示される。")]
    public TMP_Text currentKeyLabel;
    [Tooltip("文字の後ろに敷く、キーキャップ風の背景Image。左一覧（KeyGridButton）のBackgroundと同じスプライトを使い回せばよい - キーごとに別々の画像を用意する必要はない。全てのActionBindingRowで共通の1枚でOK。")]
    public Image currentKeyBackground;
    [Tooltip("アクションがロック中の時だけ表示するオブジェクト（「ロック中」の文字を入れたLockedLabelなど）。文字のサイズを固定したいので、CurrentKeyLabelとは別のTextMeshProにする。")]
    public GameObject lockedIndicator;
    [Tooltip("キーが未設定（アクションは解放済みだが、まだ何も割り当てていない）の時だけ表示するオブジェクト（「未設定」の文字を入れたUnsetLabelなど）。CurrentKeyLabelはAuto Sizeで縮むので、固定サイズの別TextMeshProにする。")]
    public GameObject unsetIndicator;

    [Header("ドラッグ中の強調表示")]
    [Tooltip("キーをつかんでいる間だけ表示する、黒い半透明の背景（DragBackdrop）。キーのスプライトより奥（Rowの子の中で一番最初）に置き、Raycast Targetはオフにする。ロック中の行には表示されない。このオブジェクトにImageが付いていること。")]
    public GameObject dragBackdrop;
    [Tooltip("つかんだキーがキースロットの上に重なっている間の、背景の色。普段の色は、DragBackdropのImageに設定した色がそのまま使われる。")]
    public Color dragBackdropHoverColor = new Color(1f, 0.82f, 0.2f, 0.6f);
    [Tooltip("ドラッグ中、「未設定」の文字が黒い背景の上で読めるように変える色。")]
    public Color unsetTextDraggingColor = Color.white;

    [Tooltip("The Key Config screen this row belongs to.")]
    public KeyConfigUI owner;

    private bool _dragging;
    private bool _hover;
    private Image _backdropImage;
    private Color _backdropNormalColor;
    private TMP_Text _unsetText;
    private Color _unsetTextNormalColor;

    /// <summary>
    /// キーキャップ（現在のキーの背景）のRectTransform。キーを設定した時の
    /// エフェクト（KeyConfigUI.PlayBindEffect）を出す位置の基準になる。
    /// </summary>
    public RectTransform EffectAnchor =>
        currentKeyBackground != null ? currentKeyBackground.rectTransform : (RectTransform)transform;

    private void Awake()
    {
        if (actionLabel != null)
            actionLabel.text = GameActionLabel.Get(action);

        if (dragBackdrop != null)
        {
            _backdropImage = dragBackdrop.GetComponent<Image>();
            if (_backdropImage != null)
                _backdropNormalColor = _backdropImage.color;
        }

        if (unsetIndicator != null)
        {
            _unsetText = unsetIndicator.GetComponentInChildren<TMP_Text>(true);
            if (_unsetText != null)
                _unsetTextNormalColor = _unsetText.color;
        }
    }

    private void OnEnable()
    {
        KeyDragState.Changed += OnDragStateChanged;
        _dragging = KeyDragState.IsDragging;
        _hover = false;
        UpdateDragHighlight();
    }

    private void OnDisable()
    {
        KeyDragState.Changed -= OnDragStateChanged;
        _dragging = false;
        _hover = false;
    }

    private void OnDragStateChanged(bool dragging)
    {
        _dragging = dragging;
        // ドラッグが終わった時は、ポインターが重なっていた状態も必ずリセットする
        // （キースロットの上で離すと、OnPointerExitが呼ばれないことがあるため）。
        if (!dragging)
            _hover = false;
        UpdateDragHighlight();
    }

    /// <summary>
    /// KeyDropSlotから呼ばれる。つかんだキーがキースロットの上に重なった／離れた。
    /// </summary>
    public void SetDropHover(bool hover)
    {
        _hover = hover;
        UpdateDragHighlight();
    }

    /// <summary>
    /// キーをつかんでいて、かつこの行のアクションが解放済みの時だけ、
    /// 黒い半透明の背景を出す（ロック中の行にはドロップできないので出さない）。
    /// さらに、ポインターがキースロットの上に重なっている間は、背景の色を変える。
    /// </summary>
    private void UpdateDragHighlight()
    {
        var mgr = KeyBindingManager.Instance;
        bool show = _dragging && mgr != null && mgr.IsActionUnlocked(action);

        if (dragBackdrop != null)
            dragBackdrop.SetActive(show);

        if (_backdropImage != null)
            _backdropImage.color = (show && _hover) ? dragBackdropHoverColor : _backdropNormalColor;

        if (_unsetText != null)
            _unsetText.color = show ? unsetTextDraggingColor : _unsetTextNormalColor;
    }

    /// <summary>
    /// 左のキー一覧からドラッグしてきたKeyGridButtonUIが、この行のキースロット
    /// （KeyDropSlot）にドロップされた時に、KeyDropSlotから呼ばれる。
    /// </summary>
    public void HandleDrop(PointerEventData eventData)
    {
        if (owner == null || eventData.pointerDrag == null)
            return;

        var dragged = eventData.pointerDrag.GetComponent<KeyGridButtonUI>();
        if (dragged == null || dragged.Key == null)
            return;

        owner.TryBindByDrag(action, dragged.Key);
    }

    /// <summary>
    /// 行のどこか（キースロットも含む）をクリックした時に呼ばれる。
    /// 右下のプレビュー欄で、このアクションの動作を再生させる。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null)
            owner.PlayActionPreview(action);
    }

    /// <summary>Called by KeyConfigUI.RefreshAll() whenever bindings/locks change.</summary>
    public void Refresh()
    {
        var mgr = KeyBindingManager.Instance;
        if (mgr == null)
            return;

        bool unlocked = mgr.IsActionUnlocked(action);
        var key = mgr.GetBinding(action);
        bool hasKey = unlocked && key != null;

        // 「ロック中」「未設定」は、それぞれ固定サイズの別TextMeshProに任せる。
        // CurrentKeyLabelにはキーが割り当たっている時だけ文字を入れる
        // （Auto Sizeで枠に収まるまで縮むので、長い文字を入れると小さくなってしまうため）。
        if (currentKeyLabel != null)
            currentKeyLabel.text = hasKey ? key.displayName : "";

        // キーキャップの背景は、実際にキーが割り当たっている時だけ表示する。
        if (currentKeyBackground != null)
            currentKeyBackground.enabled = hasKey;

        if (lockedIndicator != null)
            lockedIndicator.SetActive(!unlocked);

        if (unsetIndicator != null)
            unsetIndicator.SetActive(unlocked && key == null);

        // ロックが解けた直後などにも、ドラッグ中の強調表示が正しくなるように更新する。
        UpdateDragHighlight();
    }
}