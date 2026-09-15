using UnityEngine;

// “ип є1 Ч обычный противник.
// ќбнаружение: направленное поле зрени€ 180∞ + дальность + Raycast (проверка преп€тствий).
// ѕеремещение: плавное, 8-направленное (за счЄт нормализованного вектора направлени€ в MoveTowards).
//
// —хема состо€ний:
// Idle    -> (видит игрока)                    -> Chase
// Idle    -> (истЄк таймер ожидани€)            -> Patrol
// Patrol  -> (видит игрока)                     -> Chase
// Chase   -> (дистанци€ <= attackRange)         -> Attack
// Chase   -> (игрок вне зоны обнаружени€)       -> Search
// Attack  -> (дистанци€ > attackRange * 1.2)    -> Chase
// Search  -> (снова увидел игрока)              -> Chase
// Search  -> (не нашЄл игрока, таймер истЄк)    -> Patrol
public class EnemyMeleeFSM : EnemyFSM
{
    [Header("Idle")]
    public float idleDuration = 2f;
    private float idleTimer;

    [Header("Patrol fallback (если patrolPoints не заданы)")]
    public float patrolLegDistance = 2f;
    private Vector2 patrolOrigin;
    private Vector2 patrolTarget;
    private bool patrolInitialized = false;

    protected override void OnEnterState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                idleTimer = idleDuration;
                StopMoving();
                break;

            case EnemyState.Patrol:
                StopMoving();
                break;

            case EnemyState.Chase:
                break;

            case EnemyState.Attack:
                StopMoving();
                break;

            case EnemyState.Search:
                searchTimer = searchDuration;
                if (player != null)
                    lastKnownPlayerPos = player.position;
                break;
        }
    }

    protected override void UpdateState()
    {
        switch (CurrentState)
        {
            case EnemyState.Idle: UpdateIdle(); break;
            case EnemyState.Patrol: UpdatePatrol(); break;
            case EnemyState.Chase: UpdateChase(); break;
            case EnemyState.Attack: UpdateAttack(); break; // реализаци€ из базового класса (умеет стрел€ть жалом)
            case EnemyState.Search: UpdateSearch(); break;
        }
    }

    // === IDLE ===
    private void UpdateIdle()
    {
        StopMoving();
        idleTimer -= Time.deltaTime;

        if (CanSeePlayer()) { EnterState(EnemyState.Chase); return; }
        if (idleTimer <= 0f) EnterState(EnemyState.Patrol);
    }

    // === PATROL ===
    private void UpdatePatrol()
    {
        if (CanSeePlayer()) { EnterState(EnemyState.Chase); return; }

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            // ѕатрулирование по заданным точкам
            Transform target = patrolPoints[currentPatrolIndex];
            Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;

            if (toTarget.magnitude < 0.2f)
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            else
                MoveTowards(target.position, patrolSpeed);
        }
        else
        {
            // Fallback: простое хождение туда-обратно
            if (!patrolInitialized)
            {
                patrolOrigin = transform.position;
                patrolTarget = patrolOrigin + Vector2.right * patrolLegDistance;
                patrolInitialized = true;
            }

            if (Vector2.Distance(transform.position, patrolTarget) < 0.2f)
            {
                patrolTarget = (patrolTarget == patrolOrigin + Vector2.right * patrolLegDistance)
                    ? patrolOrigin + Vector2.left * patrolLegDistance
                    : patrolOrigin + Vector2.right * patrolLegDistance;
            }

            MoveTowards(patrolTarget, patrolSpeed);
        }
    }

    // === CHASE ===
    private void UpdateChase()
    {
        float dist = DistanceToPlayer();

        if (dist > detectionRange * 1.2f) // небольшой запас, чтобы не тер€ть цель на границе
        {
            EnterState(EnemyState.Search);
            return;
        }

        if (dist <= attackRange)
        {
            EnterState(EnemyState.Attack);
            return;
        }

        FacePlayer();
        MoveTowards(player.position, moveSpeed);
    }

    // === SEARCH ===
    private void UpdateSearch()
    {
        float distToLast = Vector2.Distance(transform.position, lastKnownPlayerPos);

        if (distToLast > 0.3f)
            MoveTowards(lastKnownPlayerPos, moveSpeed);
        else
            StopMoving();

        if (CanSeePlayer())
        {
            EnterState(EnemyState.Chase);
            return;
        }

        if (distToLast <= 0.3f)
        {
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f)
                EnterState(EnemyState.Patrol);
        }
    }

    // Top-down перемещение по обеим ос€м Ч даЄт полноценные 8 направлений.
    protected override void ApplyMovement()
    {
        rb.velocity = moveDirection;
    }
}