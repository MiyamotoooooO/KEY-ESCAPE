using UnityEngine;

/// <summary>
/// Data describing one physical keyboard key the player can own and assign
/// to an action - the "key" side of the spec's key/action split (Ver.0.1 §7,
/// Ver.0.2 §33). Create one asset per key via
/// Assets > Create > Key Escape > Key Definition.
///
/// 旧KeyAttribute（移動／攻撃の属性ボーナス）は廃止し、personality（性格）に
/// 完全に置き換えた（性格仕様書.md）。ランクと性格は必ずセットで効果が決まる
/// 点は旧システムと同じだが、GetPersonalityEffect()が返すのは単純な数値
/// ボーナスだけではなく、入力失敗率・操作性低下・最大HP減少・条件付き強化・
/// 確率発動なども含む一式（PersonalityEffect参照）- 実際の計算は
/// KeyPersonalityDataに集約している。
/// </summary>
[CreateAssetMenu(menuName = "Key Escape/Key Definition", fileName = "Key_")]
public class KeyDefinition : ScriptableObject
{
    [Tooltip("The physical keyboard key this represents.")]
    public KeyCode keyCode = KeyCode.A;

    [Tooltip("Label shown in the Key Config UI, e.g. \"W\", \"Space\", \"Ctrl\".")]
    public string displayName = "A";

    public KeyRank rank = KeyRank.One;

    [Tooltip("このキーの性格（性格仕様書.md参照）。isSpecialKeyがtrueの場合は" +
             "常にSerious（まじめ）として扱われる - OnValidate()がInspector上でも" +
             "自動的にSeriousへ戻す。")]
    public KeyPersonality personality = KeyPersonality.Serious;

    [Tooltip("Special keys (Ctrl/Shift/Alt) are modifiers, not standalone actions. " +
             "性格仕様書の方針により、特殊キーは常にまじめ（Serious）として扱い、" +
             "性格による特殊効果は一切持たない。")]
    public bool isSpecialKey = false;

    [TextArea]
    public string flavorText;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 特殊キーは常にまじめ扱い（性格仕様書の方針）。Inspectorで誤って
        // 別の性格を設定してしまっても、ここで自動的にSeriousへ戻す。
        if (isSpecialKey)
            personality = KeyPersonality.Serious;
    }
#endif

    /// <summary>
    /// このキーを assignedAction に割り当てた場合の性格効果一式
    /// （性格仕様書2章：性格はキーに付属するが、効果の出方は設定先アクション
    /// によって変わる）。特殊キーは常にPersonalityEffect.None。
    /// </summary>
    public PersonalityEffect GetPersonalityEffect(GameAction assignedAction)
    {
        if (isSpecialKey)
            return PersonalityEffect.None;

        return KeyPersonalityData.GetEffect(personality, rank, assignedAction);
    }
}
