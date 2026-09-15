/// <summary>
/// KEY ESCAPE - GameActionの日本語表示名。Key Config UI系のスクリプトで共有する。
/// 表記を直す箇所が1か所で済むようここにまとめてある
/// （仕様書ではこの表記で統一：左移動/右移動/ジャンプ/攻撃/
/// ダッシュ/遠距離攻撃）。
/// </summary>
public static class GameActionLabel
{
    public static string Get(GameAction action)
    {
        switch (action)
        {
            case GameAction.MoveLeft: return "左移動";
            case GameAction.MoveRight: return "右移動";
            case GameAction.Jump: return "ジャンプ";
            case GameAction.Attack: return "攻撃";
            case GameAction.Dash: return "ダッシュ";
            case GameAction.RangedAttack: return "遠距離攻撃";
            default: return action.ToString();
        }
    }
}
