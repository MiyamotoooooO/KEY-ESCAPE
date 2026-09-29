/// <summary>
/// KEY ESCAPE - 性格・ランクの組み合わせから実際のPersonalityEffectを計算する
/// 場所。性格ごとの数値テーブルをここに集約しておくことで、KeyDefinition側は
/// 「性格＋ランク＋設定先アクション」を渡すだけでよく、数値のチューニングは
/// 常にこの1ファイルだけで完結する。
///
/// 数値について：性格仕様書.md 6章（ぶきよう）・10章（きまぐれ）の「仮仕様」
/// 表はそのまま使用。7章（非情）・8章（らんぼう）・9章（おくびょう）は
/// 「高ランクほど～という方向」までしか決まっておらず具体的な数値が未確定の
/// ため、その方向性だけ守った仮数値を置いている（コメントに"仮数値"と明記）。
/// 実際の数値は今後プレイテストで調整すること。
/// </summary>
public static class KeyPersonalityData
{
    public static PersonalityEffect GetEffect(KeyPersonality personality, KeyRank rank, GameAction assignedAction)
    {
        switch (personality)
        {
            case KeyPersonality.Clumsy:
                return GetClumsy(rank);
            case KeyPersonality.Ruthless:
                return GetRuthless(rank, assignedAction);
            case KeyPersonality.Rough:
                return GetRough(rank);
            case KeyPersonality.Cowardly:
                return GetCowardly(rank);
            case KeyPersonality.Fickle:
                return GetFickle(rank);
            case KeyPersonality.Serious:
            default:
                return PersonalityEffect.None;
        }
    }

    /// <summary>ぶきよう - 性格仕様書6章の仮仕様表そのまま。</summary>
    private static PersonalityEffect GetClumsy(KeyRank rank)
    {
        switch (rank)
        {
            case KeyRank.Two:
                return new PersonalityEffect { statBonus = 0.10f, failureChance = 0.10f };
            case KeyRank.Three:
                return new PersonalityEffect { statBonus = 0.15f, failureChance = 0.05f };
            default: // Rank One
                return new PersonalityEffect { statBonus = 0.05f, failureChance = 0.15f };
        }
    }

    /// <summary>
    /// 非情 - 性格仕様書7章。攻撃系アクション（Attack／RangedAttack）に設定
    /// した場合のみ効果が乗る（ルール④の意図的な例外）。それ以外に設定した
    /// 場合はnoBenefitForThisAction=trueを返し、他のフィールドは全て0のまま。
    /// 数値は7章「HP計算イメージ」節が未確定のため仮数値
    /// （攻撃強化・HP減少ともに、他の性格とは逆にランクが上がるほど増加する
    /// という方向だけは仕様書通り）。
    /// </summary>
    private static PersonalityEffect GetRuthless(KeyRank rank, GameAction assignedAction)
    {
        if (!GameActionCategory.IsAttackAction(assignedAction))
            return new PersonalityEffect { noBenefitForThisAction = true };

        switch (rank)
        {
            case KeyRank.Two:
                return new PersonalityEffect { statBonus = 0.30f, maxHpReduction = 10f };
            case KeyRank.Three:
                return new PersonalityEffect { statBonus = 0.45f, maxHpReduction = 18f };
            default: // Rank One
                return new PersonalityEffect { statBonus = 0.20f, maxHpReduction = 5f };
        }
    }

    /// <summary>らんぼう - 性格仕様書8章。「高ランクほど出力上昇・操作ペナルティ
    /// 軽減」の方向のみ確定しており、数値自体は仮。</summary>
    private static PersonalityEffect GetRough(KeyRank rank)
    {
        switch (rank)
        {
            case KeyRank.Two:
                return new PersonalityEffect { statBonus = 0.15f, controlPenalty = 0.15f };
            case KeyRank.Three:
                return new PersonalityEffect { statBonus = 0.20f, controlPenalty = 0.08f };
            default: // Rank One
                return new PersonalityEffect { statBonus = 0.10f, controlPenalty = 0.25f };
        }
    }

    /// <summary>おくびょう - 性格仕様書9章。「高ランクほど発動条件が広くなり、
    /// 強化量が大きくなる」の方向のみ確定しており、距離・数値は仮
    /// （メートル想定）。</summary>
    private static PersonalityEffect GetCowardly(KeyRank rank)
    {
        switch (rank)
        {
            case KeyRank.Two:
                return new PersonalityEffect { statBonus = 0.22f, dangerTriggerRange = 3.5f };
            case KeyRank.Three:
                return new PersonalityEffect { statBonus = 0.30f, dangerTriggerRange = 5f };
            default: // Rank One
                return new PersonalityEffect { statBonus = 0.15f, dangerTriggerRange = 2f };
        }
    }

    /// <summary>きまぐれ - 性格仕様書10章の仮仕様表そのまま。</summary>
    private static PersonalityEffect GetFickle(KeyRank rank)
    {
        switch (rank)
        {
            case KeyRank.Two:
                return new PersonalityEffect { statBonus = 0.75f, procChance = 0.20f };
            case KeyRank.Three:
                return new PersonalityEffect { statBonus = 1.00f, procChance = 0.30f };
            default: // Rank One
                return new PersonalityEffect { statBonus = 0.50f, procChance = 0.10f };
        }
    }

    /// <summary>
    /// Key Config画面の情報パネル用：効果を1行の日本語テキストにする。
    /// まじめ・未設定（None）は「ー」を返す。
    /// </summary>
    public static string Describe(KeyPersonality personality, PersonalityEffect effect)
    {
        if (personality == KeyPersonality.Serious)
            return "ー";

        if (effect.noBenefitForThisAction)
            return "攻撃系アクションでないため恩恵なし";

        switch (personality)
        {
            case KeyPersonality.Clumsy:
                return $"能力+{effect.statBonus * 100f:0}% ／ 入力失敗{effect.failureChance * 100f:0}%";
            case KeyPersonality.Ruthless:
                return $"攻撃+{effect.statBonus * 100f:0}% ／ 最大HP-{effect.maxHpReduction:0}";
            case KeyPersonality.Rough:
                return $"出力+{effect.statBonus * 100f:0}% ／ 操作性-{effect.controlPenalty * 100f:0}%";
            case KeyPersonality.Cowardly:
                return $"敵が距離{effect.dangerTriggerRange:0.#}以内なら+{effect.statBonus * 100f:0}%";
            case KeyPersonality.Fickle:
                return $"{effect.procChance * 100f:0}%の確率で+{effect.statBonus * 100f:0}%";
            default:
                return "ー";
        }
    }
}
