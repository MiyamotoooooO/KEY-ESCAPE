using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KEY ESCAPE fix for the original Ally script.
///
/// The `enemy` field only ever drove movement/AI decisions (who to chase,
/// when to switch from "approach" to "melee range" to "out of range"). The
/// original MeleeAttack() ignored that field completely and instead damaged
/// *any* collider tagged "Enemy" (or "Player") found inside the attack
/// radius. That's harmless when there's exactly one Ally next to the Player,
/// but once two Ally instances (both tagged "Enemy", per the scene setup)
/// end up within melee range of each other, they start damaging each other
/// even though each one's `enemy` field points at the Player - the tag
/// check doesn't care what `enemy` is set to.
///
/// MeleeAttack() below now only ever damages the specific GameObject
/// assigned to `enemy`, matching what the movement AI is actually chasing.
/// This also fixes the same "mutates a shared field with a sign flip inside
/// the loop" bug that Attack.cs had - dmgValue is no longer overwritten in
/// place, so repeated attacks can't drift in sign over time.
///
/// Also added: an optional `keyDrop` (same idea as Enemy.cs) - this lets an
/// Ally instance double as a "暴走キー" (a malfunctioning/rampaging key that
/// must be fought, as opposed to KeyPickup's quietly-hiding key). Granted
/// once, right when the death sequence starts.
///
/// Unrelated, and NOT a bug: if the assigned `enemy` is destroyed (e.g. you
/// pointed one Ally's `enemy` at another Ally for testing, and that Ally
/// died), Unity reports the reference as null on the next frame, and
/// FixedUpdate()'s existing fallback (`enemy = GameObject.Find("DrawCharacter")`)
/// re-acquires the Player automatically. That's the original asset's
/// intended behavior for "no target assigned yet" - it just also fires
/// whenever a previously-assigned target goes away. As long as every Ally's
/// `enemy` is set to the Player (DrawCharacter) to begin with, this fallback
/// never has anything to change.
/// </summary>
public class Ally : MonoBehaviour
{
    private Rigidbody2D m_Rigidbody2D;
    private bool m_FacingRight = true;  // For determining which way the player is currently facing.

    public float life = 10;

    private bool facingRight = true;

    public float speed = 5f;

    public bool isInvincible = false;
    private bool isHitted = false;

    [SerializeField] private float m_DashForce = 25f;
    private bool isDashing = false;

    public GameObject enemy;
    private float distToPlayer;
    private float distToPlayerY;
    public float meleeDist = 1.5f;
    public float rangeDist = 5f;
    private bool canAttack = true;
    private Transform attackCheck;
    public float dmgValue = 4;

    public GameObject throwableObject;

    private float randomDecision = 0;
    private bool doOnceDecision = true;
    private bool endDecision = false;
    private Animator anim;

    [Header("KEY ESCAPE - 暴走キー")]
    [Tooltip("倒すと入手できるキー。未設定ならキーは何もドロップしない。隠れているだけで戦わず拾えるキーはKeyPickup.csを使う")]
    public KeyDefinition keyDrop;
    private bool _keyGranted;

    void Awake()
    {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
        attackCheck = transform.Find("AttackCheck").transform;
        anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {

        if (life <= 0)
        {
            GrantKeyDropOnce();
            StartCoroutine(DestroyEnemy());
        }

        else if (enemy != null)
        {
            if (isDashing)
            {
                m_Rigidbody2D.velocity = new Vector2(transform.localScale.x * m_DashForce, 0);
            }
            else if (!isHitted)
            {
                distToPlayer = enemy.transform.position.x - transform.position.x;
                distToPlayerY = enemy.transform.position.y - transform.position.y;

                if (Mathf.Abs(distToPlayer) < 0.25f)
                {
                    GetComponent<Rigidbody2D>().velocity = new Vector2(0f, m_Rigidbody2D.velocity.y);
                    anim.SetBool("IsWaiting", true);
                }
                else if (Mathf.Abs(distToPlayer) > 0.25f && Mathf.Abs(distToPlayer) < meleeDist && Mathf.Abs(distToPlayerY) < 2f)
                {
                    GetComponent<Rigidbody2D>().velocity = new Vector2(0f, m_Rigidbody2D.velocity.y);
                    if ((distToPlayer > 0f && transform.localScale.x < 0f) || (distToPlayer < 0f && transform.localScale.x > 0f))
                        Flip();
                    if (canAttack)
                    {
                        MeleeAttack();
                    }
                }
                else if (Mathf.Abs(distToPlayer) > meleeDist && Mathf.Abs(distToPlayer) < rangeDist)
                {
                    anim.SetBool("IsWaiting", false);
                    m_Rigidbody2D.velocity = new Vector2(distToPlayer / Mathf.Abs(distToPlayer) * speed, m_Rigidbody2D.velocity.y);
                }
                else
                {
                    if (!endDecision)
                    {
                        if ((distToPlayer > 0f && transform.localScale.x < 0f) || (distToPlayer < 0f && transform.localScale.x > 0f))
                            Flip();

                        if (randomDecision < 0.4f)
                            Run();
                        else if (randomDecision >= 0.4f && randomDecision < 0.6f)
                            Jump();
                        else if (randomDecision >= 0.6f && randomDecision < 0.8f)
                            StartCoroutine(Dash());
                        else if (randomDecision >= 0.8f && randomDecision < 0.95f)
                            RangeAttack();
                        else
                            Idle();
                    }
                    else
                    {
                        endDecision = false;
                    }
                }
            }
            else if (isHitted)
            {
                if ((distToPlayer > 0f && transform.localScale.x > 0f) || (distToPlayer < 0f && transform.localScale.x < 0f))
                {
                    Flip();
                    StartCoroutine(Dash());
                }
                else
                    StartCoroutine(Dash());
            }
        }
        else
        {
            enemy = GameObject.Find("DrawCharacter");
        }

        if (transform.localScale.x * m_Rigidbody2D.velocity.x > 0 && !m_FacingRight && life > 0)
        {
            // ... flip the player.
            Flip();
        }
        // Otherwise if the input is moving the player left and the player is facing right...
        else if (transform.localScale.x * m_Rigidbody2D.velocity.x < 0 && m_FacingRight && life > 0)
        {
            // ... flip the player.
            Flip();
        }
    }

    void Flip()
    {
        // Switch the way the player is labelled as facing.
        facingRight = !facingRight;

        // Multiply the player's x local scale by -1.
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }

    public void ApplyDamage(float damage)
    {
        if (!isInvincible)
        {
            float direction = damage / Mathf.Abs(damage);
            damage = Mathf.Abs(damage);
            anim.SetBool("Hit", true);
            life -= damage;
            transform.gameObject.GetComponent<Rigidbody2D>().velocity = new Vector2(0, 0);
            transform.gameObject.GetComponent<Rigidbody2D>().AddForce(new Vector2(direction * 300f, 100f));
            StartCoroutine(HitTime());
        }
    }

    /// <summary>
    /// KEY ESCAPE fix: only damages `enemy` itself (the thing the AI is
    /// actually chasing), not every "Enemy"/"Player"-tagged collider in
    /// range. Prevents two Ally instances from hitting each other when they
    /// both carry the "Enemy" tag and end up next to one another. Also no
    /// longer mutates `dmgValue` in place (local variable instead), so the
    /// damage sign can't drift across repeated attacks.
    /// </summary>
    public void MeleeAttack()
    {
        transform.GetComponent<Animator>().SetBool("Attack", true);
        Collider2D[] collidersEnemies = Physics2D.OverlapCircleAll(attackCheck.position, 0.9f);
        for (int i = 0; i < collidersEnemies.Length; i++)
        {
            GameObject hit = collidersEnemies[i].gameObject;
            if (hit == gameObject || hit != enemy)
                continue;

            if (hit.CompareTag("Player"))
            {
                hit.GetComponent<CharacterController2D>().ApplyDamage(2f, transform.position);
            }
            else if (hit.CompareTag("Enemy"))
            {
                float signedDamage = transform.localScale.x < 0 ? -dmgValue : dmgValue;
                hit.SendMessage("ApplyDamage", signedDamage);
            }
        }
        StartCoroutine(WaitToAttack(0.5f));
    }

    public void RangeAttack()
    {
        if (doOnceDecision)
        {
            GameObject throwableProj = Instantiate(throwableObject, transform.position + new Vector3(transform.localScale.x * 0.5f, -0.2f), Quaternion.identity) as GameObject;
            throwableProj.GetComponent<ThrowableProjectile>().owner = gameObject;
            Vector2 direction = new Vector2(transform.localScale.x, 0f);
            throwableProj.GetComponent<ThrowableProjectile>().direction = direction;
            StartCoroutine(NextDecision(0.5f));
        }
    }

    public void Run()
    {
        anim.SetBool("IsWaiting", false);
        m_Rigidbody2D.velocity = new Vector2(distToPlayer / Mathf.Abs(distToPlayer) * speed, m_Rigidbody2D.velocity.y);
        if (doOnceDecision)
            StartCoroutine(NextDecision(0.5f));
    }
    public void Jump()
    {
        Vector3 targetVelocity = new Vector2(distToPlayer / Mathf.Abs(distToPlayer) * speed, m_Rigidbody2D.velocity.y);
        Vector3 velocity = Vector3.zero;
        m_Rigidbody2D.velocity = Vector3.SmoothDamp(m_Rigidbody2D.velocity, targetVelocity, ref velocity, 0.05f);
        if (doOnceDecision)
        {
            anim.SetBool("IsWaiting", false);
            // KEY ESCAPE addition: the original script never told the Animator
            // a jump was happening, so no Jump animation state could ever play.
            // NextDecision() (below) clears this back to false once the ~1s
            // jump decision window ends.
            anim.SetBool("IsJumping", true);
            m_Rigidbody2D.AddForce(new Vector2(0f, 850f));
            StartCoroutine(NextDecision(1f));
        }
    }

    public void Idle()
    {
        m_Rigidbody2D.velocity = new Vector2(0f, m_Rigidbody2D.velocity.y);
        if (doOnceDecision)
        {
            anim.SetBool("IsWaiting", true);
            StartCoroutine(NextDecision(1f));
        }
    }

    public void EndDecision()
    {
        randomDecision = Random.Range(0.0f, 1.0f);
        endDecision = true;
    }

    IEnumerator HitTime()
    {
        isInvincible = true;
        isHitted = true;
        yield return new WaitForSeconds(0.1f);
        isHitted = false;
        isInvincible = false;
    }

    IEnumerator WaitToAttack(float time)
    {
        canAttack = false;
        yield return new WaitForSeconds(time);
        canAttack = true;
    }

    IEnumerator Dash()
    {
        anim.SetBool("IsDashing", true);
        isDashing = true;
        yield return new WaitForSeconds(0.1f);
        isDashing = false;
        EndDecision();
    }

    IEnumerator NextDecision(float time)
    {
        doOnceDecision = false;
        yield return new WaitForSeconds(time);
        EndDecision();
        doOnceDecision = true;
        anim.SetBool("IsWaiting", false);
        anim.SetBool("IsJumping", false); // clears whatever Jump() set, harmless no-op otherwise
    }

    /// <summary>Called once, right when the death sequence starts (life hits 0) - see FixedUpdate().</summary>
    private void GrantKeyDropOnce()
    {
        if (_keyGranted || keyDrop == null)
            return;
        _keyGranted = true;

        if (KeyInventory.Instance == null)
        {
            Debug.LogWarning($"[Ally] {name}: no KeyInventory in the scene (Bootstrap missing?) - key drop lost.", this);
            return;
        }
        KeyInventory.Instance.AddKey(keyDrop);
    }

    IEnumerator DestroyEnemy()
    {
        CapsuleCollider2D capsule = GetComponent<CapsuleCollider2D>();
        capsule.size = new Vector2(1f, 0.25f);
        capsule.offset = new Vector2(0f, -0.8f);
        capsule.direction = CapsuleDirection2D.Horizontal;
        transform.GetComponent<Animator>().SetBool("IsDead", true);
        yield return new WaitForSeconds(0.25f);
        m_Rigidbody2D.velocity = new Vector2(0, m_Rigidbody2D.velocity.y);
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }
}
