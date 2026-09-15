using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - Key Config画面の「キーを選ぶ」リストにあるボタン1つ分。
/// プレイヤーが所有しているキー（KeyInventory.OwnedKeys）ごとに
/// KeyConfigUI.RefreshKeyList()からInstantiateされる - ActionBindingRowとは
/// 違い、こちらは本当にプレハブ化されている。所有するキーの数はプレイが
/// 進むにつれて増えていくため。
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
