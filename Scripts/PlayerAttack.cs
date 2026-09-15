using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("Apple Settings")]
    public GameObject applePrefab;
    public Transform throwPoint;        // если null — берём позицию самого игрока
    public float throwForce = 10f;

    [Header("Attack Settings")]
    public float attackCooldown = 0.5f;

    [Header("Sounds")]
    public AudioClip throwSound;        // звук броска

    private float lastAttackTime;
    private Animator anim;
    private bool isAttacking = false;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (isAttacking) return;
        if (Time.time < lastAttackTime + attackCooldown) return;

        // Проверяем 4 клавиши по очереди
        if (Input.GetKeyDown(KeyCode.Keypad8)) Attack(Vector2.up);       // Num 8
        else if (Input.GetKeyDown(KeyCode.Keypad2)) Attack(Vector2.down); // Num 2
        else if (Input.GetKeyDown(KeyCode.Keypad7)) Attack(new Vector2(-1f, 1f).normalized); // Num 7
        else if (Input.GetKeyDown(KeyCode.Keypad1)) Attack(new Vector2(-1f, -1f).normalized); // Num 1
    }

    void Attack(Vector2 direction)
    {
        lastAttackTime = Time.time;
        isAttacking = true;

        if (anim != null) anim.SetTrigger("Attack");
        if (throwSound != null)
            AudioSource.PlayClipAtPoint(throwSound, transform.position);

        // Передаём направление в ThrowApple через лямбду Invoke нельзя,
        // поэтому используем корутину
        StartCoroutine(ThrowAfterDelay(direction, 0.2f));
        Invoke(nameof(ResetAttack), 0.3f);
    }

    System.Collections.IEnumerator ThrowAfterDelay(Vector2 direction, float delay)
    {
        yield return new WaitForSeconds(delay);
        ThrowApple(direction);
    }

    void ThrowApple(Vector2 direction)
    {
        if (applePrefab == null) return;

        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position;

        GameObject apple = Instantiate(applePrefab, spawnPos, Quaternion.identity);

        Rigidbody2D rb = apple.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;                       // top-down: гравитация не нужна
            rb.velocity = direction * throwForce;       // летим строго в нужную сторону
        }

        // Поворачиваем яблоко «носом» в сторону полёта
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        apple.transform.rotation = Quaternion.Euler(0, 0, angle);

        // Разворачиваем спрайт, если летим влево
        SpriteRenderer sr = apple.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            sr.flipY = direction.x < 0;  // или flipX — зависит от спрайта

        Destroy(apple, 3f);
    }

    void ResetAttack()
    {
        isAttacking = false;
    }
}