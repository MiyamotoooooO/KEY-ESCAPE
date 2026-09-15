using UnityEngine;

/// <summary>
/// プレイヤーが所有し、アクションに割り当てられる、物理的なキーボードキー1つ分の
/// データ。仕様上の key/action の分離（Ver.0.1 §7、Ver.0.2 §33）における「key」側。
/// Assets > Create > Key Escape > Key Definition からキー1つにつき1アセット作成する。
///
/// ランクと属性は常にセットで意味を持つ（Ver.0.2 §9-1：強いキーが何でも強いわけ
/// ではない）- GetAttributeBonus() がこのルールの唯一の実装箇所なので、
/// ゲームプレイ側のコードで同じロジックを再実装する必要はない。
/// </summary>
[CreateAssetMenu(menuName = "Key Escape/Key Definition", fileName = "Key_")]
public class KeyDefinition : ScriptableObject
{
    [Tooltip("The physical keyboard key this represents.")]
    public KeyCode keyCode = KeyCode.A;

    [Tooltip("Label shown in the Key Config UI, e.g. \"W\", \"Space\", \"Ctrl\".")]
    public string displayName = "A";

    public KeyRank rank = KeyRank.One;

    [Tooltip("Chapter 1 only: Movement or Attack. Leave as None for a key with no attribute bonus.")]
    public KeyAttribute attribute = KeyAttribute.None;

    [Tooltip("Special keys (Ctrl/Shift/Alt) are modifiers, not standalone actions, and never carry " +
             "a rank/attribute bonus of their own (spec Ver.0.2 confirmed decision, §10).")]
    public bool isSpecialKey = false;

    [TextArea]
    public string flavorText;

    /// <summary>
    /// Ver.0.2 §9: rank/attribute bonus applied when this key is bound to an
    /// action whose required attribute it matches. Returns 0 for a mismatch,
    /// for a special key, or for a Rank One key (Rank One = usable, no bonus).
    /// </summary>
    public float GetAttributeBonus(KeyAttribute requiredAttribute)
    {
        if (isSpecialKey || attribute == KeyAttribute.None || attribute != requiredAttribute)
            return 0f;

        switch (rank)
        {
            case KeyRank.Two:
                return 0.05f;
            case KeyRank.Three:
                return 0.10f;
            default:
                return 0f; // Rank One
        }
    }
}
