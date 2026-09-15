using UnityEngine;

// Тип №2 — телепортирующийся противник.
// Обнаружение: периодическое круговое сканирование (360°, радиус 1 м, интервал scanInterval),
// а не постоянное конусное зрение, как у обычного врага.
// Особая способность: телепортация с перезарядкой, ограничением дистанции,
// запретом телепортироваться внутрь препятствий и опциональным визуальным эффектом.
//
// Условие использования телепортации (выбрано и обосновано студентом):
//  1) Chase -> Teleport, если игрок пытается убежать (дистанция >= fleeDistanceThreshold)
//     и способность готова — так враг не теряет игрока при погоне и выглядит "опасным".
//  2) Search -> Teleport, если враг долго не может найти игрока — телепортируется в новую
//     точку рядом с последней известной позицией, чтобы расширить зону поиска.
//
// Схема состояний:
// Idle/Patrol/Search -> (периодическое сканирование нашло игрока) -> Chase
// Chase  -> (дистанция <= attackRange)                              -> Attack
// Chase  -> (дистанция > loseChaseRange)                            -> Search
// Chase  -> (игрок убегает и телепорт готов)                        -> Teleport
// Attack -> (дистанция > attackRange * 1.2)                         -> Chase
// Search -> (телепорт готов и долго не находит игрока)              -> Teleport
// Search -> (таймер поиска истёк)                                   -> Patrol
// Teleport -> (после телепортации видит игрока)                     -> Chase
// Teleport -> (после телепортации не видит игрока)                  -> Search
public class EnemyTeleportFSM : EnemyFSM
{
    [Header("Idle")]
    public float idleDuration = 2f;
    private float idleTimer;

    [Header("Сканирование 360°")]
    public float scanRadius = 1f;      // радиус обнаружения при сканировании (по ТЗ — 1 метр)
    public float scanInterval = 1.5f;  // периодичность сканирования, задаётся самостоятельно
    private float scanTimer;

    [Header("Погоня")]
    public float loseChaseRange = 6f;  // дистанция, на которой враг теряет игрока во время погони

    [Header("Телепортация")]
    public float teleportCooldown = 4f;              // перезарядка способности
    public float teleportMaxDistance = 5f;            // максимальная дистанция телепортации
    public float teleportMinDistanceFromPlayer = 1f;  // не телепортироваться прямо в игрока
    public LayerMask teleportObstacleMask;            // маска препятствий для точки назначения
    public float teleportCheckRadius = 0.3f;          // радиус проверки на препятствия
    public float fleeDistanceThreshold = 4f;          // с какой дистанции считаем, что игрок "убегает"
    public GameObject teleportEffect;
    public AudioClip teleportSound;

    private float lastTeleportTime = -999f;
    private bool TeleportReady => Time.time >= lastTeleportTime + teleportCooldown;

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

            case EnemyState.Teleport:
                StopMoving();
                DoTeleport();
                break;
        }
    }

    protected override void UpdateState()
    {
        // Периодическое круговое сканирование работает в фоновом режиме
        // во всех состояниях, кроме Attack и Teleport.
        if (CurrentState != EnemyState.Attack && CurrentState != EnemyState.Teleport)
        {
            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = scanInterval;

                bool canBeAlerted = CurrentState == EnemyState.Idle
                                  || CurrentState == EnemyState.Patrol
                                  || CurrentState == EnemyState.Search;

                if (canBeAlerted && CanSeePlayer(scanRadius, 360f))
                {
                    EnterState(EnemyState.Chase);
                    return;
                }
            }
        }

        switch (CurrentState)
        {
            case EnemyState.Idle: UpdateIdle(); break;
            case EnemyState.Patrol: UpdatePatrol(); break;
            case EnemyState.Chase: UpdateChase(); break;
            case EnemyState.Attack: UpdateAttack(); break; // реализация из базового класса (умеет стрелять жалом)
            case EnemyState.Search: UpdateSearch(); break;
            case EnemyState.Teleport: break; // мгновенный переход, обрабатывается в OnEnterState
        }
    }

    // === IDLE ===
    private void UpdateIdle()
    {
        StopMoving();
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f) EnterState(EnemyState.Patrol);
    }

    // === PATROL ===
    private void UpdatePatrol()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Transform target = patrolPoints[currentPatrolIndex];
            Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;

            if (toTarget.magnitude < 0.2f)
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            else
                MoveTowards(target.position, patrolSpeed);
        }
        else
        {
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

        // Игрок пытается убежать, а способность готова — телепортируемся ближе к нему
        if (TeleportReady && dist >= fleeDistanceThreshold)
        {
            EnterState(EnemyState.Teleport);
            return;
        }

        if (dist > loseChaseRange)
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

        if (distToLast <= 0.3f)
        {
            searchTimer -= Time.deltaTime;

            // Если долго не можем найти игрока — телепортируемся в новую точку поиска
            if (TeleportReady && searchTimer <= searchDuration * 0.5f)
            {
                EnterState(EnemyState.Teleport);
                return;
            }

            if (searchTimer <= 0f)
                EnterState(EnemyState.Patrol);
        }
    }

    // === TELEPORT ===
    private void DoTeleport()
    {
        Vector2 destination = FindTeleportDestination();
        transform.position = destination;
        lastTeleportTime = Time.time;

        if (teleportEffect != null) Instantiate(teleportEffect, destination, Quaternion.identity);
        if (teleportSound != null) AudioSource.PlayClipAtPoint(teleportSound, destination);

        // После телепортации враг продолжает своё поведение
        if (player != null && CanSeePlayer(detectionRange, 360f))
            EnterState(EnemyState.Chase);
        else
            EnterState(EnemyState.Search);
    }

    // Подбирает точку телепортации рядом с игроком (если известен) в пределах teleportMaxDistance,
    // не внутри препятствия и не слишком близко к самому игроку.
    private Vector2 FindTeleportDestination()
    {
        Vector2 origin = transform.position;
        Vector2 anchor = player != null ? (Vector2)player.position : origin;

        const int maxAttempts = 8;
        for (int i = 0; i < maxAttempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(teleportMinDistanceFromPlayer, teleportMaxDistance);
            Vector2 candidate = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            // Ограничение максимальной дистанции телепортации относительно текущей позиции
            if (Vector2.Distance(origin, candidate) > teleportMaxDistance) continue;

            // Запрет на телепортацию внутрь препятствий
            if (Physics2D.OverlapCircle(candidate, teleportCheckRadius, teleportObstacleMask) != null) continue;

            return candidate;
        }

        return origin; // не нашли подходящую точку — остаёмся на месте
    }

    protected override void ApplyMovement()
    {
        rb.velocity = moveDirection;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, scanRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, teleportMaxDistance);
    }
}