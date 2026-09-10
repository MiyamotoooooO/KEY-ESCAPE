    /// <summary>
/// Every action the player can assign a keyboard key to.
/// Ver.0.2 scope: only these six exist in Chapter 1. New actions unlocked
/// later in the story should be added here as the design calls for them.
///
/// MoveLeft / MoveRight / Jump / Attack are unlocked from the very start of
/// the game (see KeyBindingManager's default bindings). Dash and
/// RangedAttack start locked and are unlocked via
/// KeyBindingManager.UnlockAction(...) when the player earns them
/// (stage-clear reward for Dash, Stage 7 boss defeat for RangedAttack -
/// spec Ver.0.2 §12).
///
/// Double jump and wall jump are NOT separate entries here: per the spec
/// they piggyback on the Jump key rather than needing their own binding,
/// so they are tracked as ability unlock flags directly on
/// CharacterController2D (hasDoubleJumpAbility / hasWallJumpAbility).
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
