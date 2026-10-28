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
public class ActionBindingRow : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
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
    private KeyCageGraphic _cage;

    // 「現在の設定」のキーを、つかんで動かしている間の状態
    private bool _isDragSource;
    private RectTransform _dragIcon;
    private Color _capNormalColor = Color.white;
    private Color _labelNormalColor = Color.white;

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
        KeySeal.Changed += OnSealChanged;
        _dragging = KeyDragState.IsDragging;
        _hover = false;
        UpdateDragHighlight();
    }

    private void OnDisable()
    {
        KeyDragState.Changed -= OnDragStateChanged;
        KeySeal.Changed -= OnSealChanged;
        _dragging = false;
        _hover = false;
        // キーをつかんだまま画面が閉じられても、アイコンや薄い表示が残らないようにする
        if (_isDragSource)
            EndRowDrag();
    }

    private void OnSealChanged()
    {
        Refresh();
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
        bool show = _dragging && mgr != null && mgr.IsActionUnlocked(action) && !IsForcedLocked();

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

        // 「現在の設定」の別の行から、ドラッグしてきたとき：キーを、この行へ動かす（キーがあれば、入れ替える）
        var fromRow = eventData.pointerDrag.GetComponent<ActionBindingRow>();
        if (fromRow != null)
        {
            if (fromRow != this)
                owner.TryMoveBinding(fromRow.action, action);
            return;
        }

        var dragged = eventData.pointerDrag.GetComponent<KeyGridButtonUI>();
        if (dragged == null || dragged.Key == null)
            return;

        owner.TryBindByDrag(action, dragged.Key);
    }

    // ---- 「現在の設定」のキーを、つかんで、別の行へ動かす --------------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragSource = false;

        var mgr = KeyBindingManager.Instance;
        if (owner == null || mgr == null || owner.IsInteractionLocked)
            return;
        if (!mgr.IsActionUnlocked(action) || IsForcedLocked())
            return;

        KeyDefinition key = mgr.GetBinding(action);
        if (key == null || KeySeal.IsSealed(key))
            return;   // キーが無い行・檻に封じられたキーは、つかめない

        // キーキャップの上を押したときだけ、つかめる（行のほかの場所では、つかまない）
        RectTransform cap = EffectAnchor;
        if (!RectTransformUtility.RectangleContainsScreenPoint(cap, eventData.pressPosition, eventData.pressEventCamera))
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        canvas = canvas.rootCanvas;

        _isDragSource = true;
        KeyDragState.Set(true);   // ほかの行に、ドロップ先の黒い背景を出す

        // 指・マウスについてくるアイコン（左の一覧からつかんだときと、同じ作り）
        var go = new GameObject("DragKeyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var rt = (RectTransform)go.transform;
        Vector2 size = cap.rect.size;
        if (size.x < 8f || size.y < 8f)
            size = new Vector2(90f, 90f);
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.sprite = currentKeyBackground != null ? currentKeyBackground.sprite : null;
        img.color = new Color(1f, 1f, 1f, 0.85f);
        img.raycastTarget = false;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = key.displayName;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (currentKeyLabel != null)
        {
            tmp.font = currentKeyLabel.font;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 8f;
            tmp.fontSizeMax = Mathf.Max(12f, currentKeyLabel.fontSize);
            tmp.color = currentKeyLabel.color;
        }

        _dragIcon = rt;
        _dragIcon.position = eventData.position;

        // 元の場所のキーは、薄くして、「いま動かしている」ことを見せる
        if (currentKeyBackground != null)
        {
            _capNormalColor = currentKeyBackground.color;
            currentKeyBackground.color = new Color(_capNormalColor.r, _capNormalColor.g, _capNormalColor.b, 0.35f);
        }
        if (currentKeyLabel != null)
        {
            _labelNormalColor = currentKeyLabel.color;
            currentKeyLabel.color = new Color(_labelNormalColor.r, _labelNormalColor.g, _labelNormalColor.b, 0.35f);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_isDragSource && _dragIcon != null)
            _dragIcon.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // ドロップしたときも、しないで離したときも、必ず呼ばれる
        if (_isDragSource)
            EndRowDrag();
    }

    private void EndRowDrag()
    {
        _isDragSource = false;
        KeyDragState.Set(false);

        if (_dragIcon != null)
            Destroy(_dragIcon.gameObject);
        _dragIcon = null;

        // 薄くした元のキーを、もとの色に戻す
        if (currentKeyBackground != null)
            currentKeyBackground.color = _capNormalColor;
        if (currentKeyLabel != null)
            currentKeyLabel.color = _labelNormalColor;
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

    /// <summary>キーキャップの位置に、檻をかぶせる／はずす。</summary>
    private void UpdateSealCage(bool sealedNow, KeyDefinition key)
    {
        if (!sealedNow)
        {
            if (_cage != null)
                _cage.gameObject.SetActive(false);
            return;
        }

        RectTransform bg = currentKeyBackground != null ? currentKeyBackground.rectTransform : null;
        RectTransform host = (bg != null && bg.parent is RectTransform) ? (RectTransform)bg.parent : (RectTransform)transform;

        if (_cage == null)
            _cage = KeyCageGraphic.Attach(host);

        RectTransform cr = (RectTransform)_cage.transform;
        if (bg != null && cr.parent == bg.parent)
        {
            // キーキャップと同じ位置・大きさに合わせる
            cr.anchorMin = bg.anchorMin;
            cr.anchorMax = bg.anchorMax;
            cr.pivot = bg.pivot;
            cr.anchoredPosition = bg.anchoredPosition;
            cr.sizeDelta = bg.sizeDelta;
        }

        _cage.Bind(key);
        _cage.gameObject.SetActive(true);
        cr.SetAsLastSibling();
    }

    /// <summary>ロックを壊す演出のために、この行を「ロック中」の見た目にしている最中か。</summary>
    private bool IsForcedLocked()
    {
        return owner != null && owner.IsForcedLocked(action);
    }

    /// <summary>Called by KeyConfigUI.RefreshAll() whenever bindings/locks change.</summary>
    public void Refresh()
    {
        var mgr = KeyBindingManager.Instance;
        if (mgr == null)
            return;

        // ロックを壊す演出の最中は、解放済みでも、ロック中の見た目にしておく。
        bool unlocked = mgr.IsActionUnlocked(action) && !IsForcedLocked();
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

        // ボスの檻に封じられているキーには、檻をかぶせる（別のキーを設定すれば、使えるようになる）。
        UpdateSealCage(hasKey && KeySeal.IsSealed(key), key);

        // ロックが解けた直後などにも、ドラッグ中の強調表示が正しくなるように更新する。
        UpdateDragHighlight();
    }
}