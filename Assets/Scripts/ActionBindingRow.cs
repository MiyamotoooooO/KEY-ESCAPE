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
/// 新デザイン（Ver.0.3想定）ではドラッグ&ドロップで割り当てる：左のキー一覧
/// （KeyGridButtonUI）からこの行のどこかにドラッグしたキーをドロップすると
/// OnDrop()が呼ばれ、owner.TryBindByDrag(action, key)を試みる。行のどこかに
/// Raycast Target有効なImage（背景の枠など）が必要 - それが無いとUnityの
/// EventSystemがこの行を「ドロップ先」として検出できない。
/// </summary>
public class ActionBindingRow : MonoBehaviour, IDropHandler
{
    [Tooltip("Which action this row represents.")]
    public GameAction action;

    [Header("UI references")]
    public TMP_Text actionLabel;
    public TMP_Text currentKeyLabel;
    [Tooltip("Optional - shown while the action is still locked (e.g. a padlock icon).")]
    public GameObject lockedIndicator;

    [Tooltip("The Key Config screen this row belongs to.")]
    public KeyConfigUI owner;

    private void Awake()
    {
        if (actionLabel != null)
            actionLabel.text = GameActionLabel.Get(action);
    }

    /// <summary>
    /// 左のキー一覧からドラッグしてきたKeyGridButtonUIがこの行にドロップされた
    /// 時にUnityのEventSystemから自動的に呼ばれる。
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"[DEBUG] OnDrop on row {action}: owner={(owner != null)}, pointerDrag={(eventData.pointerDrag != null ? eventData.pointerDrag.name : "null")}");
        if (owner == null || eventData.pointerDrag == null)
            return;

        var dragged = eventData.pointerDrag.GetComponent<KeyGridButtonUI>();
        Debug.Log($"[DEBUG] OnDrop: dragged component found={(dragged != null)}, dragged.Key={(dragged != null && dragged.Key != null ? dragged.Key.displayName : "null")}");
        if (dragged == null || dragged.Key == null)
            return;

        owner.TryBindByDrag(action, dragged.Key);
    }

    /// <summary>Called by KeyConfigUI.RefreshAll() whenever bindings/locks change.</summary>
    public void Refresh()
    {
        var mgr = KeyBindingManager.Instance;
        if (mgr == null)
            return;

        bool unlocked = mgr.IsActionUnlocked(action);
        var key = mgr.GetBinding(action);

        if (currentKeyLabel != null)
            currentKeyLabel.text = !unlocked ? "ロック中" : (key != null ? key.displayName : "未設定");

        if (lockedIndicator != null)
            lockedIndicator.SetActive(!unlocked);
    }
}