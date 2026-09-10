using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - one button in the "pick a key" list on the Key Config
/// screen. Instantiated once per key the player owns (KeyInventory.OwnedKeys)
/// by KeyConfigUI.RefreshKeyList() - this one IS a prefab, unlike
/// ActionBindingRow, because the number of owned keys grows as you play.
/// </summary>
public class OwnedKeyButtonUI : MonoBehaviour
{
    [Header("UI references")]
    public Button button;
    public TMP_Text nameLabel;
    [Tooltip("Optional - shows which action this key is currently bound to, if any.")]
    public TMP_Text usedByLabel;

    public KeyDefinition Key { get; private set; }

    /// <summary>Call right after Instantiate(). `owner` is the Key Config screen driving this list.</summary>
    public void Setup(KeyDefinition key, KeyConfigUI owner)
    {
        Key = key;

        if (nameLabel != null)
            nameLabel.text = key != null ? key.displayName : "?";

        if (usedByLabel != null)
        {
            if (key != null && KeyBindingManager.Instance != null && KeyBindingManager.Instance.TryGetActionForKey(key, out var usedBy))
                usedByLabel.text = $"現在：{GameActionLabel.Get(usedBy)}";
            else
                usedByLabel.text = "未使用";
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (owner != null)
                    owner.SelectKey(Key);
            });
        }
    }
}
