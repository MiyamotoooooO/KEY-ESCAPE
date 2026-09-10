using System.Collections;
using UnityEngine;

/// <summary>
/// KEY ESCAPE key-layer refactor of the original Attack script.
///
/// Changes from the original asset:
///  - KeyCode.X (melee) and KeyCode.V (throw) are gone; both read through
///    KeyBindingManager now (GameAction.Attack / GameAction.RangedAttack).
///  - RangedAttack is gated behind IsActionUnlocked - it stays locked until
///    the Stage 7 boss reward unlocks it (spec Ver.0.2 §12).
///  - Holding the special key (Ctrl, once unlocked) while attacking is a
///    Strong Attack: Ctrl+Attack -> extra damage, per spec §15-16. The
///    animator gets a matching "IsStrongAttacking" bool - add a strong
///    attack clip/transition for it when the art is in, it's safe to leave
///    unused in the Animator Controller until then.
///  - Bug fix: the original DoDashDamage() mutated the shared `dmgValue`
///    field with a sign flip inside the loop, so damage direction could
///    leak between enemies hit in the same swing. This version uses local
///    variables instead so each enemy's damage sign is computed independently.
/// </summary>
public class Attack : MonoBehaviour
{
    public float dmgValue = 4;

    [Tooltip("Multiplier applied on top of dmgValue (after the key attribute bonus) for a Ctrl+Attack strong hit.")]
    public float strongAttackMultiplier = 1.5f;

    public GameObject throwableObject;
    public Transform attackCheck;
    private Rigidbody2D m_Rigidbody2D;
    public Animator animator;
    public bool canAttack = true;

    public GameObject cam;

    private bool _pendingStrongAttack = false;

    private KeyBindingManager Keys => KeyBindingManager.Instance;

    private void Awake()
    {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (Keys == null)
            return; // No KeyBindingManager in the scene yet - see README.

        if (canAttack && Keys.IsActionDown(GameAction.Attack))
        {
            canAttack = false;
            _pendingStrongAttack = Keys.IsActionDownWithModifier(GameAction.Attack);
            animator.SetBool("IsAttacking", true);
            animator.SetBool("IsStrongAttacking", _pendingStrongAttack);
            StartCoroutine(AttackCooldown());
        }

        if (Keys.IsActionUnlocked(GameAction.RangedAttack) && Keys.IsActionDown(GameAction.RangedAttack))
        {
            GameObject throwableWeapon = Instantiate(throwableObject, transform.position + new Vector3(transform.localScale.x * 0.5f, -0.2f), Quaternion.identity) as GameObject;
            Vector2 direction = new Vector2(transform.localScale.x, 0);
            throwableWeapon.GetComponent<ThrowableWeapon>().direction = direction;
            throwableWeapon.name = "ThrowableWeapon";
        }
    }

    private IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(0.25f);
        canAttack = true;
    }

    /// <summary>
    /// Called from the melee-attack animation clip via an Animation Event,
    /// same as in the original asset - only the damage math changed.
    /// </summary>
    public void DoDashDamage()
    {
        float attributeBonus = Keys != null ? Keys.GetBonusMultiplier(GameAction.Attack, KeyAttribute.Attack) : 0f;
        float damage = Mathf.Abs(dmgValue) * (1f + attributeBonus);
        if (_pendingStrongAttack)
            damage *= strongAttackMultiplier;

        Collider2D[] collidersEnemies = Physics2D.OverlapCircleAll(attackCheck.position, 0.9f);
        for (int i = 0; i < collidersEnemies.Length; i++)
        {
            if (collidersEnemies[i].gameObject.tag == "Enemy")
            {
                float signedDamage = damage;
                if (collidersEnemies[i].transform.position.x - transform.position.x < 0)
                    signedDamage = -signedDamage;

                collidersEnemies[i].gameObject.SendMessage("ApplyDamage", signedDamage);
                if (cam != null)
                    cam.GetComponent<CameraFollow>().ShakeCamera();
            }
        }

        _pendingStrongAttack = false;
    }
}
