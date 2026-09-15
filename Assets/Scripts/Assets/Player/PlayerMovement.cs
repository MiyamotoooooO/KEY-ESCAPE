using UnityEngine;

/// <summary>
/// KEY ESCAPE key-layer refactor of the original PlayerMovement.
///
/// The only functional change from the original asset: KeyCode.Z / KeyCode.C
/// and the "Horizontal" input axis are gone. Movement and jump/dash now read
/// through KeyBindingManager, so whatever the player has bound to
/// MoveLeft/MoveRight/Jump/Dash in the Key Config screen is what fires here -
/// which is the entire point of the game (spec Ver.0.1 Åò6-7).
///
/// Left and right are read as two independent keys rather than one axis,
/// because the spec's own first example assigns them to non-adjacent keys
/// (L for left, O for right) - they are not guaranteed to be a symmetric
/// pair. Holding both cancels out, same feel as a normal axis.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    public CharacterController2D controller;
    public Animator animator;

    public float runSpeed = 40f;

    private float horizontalMove = 0f;
    private bool jump = false;
    private bool dash = false;

    private KeyBindingManager Keys => KeyBindingManager.Instance;

    private void Update()
    {
        if (Keys == null)
            return; // No KeyBindingManager in the scene yet - see README.

        float move = 0f;
        if (Keys.IsActionHeld(GameAction.MoveLeft)) move -= 1f;
        if (Keys.IsActionHeld(GameAction.MoveRight)) move += 1f;
        horizontalMove = move * runSpeed;

        animator.SetFloat("Speed", Mathf.Abs(horizontalMove));

        if (Keys.IsActionDown(GameAction.Jump))
        {
            jump = true;
        }

        // Dash starts locked (spec Ver.0.2 Åò12: unlocked via a stage-clear reward).
        if (Keys.IsActionUnlocked(GameAction.Dash) && Keys.IsActionDown(GameAction.Dash))
        {
            dash = true;
        }
    }

    public void OnFall()
    {
        animator.SetBool("IsJumping", true);
    }

    public void OnLanding()
    {
        animator.SetBool("IsJumping", false);
    }

    private void FixedUpdate()
    {
        // Move our character
        controller.Move(horizontalMove * Time.fixedDeltaTime, jump, dash);
        jump = false;
        dash = false;
    }
}
