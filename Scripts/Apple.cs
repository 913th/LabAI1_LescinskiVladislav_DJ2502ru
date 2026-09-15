using UnityEngine;

public class Apple : MonoBehaviour
{
    public int damage = 1;
    public GameObject hitEffect;
    public AudioClip hitSound;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log($"яблоко столкнулось с: {collision.tag}");

        if (collision.CompareTag("Enemy"))
        {
            Debug.Log("яблоко попало во врага!");

            // »щем любой компонент, реализующий IDamageable
            IDamageable target = collision.GetComponent<IDamageable>();

            if (target != null)
            {
                target.TakeDamage(damage);
                Debug.Log($"яблоко нанесло {damage} урона врагу!");
            }
            else
            {
                Debug.LogError("Ќа враге нет компонента с IDamageable!");
            }

            if (hitEffect != null)
                Instantiate(hitEffect, transform.position, Quaternion.identity);

            if (hitSound != null)
                AudioSource.PlayClipAtPoint(hitSound, transform.position);

            Destroy(gameObject);
        }
        else if (collision.CompareTag("Wall") || collision.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }
}