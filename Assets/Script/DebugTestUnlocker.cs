using UnityEngine;

/// <summary>
/// TEMPORARY debug helper - not part of the final game, delete this script
/// and the GameObject it's on once you're done testing.
///
/// Purpose: Dash and RangedAttack start locked AND unbound (spec Ver.0.2 -
/// the "Default Bindings" rows for them are intentionally empty), so calling
/// KeyBindingManager.Instance.UnlockAction(...) alone does nothing you can
/// see or feel. This script also binds a key to the action, which is the
/// missing step that actually lets you test it before the real Key Config
/// UI and stage-clear reward hooks exist.
///
/// Setup:
///  1. Attach this to any GameObject in your test scene (e.g. Bootstrap).
///  2. Create two throwaway KeyDefinition assets that aren't used anywhere
///     else yet (e.g. Key_V, Key_B - Rank/Attribute don't matter for this test).
///  3. Add both to KeyInventory's "Starting Keys" list (TryBindKey fails
///     - see the Console log this script prints if that happens - if the
///     player doesn't "own" the key yet. This is easy to forget when you
///     make a new test key: creating the KeyDefinition asset and dragging
///     it into this script is NOT the same as owning it).
///  4. Drag them into the Dash Test Key / Ranged Test Key fields below.
///  5. Drag the Player (the GameObject with CharacterController2D) into
///     `playerController` below. This lets F9 also flip
///     `hasDashAbility` to true for you - that flag is a Play-mode-only
///     Inspector change if you toggle it by hand, so it silently resets to
///     false every time you stop and restart Play. Without this, a bind
///     that succeeded can still look like "dash doesn't work".
///  6. Press Play.
///
/// Controls:
///   F9  - unlocks Dash, binds it to Dash Test Key, sets
///         playerController.hasDashAbility = true, AND unlocks the Ctrl
///         modifier (so this key also confirms the Ctrl+Attack strong-attack
///         issue: if Ctrl+F starts working right after pressing F9, the
///         modifier being locked was the cause).
///   F10 - unlocks RangedAttack and binds it to Ranged Test Key.
///
/// After F9, press whatever key you assigned as Dash Test Key to actually
/// dash (watch for the IsDashing animator bool and a forward burst of
/// movement). After F10, press the RangedAttack test key to confirm a
/// throwable spawns.
/// </summary>
public class DebugTestUnlocker : MonoBehaviour
{
    [Tooltip("A KeyDefinition not already bound to anything (e.g. Key_V). Must also be in KeyInventory's Starting Keys.")]
    public KeyDefinition dashTestKey;

    [Tooltip("A second unused KeyDefinition (e.g. Key_B). Must also be in KeyInventory's Starting Keys.")]
    public KeyDefinition rangedTestKey;

    [Tooltip("The Player's CharacterController2D. Optional, but without it you must remember to check 'Has Dash Ability' by hand every single Play session.")]
    public CharacterController2D playerController;

    private void Update()
    {
        var keys = KeyBindingManager.Instance;
        if (keys == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            keys.UnlockAction(GameAction.Dash);
            keys.UnlockModifierKey();

            if (playerController != null)
                playerController.hasDashAbility = true;
            else
                Debug.LogWarning("[DebugTestUnlocker] playerController not assigned - remember to check 'Has Dash Ability' on CharacterController2D by hand, it resets every Play session.");

            if (dashTestKey == null)
            {
                Debug.LogWarning("[DebugTestUnlocker] Dash unlocked and Ctrl modifier unlocked, but no Dash Test Key is assigned - nothing to bind.");
            }
            else
            {
                bool ok = keys.TryBindKey(GameAction.Dash, dashTestKey);
                Debug.Log(ok
                    ? $"[DebugTestUnlocker] Dash unlocked and bound to {dashTestKey.displayName}. Ctrl modifier unlocked - try Ctrl+F too."
                    : "[DebugTestUnlocker] TryBindKey(Dash) failed - is dashTestKey in KeyInventory's Starting Keys?");
            }
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            keys.UnlockAction(GameAction.RangedAttack);

            if (rangedTestKey == null)
            {
                Debug.LogWarning("[DebugTestUnlocker] RangedAttack unlocked, but no Ranged Test Key is assigned - nothing to bind.");
            }
            else
            {
                bool ok = keys.TryBindKey(GameAction.RangedAttack, rangedTestKey);
                Debug.Log(ok
                    ? $"[DebugTestUnlocker] RangedAttack unlocked and bound to {rangedTestKey.displayName}."
                    : "[DebugTestUnlocker] TryBindKey(RangedAttack) failed - is rangedTestKey in KeyInventory's Starting Keys?");
            }
        }
    }
}
