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
///   ドラッグして中央のActionBindingRowにドロップすると割り当てられる。
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
    }

    // ---- クリック：右のキー情報パネルを更新 ---------------------------------

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Owned || Key == null || _owner == null)
            return; // 未入手キーはクリックしても何も起きない（正体は伏せたまま）

        _owner.SelectKey(Key);
    }

    // ---- ドラッグ：中央のActionBindingRowへドロップして割り当て ----------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"[DEBUG] OnBeginDrag: gameObject={gameObject.name}, Owned={Owned}, Key={(Key != null ? Key.displayName : "null")}, rootCanvas={(_rootCanvas != null)}");
        if (!Owned || Key == null || _rootCanvas == null)
            return;

        // ドラッグ中、指/マウスに追従する簡易アイコンをその場で生成する
        // （専用プレハブを用意しなくても動くように、コードでImage+TMP_Textを
        // 組み立てている）。eventData.pointerDragは自動的にこのボタン自身の
        // GameObjectのままなので、ActionBindingRow.OnDrop()側でKeyGridButtonUIを
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
        Debug.Log($"[DEBUG] OnEndDrag: gameObject={gameObject.name}, hadDragIcon={(_dragIcon != null)}");
        if (_dragIcon != null)
            Destroy(_dragIcon.gameObject);
        _dragIcon = null;
    }
}