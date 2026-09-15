using UnityEngine;

public class Sting : MonoBehaviour
{
    public int damage = 1;
    public GameObject hitEffect;
    public AudioClip hitSound;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.TakeDamage(damage); // звук играет внутри PlayerHealth

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