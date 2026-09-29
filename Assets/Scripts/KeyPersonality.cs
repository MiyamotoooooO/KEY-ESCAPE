using UnityEngine;

/// <summary>
/// KEY ESCAPE - キーに設定する「性格」（性格仕様書.md 4章）。
///
/// 旧KeyAttribute（移動／攻撃の属性ボーナス）を完全に置き換えるシステム。
/// 旧システムは「攻撃属性のキーは攻撃に置くと強い」という単純な型はめだった
/// ため、結局プレイヤーが最適な配置に収束してしまう問題があった。性格は
/// 単純な能力値上昇だけでなく、入力失敗／操作性低下／HP減少／条件付き強化／
/// ランダム強化のように「置き方そのものを考えさせる」効果を持つ
/// （性格仕様書12章ルール③・④）。
///
/// 各性格の具体的な効果はKeyPersonalityData.GetEffect()にまとめている。
///
/// [InspectorName]を付けているのは見た目（Inspectorのドロップダウン表示）だけの
/// ためで、コード上の識別子（Serious等）やシリアライズされる実際の値には
/// 影響しない - 保存済みのKeyDefinitionアセットの値もそのまま有効。
/// </summary>
public enum KeyPersonality
{
    /// <summary>まじめ - 特殊効果なし。特殊キー（isSpecialKey）は常にこれになる。</summary>
    [InspectorName("まじめ")]
    Serious,
    /// <summary>ぶきよう - 設定アクションの能力上昇＋一定確率で入力失敗。</summary>
    [InspectorName("ぶきよう")]
    Clumsy,
    /// <summary>非情 - 攻撃系アクションに設定した時だけ、攻撃性能を大幅強化＋最大HP減少。ランクが上がるほど両方増加する唯一の性格。</summary>
    [InspectorName("非情")]
    Ruthless,
    /// <summary>らんぼう - 設定アクションの出力上昇＋操作性低下。</summary>
    [InspectorName("らんぼう")]
    Rough,
    /// <summary>おくびょう - 危険（敵接近）時に設定アクションが強化される。</summary>
    [InspectorName("おくびょう")]
    Cowardly,
    /// <summary>きまぐれ - 一定確率で設定アクションが大幅強化される。</summary>
    [InspectorName("きまぐれ")]
    Fickle,
}

/// <summary>KeyPersonalityの日本語表示名。Key Config画面などで使う。</summary>
public static class KeyPersonalityLabel
{
    public static string Get(KeyPersonality personality)
    {
        switch (personality)
        {
            case KeyPersonality.Serious: return "まじめ";
            case KeyPersonality.Clumsy: return "ぶきよう";
            case KeyPersonality.Ruthless: return "非情";
            case KeyPersonality.Rough: return "らんぼう";
            case KeyPersonality.Cowardly: return "おくびょう";
            case KeyPersonality.Fickle: return "きまぐれ";
            default: return personality.ToString();
        }
    }
}
