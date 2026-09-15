using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Если true — игрок неуязвим (урон не наносится, но эффекты играют)")]
    public bool invincible = true;

    public int maxHealth = 5;
    private int currentHealth;

    [Header("Неуязвимость после удара")]
    public float invulnerabilityTime = 0.5f;
    private float lastHitTime;

    [Header("Звуки и эффекты")]
    public AudioClip hurtSound;
    public GameObject hurtEffect;

    [Header("Мигание спрайта")]
    public Color flashColor = new Color(1f, 0.3f, 0.3f, 1f);
    public float flashDuration = 0.15f;

    private SpriteRenderer[] renderers;

    void Awake()
    {
        currentHealth = maxHealth;
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public void TakeDamage(int damage)
    {
        // Защита от спама урона подряд
        if (Time.time < lastHitTime + invulnerabilityTime) return;
        lastHitTime = Time.time;

        // Звук удара по игроку — играет всегда, даже если игрок неуязвим
        if (hurtSound != null)
            AudioSource.PlayClipAtPoint(hurtSound, transform.position);

        // Визуальный эффект
        if (hurtEffect != null)
            Instantiate(hurtEffect, transform.position, Quaternion.identity);

        StartCoroutine(FlashRed());

        // Если игрок неуязвим — HP не трогаем
        if (invincible)
        {
            Debug.Log($"[Player] Получен удар ({damage}), но игрок неуязвим.");
            return;
        }

        // Реальный урон (если когда-нибудь включишь invincible = false)
        currentHealth -= damage;
        Debug.Log($"[Player] HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            // тут можно добавить Die(), рестарт уровня и т.п.
            Debug.Log("Игрок погиб (заглушка).");
        }
    }

    private IEnumerator FlashRed()
    {
        if (renderers == null || renderers.Length == 0) yield break;

        Color[] original = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            original[i] = renderers[i].color;
            renderers[i].color = flashColor;
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].color = original[i];
    }
}