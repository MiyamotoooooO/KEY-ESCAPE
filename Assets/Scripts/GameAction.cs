/// <summary>
/// プレイヤーがキーボードのキーを割り当てられる、すべてのアクション。
/// Ver.0.2時点の範囲：Chapter 1に存在するのはこの6つのみ。物語の後半で
/// 新しいアクションが解放される場合は、デザイン側の必要に応じてここに追加すること。
///
/// MoveLeft / MoveRight / Jump / Attack はゲーム開始時点から解放済み
/// （KeyBindingManagerのデフォルト割り当てを参照）。DashとRangedAttackは
/// 最初はロックされていて、プレイヤーが獲得したタイミングで
/// KeyBindingManager.UnlockAction(...) を通じて解放される
/// （Dashはステージクリア報酬、RangedAttackはStage 7のボス撃破 -
/// 仕様書Ver.0.2 §12）。
///
/// 二段ジャンプとウォールジャンプはここに独立した項目として存在しない：
/// 仕様上、専用の割り当てを必要とせずJumpキーに相乗りする形になっているため、
/// CharacterController2D上のアビリティ解放フラグ
/// （hasDoubleJumpAbility / hasWallJumpAbility）として直接管理している。
/// </summary>
public enum GameAction
{
    MoveLeft,
    MoveRight,
    Jump,
    Attack,
    Dash,
    RangedAttack,
}
