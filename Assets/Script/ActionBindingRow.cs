using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - one row of the Key Config screen, representing a single
/// GameAction (spec Ver.0.1 §49 / Ver.0.2 §20). Chapter 1 has exactly 6
/// bindable actions, so this is meant to be placed 6 times by hand in the
/// scene (one per GameAction) rather than instantiated from a prefab - see
/// シーン構築ガイド.md.
/// </summary>
public class ActionBindingRow : MonoBehaviour
{
    [Tooltip("Which action this row represents.")]
    public GameAction action;

    [Header("UI references")]
    public TMP_Text actionLabel;
    public TMP_Text currentKeyLabel;
    public Button changeButton;
    [Tooltip("Optional - shown while the action is still locked (e.g. a padlock icon).")]
    public GameObject lockedIndicator;

    [Tooltip("The Key Config screen this row belongs to.")]
    public KeyConfigUI owner;

    private void Awake()
    {
        if (actionLabel != null)
            actionLabel.text = GameActionLabel.Get(action);

        if (changeButton != null)
            changeButton.onClick.AddListener(() =>
            {
                if (owner != null)
                    owner.BeginRebind(action);
            });
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

        if (changeButton != null)
            changeButton.interactable = unlocked;

        if (lockedIndicator != null)
            lockedIndicator.SetActive(!unlocked);
    }
}
