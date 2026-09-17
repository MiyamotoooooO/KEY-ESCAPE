using UnityEngine;

/// <summary>
/// KEY ESCAPE key-layer refactor of the original PlayerMovement.
///
/// The only functional change from the original asset: KeyCode.Z / KeyCode.C
/// and the "Horizontal" input axis are gone. Movement and jump/dash now read
/// through KeyBindingManager, so whatever the player has bound to
/// MoveLeft/MoveRight/Jump/Dash in the Key Config screen is what fires here -
/// which is the entire point of the game (spec Ver.0.1 §6-7).
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

    [Tooltip("trueの間はキー入力を一切読み取らず、移動・ジャンプ・ダッシュができなくなる（会話中などに使う）。Time.timeScaleは変更しないので、アニメーションや他のオブジェクトは通常通り動き続ける。")]
    public bool inputLocked = false;

    private KeyBindingManager Keys => KeyBindingManager.Instance;

    /// <summary>
    /// DialogueKeyRecruitなど、外部のイベントからプレイヤーの操作を止めたい
    /// 時に呼ぶ。Time.timeScaleは触らないので、プレイヤー自身やまわりの
    /// アイドルアニメーションはそのまま再生され続ける。
    /// </summary>
    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;

        if (locked)
        {
            // ロックした瞬間の入力・慣性を引きずらないよう、即座にリセットする。
            horizontalMove = 0f;
            jump = false;
            dash = false;

            if (animator != null)
                animator.SetFloat("Speed", 0f);
            if (controller != null)
                controller.StopHorizontalMovement();
        }
    }

    private void Update()
    {
        if (Keys == null)
            return; // No KeyBindingManager in the scene yet - see README.

        // ロック中は入力を読み取らない。horizontalMove/jump/dashはSetInputLocked(true)
        // 側で既に0/falseにしてあるので、FixedUpdate()側は毎回「動かない」入力を
        // 受け取り続けるだけになる。
        if (inputLocked)
            return;

        float move = 0f;
        if (Keys.IsActionHeld(GameAction.MoveLeft)) move -= 1f;
        if (Keys.IsActionHeld(GameAction.MoveRight)) move += 1f;
        horizontalMove = move * runSpeed;

        animator.SetFloat("Speed", Mathf.Abs(horizontalMove));

        if (Keys.IsActionDown(GameAction.Jump))
        {
            jump = true;
        }

        // Dash starts locked (spec Ver.0.2 §12: unlocked via a stage-clear reward).
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
