using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// KEY ESCAPE - 「現在の設定」の行の中で、キーをドロップできる場所（キースロット）。
/// キーのスプライトの位置（キー未設定の時は、その同じ位置）に、透明なImageと一緒に置く。
/// ドロップとクリックを、所属するActionBindingRowに転送する。
/// キーをつかんだままポインターがここに重なっている間は、行に伝えて、
/// 黒い半透明の背景（DragBackdrop）の色を変えさせる。
/// このオブジェクトはRowの子の中で一番最後（一番手前）に置くこと。他のUIより
/// 下にあると、ドロップやポインターの出入りがここに届かない。
/// </summary>
public class KeyDropSlot : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("空欄でよい。空なら、親をたどって自動で見つける。")]
    public ActionBindingRow row;

    private void Awake()
    {
        if (row == null)
            row = GetComponentInParent<ActionBindingRow>();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (row != null)
            row.HandleDrop(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (row != null)
            row.OnPointerClick(eventData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // キーをつかんでいる時だけ反応する（ふつうにマウスを乗せただけでは何も変えない）。
        if (row != null && KeyDragState.IsDragging)
            row.SetDropHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (row != null)
            row.SetDropHover(false);
    }
}