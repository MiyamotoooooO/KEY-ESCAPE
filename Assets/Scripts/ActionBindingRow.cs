using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - Key Config画面の1行。単一のGameActionを表す
/// （仕様書Ver.0.1 §49 / Ver.0.2 §20）。Chapter 1では割り当て可能な
/// アクションがちょうど6つなので、プレハブからInstantiateするのではなく
/// シーンに手作業で6個配置する想定（GameActionごとに1つ） -
/// シーン構築ガイド.md参照。
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
