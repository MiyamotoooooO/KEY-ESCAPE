using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// KEY ESCAPE - Key Config画面左側のキー一覧に並ぶ1マス分のボタン。
/// KeyConfigUI.RefreshKeyList()が、ゲーム内の全キー（KeyInventory.AllKeyDefinitions）
/// を対象にタブで絞り込んだ分だけ、これをInstantiateして並べる。
///
/// 所持済みキー：文字を表示し、クリックで右のキー情報パネルを更新、
///   ドラッグして中央のActionBindingRowのキースロット（KeyDropSlot）にドロップ
///   すると割り当てられる。ドラッグ中は、KeyDragStateを通じて、各行のドロップ先に
///   黒い半透明の背景を出す。
/// 封じられたキー（ボスの檻。KeySeal）：檻の絵がかぶさり、のこり秒数が出る。クリックもドラッグもできない
///   （解けると、もとにもどる）。
/// 未入手キー（Owned=false）：文字を「？」に隠し、lockedOverlay（鍵アイコンなど）
///   を表示する。クリックもドラッグも一切受け付けない - 何のキーかは
///   実際に入手するまでのお楽しみ。
/// </summary>
public class KeyGridButtonUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI参照")]
    public Image background;
    public TMP_Text nameLabel;
    [Tooltip("未入手キーの時だけ表示する。鍵アイコンや暗いオーバーレイなど。")]
    public GameObject lockedOverlay;

    [Header("所持状態による色分け（背景Imageがある場合のみ使用）")]
    public Color ownedColor = Color.white;
    public Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    public KeyDefinition Key { get; private set; }
    public bool Owned { get; private set; }

    private KeyConfigUI _owner;
    private Canvas _rootCanvas;
    private RectTransform _dragIcon;
    private KeyCageGraphic _cage;
    private Color _nameColor = Color.white;
    private bool _nameColorKnown;
    private GameObject _usedBadge;

    /// <summary>ボスの檻に封じられているか。</summary>
    public bool Sealed
    {
        get { return Owned && Key != null && KeySeal.IsSealed(Key); }
    }

    /// <summary>KeyConfigUI.RefreshKeyList()がInstantiate直後に呼ぶ。</summary>
    public void Setup(KeyDefinition key, bool owned, KeyConfigUI owner)
    {
        Key = key;
        Owned = owned;
        _owner = owner;
        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas != null)
            _rootCanvas = _rootCanvas.rootCanvas;

        if (nameLabel != null)
            nameLabel.text = owned ? (key != null ? key.displayName : "?") : "?";

        if (lockedOverlay != null)
            lockedOverlay.SetActive(!owned);

        if (background != null)
            background.color = owned ? ownedColor : lockedColor;

        ApplySealVisual();
        RefreshUsed();
    }

    private void OnEnable()
    {
        KeySeal.Changed += ApplySealVisual;
        ApplySealVisual();
    }

    /// <summary>封じられているなら、檻をかぶせる。解けたら、はずす。</summary>
    private void ApplySealVisual()
    {
        bool s = Sealed;

        if (nameLabel != null)
        {
            if (!_nameColorKnown)
            {
                _nameColor = nameLabel.color;
                _nameColorKnown = true;
            }
            nameLabel.color = s ? new Color(_nameColor.r * 0.6f, _nameColor.g * 0.6f, _nameColor.b * 0.6f, _nameColor.a) : _nameColor;
        }

        if (s)
        {
            if (_cage == null)
                _cage = KeyCageGraphic.Attach((RectTransform)transform);
            _cage.Bind(Key);
            _cage.gameObject.SetActive(true);
            _cage.transform.SetAsLastSibling();
        }
        else if (_cage != null)
        {
            _cage.gameObject.SetActive(false);
        }

        RefreshUsed();
    }

    // ---- 「使用中」の札：いま「現在の設定」に使っているキーに、つける --------------------

    /// <summary>このキーが、いまアクションに割り当てられているか（檻に封じられているときは、札を出さない）。</summary>
    public bool InUse
    {
        get
        {
            KeyBindingManager mgr = KeyBindingManager.Instance;
            return Owned && Key != null && !Sealed && mgr != null && mgr.TryGetActionForKey(Key, out GameAction _);
        }
    }

    /// <summary>「使用中」の札を、出す／消す。割り当てが変わるたびに、KeyConfigUIから呼ばれる（タイルは作り直さない）。</summary>
    public void RefreshUsed()
    {
        if (!InUse)
        {
            if (_usedBadge != null)
                _usedBadge.SetActive(false);
            return;
        }

        if (_usedBadge == null)
            _usedBadge = CreateUsedBadge();
        if (_usedBadge != null)
        {
            _usedBadge.SetActive(true);
            _usedBadge.transform.SetAsLastSibling();
            // 檻がかぶさっているなら、札はその下に残す（封じられているときは、そもそも出さない）
        }
    }

    // 札を、コードで作る（プレハブを直さなくてよいように）。キーの下の部分に、小さな黒い札と「使用中」の文字を重ねる。
    private GameObject CreateUsedBadge()
    {
        var go = new GameObject("UsedBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.08f, 0.05f);
        rt.anchorMax = new Vector2(0.92f, 0.30f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        img.raycastTarget = false;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var tr = (RectTransform)textGo.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(2f, 0f);
        tr.offsetMax = new Vector2(-2f, 0f);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "使用中";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.86f, 0.3f, 1f);
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 6f;
        tmp.fontSizeMax = 40f;
        if (nameLabel != null)
            tmp.font = nameLabel.font;
        return go;
    }

    // ---- クリック：右のキー情報パネルを更新 ---------------------------------

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Owned || Key == null || _owner == null)
            return; // 未入手キーはクリックしても何も起きない（正体は伏せたまま）
        if (Sealed)
            return; // 封じられているキーは、解けるまで、選べない

        _owner.SelectKey(Key);
    }

    // ---- ドラッグ：中央のActionBindingRowへドロップして割り当て ----------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!Owned || Key == null || _rootCanvas == null)
            return;
        if (Sealed)
            return; // 封じられているキーは、解けるまで、つかめない

        // キーをつかんだことを全体に知らせる。各ActionBindingRowが、これを受けて、
        // ドロップ先（キースロット）の後ろに黒い半透明の背景を出す。
        KeyDragState.Set(true);

        // ドラッグ中、指/マウスに追従する簡易アイコンをその場で生成する
        // （専用プレハブを用意しなくても動くように、コードでImage+TMP_Textを
        // 組み立てている）。eventData.pointerDragは自動的にこのボタン自身の
        // GameObjectのままなので、ActionBindingRow.HandleDrop()側でKeyGridButtonUIを
        // 正しく取得できる。
        var go = new GameObject("DragKeyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_rootCanvas.transform, false);
        go.transform.SetAsLastSibling();

        var rt = (RectTransform)go.transform;
        Vector2 size = (background != null) ? background.rectTransform.sizeDelta : new Vector2(90f, 90f);
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.sprite = background != null ? background.sprite : null;
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
        tmp.text = Key.displayName;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (nameLabel != null)
        {
            tmp.font = nameLabel.font;
            tmp.fontSize = nameLabel.fontSize;
            tmp.color = nameLabel.color;
        }

        _dragIcon = rt;
        _dragIcon.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIcon != null)
            _dragIcon.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // ドロップに成功した時も、ドロップせずに離した時も、ここが必ず呼ばれるので、
        // ドロップ先の黒い半透明の背景は、ここで確実に消える。
        KeyDragState.Set(false);

        if (_dragIcon != null)
            Destroy(_dragIcon.gameObject);
        _dragIcon = null;
    }

    private void OnDisable()
    {
        KeySeal.Changed -= ApplySealVisual;

        // ドラッグ中に画面が閉じられて（非表示になって）も、OnEndDragは呼ばれない。
        // ドラッグアイコンが残らないよう消し、ドロップ先の背景も出たままにならないようにする。
        if (_dragIcon != null)
        {
            Destroy(_dragIcon.gameObject);
            _dragIcon = null;
            KeyDragState.Set(false);
        }
    }

    private void OnDestroy()
    {
        // ドラッグ中にこのタイルが破棄された（キー一覧が作り直された、画面が閉じられた等）
        // 場合は、OnEndDragが呼ばれないことがある。ドラッグアイコンが残らないように
        // 消し、ドロップ先の背景も出たままにならないようにする。
        if (_dragIcon != null)
        {
            Destroy(_dragIcon.gameObject);
            _dragIcon = null;
            KeyDragState.Set(false);
        }
    }
}