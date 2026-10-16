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

    [Header("KEY ESCAPE - 索敵")]
    [Tooltip("プレイヤーを見つける範囲（横）。この範囲に入った瞬間に索敵状態になる（ワールド単位）")]
    public float detectRangeX = 8f;
    [Tooltip("プレイヤーを見つける範囲（縦）（ワールド単位）")]
    public float detectRangeY = 3f;
    [Tooltip("一度索敵したあと、範囲を何倍まで広げて判定するか。境目で索敵/未索敵が繰り返し切り替わらないようにする")]
    public float loseRangeMultiplier = 1.25f;
    [Tooltip("true: 索敵していない間は追いかけず待機する。false: 昔のとおり、離れていても追い続ける")]
    public bool idleWhenUnaware = true;

    /// <summary>プレイヤーを索敵している間 true。吹き出し（EmoteBubble）などが読む。</summary>
    public bool IsAlerted { get; private set; }
    private bool _provoked; // ダメージを受けたら、範囲の外でも以後ずっと索敵状態

    [Header("KEY ESCAPE - 障害物を飛び越える")]
    [Tooltip("true: 走っていて前に障害物があればジャンプして飛び越える。false: 昔のとおり、ぶつかったまま押し続ける")]
    public bool jumpOverObstacles = true;
    [Tooltip("障害物とみなすレイヤー。空欄（Nothing）のときは Default を使う")]
    public LayerMask obstacleMask;
    [Tooltip("ジャンプの強さ。大きいほど高く跳ぶ")]
    public float obstacleJumpForce = 650f;
    [Tooltip("足元からこの高さ（ワールド単位）にも壁があれば、高すぎて越えられないので跳ばない")]
    public float maxObstacleHeight = 1.3f;
    [Tooltip("足元からこの高さで、前に障害物があるかを見る（ワールド単位）")]
    public float obstacleFootRayHeight = 0.15f;
    [Tooltip("進む方向のどのくらい先まで、障害物を見るか（ワールド単位）")]
    public float obstacleCheckDistance = 0.4f;
    [Tooltip("走っているのに前に進めない状態がこの秒数続いたら、ジャンプを試す（斜面など、先読みで見つけにくい障害物の保険）")]
    public float stuckTime = 0.35f;

    private Collider2D _col;
    private float _stuckTimer;
    private float _lastCheckX;
    private float _nextObstacleJumpTime;

    void Awake()
    {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
        attackCheck = transform.Find("AttackCheck").transform;
        anim = GetComponent<Animator>();
        _col = GetComponent<Collider2D>();
        if (obstacleMask.value == 0)
            obstacleMask = LayerMask.GetMask("Default");
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
            UpdateAlert();

            if (idleWhenUnaware && !IsAlerted && !isDashing && !isHitted)
            {
                // KEY ESCAPE: 索敵していない間は、その場で待機する。
                m_Rigidbody2D.velocity = new Vector2(0f, m_Rigidbody2D.velocity.y);
                anim.SetBool("IsWaiting", true);
            }
            else if (isDashing)
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
                    TryJumpOverObstacle(Mathf.Sign(distToPlayer));
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

    /// <summary>
    /// KEY ESCAPE: 走っていて前に障害物があれば、ジャンプして飛び越える。
    ///   ・先読み: 進む方向の足元の高さに壁や箱があれば、ぶつかる前に跳ぶ。
    ///   ・詰まり検出: 走っているのに前に進めない状態が stuckTime 続いたら跳ぶ。
    /// 足元からmaxObstacleHeightの高さにも壁があるとき（高すぎて越えられない）、空中、プレイヤーや敵は、跳ばない。
    /// 走る速度を入れた直後に呼ぶ（dirX は進む向き。+1か-1）。
    /// </summary>
    private void TryJumpOverObstacle(float dirX)
    {
        if (!jumpOverObstacles || life <= 0f || _col == null)
            return;

        bool grounded = Mathf.Abs(m_Rigidbody2D.velocity.y) < 0.1f;

        // 前に進めていない時間を数える（呼ばれない間に位置がずれても、数え直すだけ）。
        float moved = Mathf.Abs(transform.position.x - _lastCheckX);
        _lastCheckX = transform.position.x;
        if (grounded && moved < 0.01f)
            _stuckTimer += Time.fixedDeltaTime;
        else
            _stuckTimer = 0f;

        if (!grounded || Time.time < _nextObstacleJumpTime)
            return;

        bool blockedAtFoot = IsBlockedAhead(dirX, obstacleFootRayHeight);
        if (!blockedAtFoot && _stuckTimer < stuckTime)
            return;

        // 高すぎる壁は、跳んでも越えられない。
        if (IsBlockedAhead(dirX, maxObstacleHeight))
            return;

        m_Rigidbody2D.velocity = new Vector2(m_Rigidbody2D.velocity.x, 0f);
        m_Rigidbody2D.AddForce(new Vector2(0f, obstacleJumpForce));
        anim.SetBool("IsJumping", true);
        _nextObstacleJumpTime = Time.time + 0.5f;
        _stuckTimer = 0f;
        StartCoroutine(ClearJumpFlag());
    }

    /// <summary>足元から height の高さで、進む方向の少し先に障害物があるか。</summary>
    private bool IsBlockedAhead(float dirX, float height)
    {
        Bounds b = _col.bounds;
        float sign = dirX >= 0f ? 1f : -1f;
        Vector2 origin = new Vector2(b.center.x + sign * (b.extents.x + 0.02f), b.min.y + height);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, new Vector2(sign, 0f), obstacleCheckDistance, obstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger || c.transform.IsChildOf(transform))
                continue;
            if (c.CompareTag("Player") || c.CompareTag("Enemy") || c.gameObject == enemy)
                continue; // プレイヤーや他の敵は障害物にしない（その上を飛び越えようとしない）
            return true;
        }
        return false;
    }

    private IEnumerator ClearJumpFlag()
    {
        yield return new WaitForSeconds(0.6f);
        if (anim != null)
            anim.SetBool("IsJumping", false);
    }

    /// <summary>プレイヤーが索敵範囲に入っているかを見て、IsAlerted を更新する。</summary>
    private void UpdateAlert()
    {
        if (enemy == null)
            return;

        float dx = Mathf.Abs(enemy.transform.position.x - transform.position.x);
        float dy = Mathf.Abs(enemy.transform.position.y - transform.position.y);
        float mul = IsAlerted ? Mathf.Max(1f, loseRangeMultiplier) : 1f;
        IsAlerted = _provoked || (dx <= detectRangeX * mul && dy <= detectRangeY * mul);
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
            _provoked = true; // KEY ESCAPE: 殴られたら、範囲の外からでも以後ずっと索敵状態
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
            // KEY ESCAPE addition: 手足の生えたキー（LimbRig）なら、弾を投げる腕振りを再生する。
            LimbRig limbRig = GetComponentInChildren<LimbRig>();
            if (limbRig != null)
                limbRig.PlayThrow();

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
        TryJumpOverObstacle(Mathf.Sign(distToPlayer));
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
