/// <summary>
/// KEY ESCAPE - Japanese display names for GameAction, shared by the Key
/// Config UI scripts. Kept in one place so the wording only needs to change
/// in one spot (仕様書 uses these exact terms: 左移動/右移動/ジャンプ/攻撃/
/// ダッシュ/遠距離攻撃).
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
