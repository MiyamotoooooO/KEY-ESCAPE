/// <summary>
/// KEY ESCAPE - 性格・ランク・設定先アクションの組み合わせ1つに対する効果一式。
///
/// 性格ごとに使うフィールドだけ0以外の値になる（性格仕様書12章ルール③：
/// 単純な「攻撃力+○%」だけにしない）。どのフィールドを実際に読んで何をする
/// かはゲームプレイ側（攻撃・移動・入力処理・HP管理など）の役目で、この構造体
/// 自体は「今のキー配置で何が起きるべきか」を1か所にまとめて計算するためだけ
/// のデータ。
/// </summary>
public struct PersonalityEffect
{
    /// <summary>設定先アクションの能力値上昇率（0.15なら+15%）。常時発動する
    /// 性格（ぶきよう／らんぼう／非情）ではそのまま使う値。条件発動
    /// （おくびょう＝危険時のみ）や確率発動（きまぐれ）の性格では、
    /// 「発動した場合の」上昇率として使う。</summary>
    public float statBonus;

    /// <summary>入力そのものが不発になる確率（ぶきよう）。0=不発なし。</summary>
    public float failureChance;

    /// <summary>操作性が低下する度合い（らんぼう）。慣性が強くなる／精度が落ちる
    /// など、実際にどう表現するかは割り当てたアクションのコントローラー側の
    /// 解釈次第。0=操作性への影響なし。</summary>
    public float controlPenalty;

    /// <summary>最大HPをこの量だけ減らす（非情。攻撃系アクションに設定した
    /// 場合のみ0以外になる）。</summary>
    public float maxHpReduction;

    /// <summary>この距離以内に敵がいるとstatBonusが発動する（おくびょう）。
    /// 0=条件発動の性格ではない。</summary>
    public float dangerTriggerRange;

    /// <summary>statBonusが発動する確率（きまぐれ）。0=確率発動の性格ではない。</summary>
    public float procChance;

    /// <summary>非情を攻撃系以外のアクションに設定した場合のように、性格の
    /// 恩恵がそもそも乗らない組み合わせだったかどうか。true の場合、他の
    /// フィールドは全て0のまま（性格仕様書7章の例外ルール）。</summary>
    public bool noBenefitForThisAction;

    /// <summary>何の効果も持たない状態（まじめ、または未設定時）。</summary>
    public static readonly PersonalityEffect None = new PersonalityEffect();
}
