/// <summary>
/// KEY ESCAPE - GameActionをカテゴリで判定するための小さなヘルパー。
/// 現時点では「攻撃系かどうか」の判定にしか使っていない（性格仕様書7章：
/// 非情は攻撃系アクションに設定した時だけ効果が乗る、という例外ルールの
/// 判定に使う）が、今後別の性格が「移動系だけ」「ジャンプ系だけ」のような
/// 条件を持つ場合もここに追加していく想定。
/// </summary>
public static class GameActionCategory
{
    /// <summary>Attack・RangedAttackの2つを攻撃系アクションとする。</summary>
    public static bool IsAttackAction(GameAction action) =>
        action == GameAction.Attack || action == GameAction.RangedAttack;
}
