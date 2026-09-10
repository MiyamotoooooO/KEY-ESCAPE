using UnityEngine;
using System.Collections;

/// <summary>
/// KEY ESCAPE addition: this enemy can optionally BE a runaway key that has
/// malfunctioned / is running on instinct (per the "暴走キー" framing agreed
/// on for field enemies - see README.md). Defeating it hands the player
/// `keyDrop` the same way picking up a KeyPickup would. Leave `keyDrop`
/// unset for an enemy that is just an enemy and drops nothing.
///
/// This is deliberately separate from KeyPickup.cs: KeyPickup is for a key
/// quietly hiding in the stage (touch it, no combat), this is for a key
/// that's rampaging and has to be fought first. Same end result
/// (KeyInventory.AddKey), different acquisition feel.
/// </summary>
public class Enemy : MonoBehaviour
{

    public float life = 10;
    private bool isPlat;
    private bool isObstacle;
    private Transform fallCheck;
    private Transform wallCheck;
    public LayerMask turnLayerMask;
    private Rigidbody2D rb;

    private bool facingRight = true;

    public float speed = 5f;

    public bool isInvincible = false;
    private bool isHitted = false;

    [Header("KEY ESCAPE - 暴走キー")]
    [Tooltip("倒すと入手できるキー。未設定ならキーは何もドロップしない（そのままただの敵）。隠れているだけで戦わず拾えるキーはKeyPickup.csを使う")]
    public KeyDefinition keyDrop;
    private bool _keyGranted;

    void Awake()
    {
        fallCheck = transform.Find("FallCheck");
        wallCheck = transform.Find("WallCheck");
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {

        if (life <= 0)
        {
            transform.GetComponent<Animator>().SetBool("IsDead", true);
            GrantKeyDropOnce();
            StartCoroutine(DestroyEnemy());
        }

        isPlat = Physics2D.OverlapCircle(fallCheck.position, .2f, 1 << LayerMask.NameToLayer("Default"));
        isObstacle = Physics2D.OverlapCircle(wallCheck.position, .2f, turnLayerMask);

        if (!isHitted && life > 0 && Mathf.Abs(rb.velocity.y) < 0.5f)
        {
            if (isPlat && !isObstacle && !isHitted)
            {
                if (facingRight)
                {
                    rb.velocity = new Vector2(-speed, rb.velocity.y);
                }
                else
                {
                    rb.velocity = new Vector2(speed, rb.velocity.y);
                }
            }
            else
            {
                Flip();
            }
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
            transform.GetComponent<Animator>().SetBool("Hit", true);
            life -= damage;
            rb.velocity = Vector2.zero;
            rb.AddForce(new Vector2(direction * 500f, 100f));
            StartCoroutine(HitTime());
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Player" && life > 0)
        {
            collision.gameObject.GetComponent<CharacterController2D>().ApplyDamage(2f, transform.position);
        }
    }

    IEnumerator HitTime()
    {
        isHitted = true;
        isInvincible = true;
        yield return new WaitForSeconds(0.1f);
        isHitted = false;
        isInvincible = false;
    }

    /// <summary>Called once, right when the death sequence starts (life hits 0) - see FixedUpdate().</summary>
    private void GrantKeyDropOnce()
    {
        if (_keyGranted || keyDrop == null)
            return;
        _keyGranted = true;

        if (KeyInventory.Instance == null)
        {
            Debug.LogWarning($"[Enemy] {name}: no KeyInventory in the scene (Bootstrap missing?) - key drop lost.", this);
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
        yield return new WaitForSeconds(0.25f);
        rb.velocity = new Vector2(0, rb.velocity.y);
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }
}
