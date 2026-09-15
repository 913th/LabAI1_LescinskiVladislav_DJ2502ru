using UnityEngine;

// Базовый конечный автомат врага. Содержит общую логику: здоровье, атаку (в т.ч. стрельбу жалом),
// перемещение к цели, проверку видимости игрока (дальность + угол обзора + Raycast).
// Конкретное поведение состояний (Idle/Patrol/Chase/Attack/Search[/Teleport]) реализуют наследники.
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyFSM : MonoBehaviour, IDamageable
{
    public EnemyState CurrentState { get; protected set; } = EnemyState.Idle;

    [Header("Health")]
    public int maxHealth = 3;
    protected int currentHealth;
    protected bool isDead = false;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float patrolSpeed = 1.5f;

    [Header("Detection")]
    public float detectionRange = 5f;
    [Range(0f, 360f)] public float fieldOfView = 180f;
    public LayerMask obstacleMask;          // слой препятствий (стены)
    public LayerMask playerMask;            // слой игрока

    [Header("Attack")]
    public float attackRange = 0.8f;
    public int damageAmount = 2;
    public float attackCooldown = 1f;
    protected float lastAttackTime;

    [Header("Ranged Attack (Sting)")]
    public bool useRangedAttack = true;     // враг атакует, выстреливая жалом (Sting)
    public GameObject stingPrefab;
    public Transform throwPoint;            // если null — бросок от текущей позиции
    public float stingForce = 8f;
    public float stingLifetime = 3f;
    public AudioClip throwSound;

    [Header("Search")]
    public float searchDuration = 3f;
    protected float searchTimer;
    protected Vector2 lastKnownPlayerPos;

    [Header("Patrol")]
    public Transform[] patrolPoints;
    protected int currentPatrolIndex = 0;

    [Header("Effects")]
    public GameObject deathEffect;
    public AudioClip hurtSound;
    public AudioClip deathSound;

    protected Transform player;
    protected Rigidbody2D rb;
    protected SpriteRenderer sr;
    protected Vector2 moveDirection;

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
        FindPlayer();
        EnterState(EnemyState.Idle);
    }

    protected virtual void Update()
    {
        if (isDead) return;
        if (player == null) { FindPlayer(); return; }

        UpdateState();
    }

    protected virtual void FixedUpdate()
    {
        if (isDead) return;
        ApplyMovement();
    }

    // === Переключение состояний ===
    protected virtual void EnterState(EnemyState newState)
    {
        CurrentState = newState;
        OnEnterState(newState);
    }

    protected abstract void OnEnterState(EnemyState state);
    protected abstract void UpdateState();
    protected abstract void ApplyMovement();

    // === Поиск игрока на сцене ===
    protected void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    // === Проверка видимости (дальность + угол обзора + Raycast/Line of Sight) ===
    // Перегрузка с параметрами нужна, чтобы один и тот же механизм использовать
    // и для узкого конуса зрения обычного врага, и для кругового (360°) сканирования телепорта.
    protected bool CanSeePlayer(float range, float fov)
    {
        if (player == null) return false;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float dist = toPlayer.magnitude;

        if (dist > range) return false;

        // Угол обзора (для 360° проверка пропускается)
        if (fov < 360f)
        {
            Vector2 forward = sr != null && sr.flipX ? Vector2.left : Vector2.right;
            float angle = Vector2.Angle(forward, toPlayer);
            if (angle > fov * 0.5f) return false;
        }

        // Проверка препятствий между врагом и игроком
        RaycastHit2D hit = Physics2D.Raycast(transform.position, toPlayer.normalized, dist, obstacleMask);
        if (hit.collider != null) return false;

        return true;
    }

    protected bool CanSeePlayer() => CanSeePlayer(detectionRange, fieldOfView);

    // === Дальняя атака — выстрел жалом в сторону игрока ===
    protected void ShootSting()
    {
        if (stingPrefab == null || player == null) return;

        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position;
        Vector2 dir = ((Vector2)player.position - (Vector2)spawnPos).normalized;

        GameObject sting = Instantiate(stingPrefab, spawnPos, Quaternion.identity);

        Rigidbody2D stingRb = sting.GetComponent<Rigidbody2D>();
        if (stingRb != null)
        {
            stingRb.gravityScale = 0f;              // top-down
            stingRb.velocity = dir * stingForce;
        }

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        sting.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer stingSr = sting.GetComponentInChildren<SpriteRenderer>();
        if (stingSr != null) stingSr.flipY = dir.x < 0;

        if (throwSound != null) AudioSource.PlayClipAtPoint(throwSound, transform.position);

        Destroy(sting, stingLifetime);
    }

    // Выполнение одной атаки: дальняя (жало) или ближняя, в зависимости от useRangedAttack.
    protected virtual void DoAttack()
    {
        if (player == null) return;

        if (useRangedAttack)
        {
            ShootSting();
        }
        else
        {
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(damageAmount);
        }
    }

    // Общая логика состояния Attack: дальность атаки, время между атаками, урон.
    // Наследники могут использовать её напрямую (case EnemyState.Attack: UpdateAttack(); break;)
    // или переопределить при необходимости.
    protected virtual void UpdateAttack()
    {
        StopMoving();
        FacePlayer();
        float dist = DistanceToPlayer();

        if (dist > attackRange * 1.2f)
        {
            EnterState(EnemyState.Chase);
            return;
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            DoAttack();
        }
    }

    protected float DistanceToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector2.Distance(transform.position, player.position);
    }

    protected void FacePlayer()
    {
        if (sr != null && player != null)
            sr.flipX = player.position.x < transform.position.x;
    }

    // Плавное движение к цели. Направление нормализуется, поэтому естественно
    // получаются все 8 направлений (вверх/вниз/влево/вправо и диагонали).
    protected void MoveTowards(Vector2 target, float speed)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        moveDirection = dir * speed;
        if (sr != null && Mathf.Abs(dir.x) > 0.01f)
            sr.flipX = dir.x < 0;
    }

    protected void StopMoving()
    {
        moveDirection = Vector2.zero;
    }

    // === Урон и смерть (вызывается, например, из Apple.cs через IDamageable) ===
    public virtual void TakeDamage(int damage)
    {
        if (isDead) return;
        currentHealth -= damage;

        if (hurtSound != null) AudioSource.PlayClipAtPoint(hurtSound, transform.position);
        if (sr != null) StartCoroutine(FlashRed());

        if (currentHealth <= 0) Die();
    }

    protected System.Collections.IEnumerator FlashRed()
    {
        Color orig = sr.color;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        sr.color = orig;
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        if (deathEffect != null) Instantiate(deathEffect, transform.position, Quaternion.identity);
        if (deathSound != null) AudioSource.PlayClipAtPoint(deathSound, transform.position);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (rb != null) rb.simulated = false;
        if (sr != null) sr.enabled = false;

        Destroy(gameObject, 0.3f);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}