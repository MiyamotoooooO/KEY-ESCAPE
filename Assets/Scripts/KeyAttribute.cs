/// <summary>
/// 仕様書Ver.0.2 §9：Chapter 1で使うのはMovementとAttackのみ。防御/回避などは
/// 対応するアクション自体が解放されてから追加する
/// （Ver.0.2 §24 - Chapter 2以降に持ち越し）。
/// </summary>
public enum KeyAttribute
{
    None,
    Movement,
    Attack,
}
