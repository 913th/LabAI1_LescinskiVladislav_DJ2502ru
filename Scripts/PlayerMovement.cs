using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Настройки движения")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 20f;   // Плавность разгона
    [SerializeField] private float deceleration = 25f;   // Плавность торможения

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector2 currentVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;              // Top-down — гравитация не нужна
        rb.freezeRotation = true;          // Чтобы физика не вращала персонажа
    }

    private void Update()
    {
        // Ввод с клавиатуры (WASD / стрелки)
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        moveInput = moveInput.normalized; // Чтобы по диагонали не было быстрее
    }

    private void FixedUpdate()
    {
        Vector2 targetVelocity = moveInput * moveSpeed;

        // Плавное ускорение/замедление
        float rate = moveInput.sqrMagnitude > 0.01f ? acceleration : deceleration;
        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);

        rb.velocity = currentVelocity; // В Unity 6
        // rb.velocity = currentVelocity;    // Для Unity 2022 и ниже
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = input.normalized;
    }
}
