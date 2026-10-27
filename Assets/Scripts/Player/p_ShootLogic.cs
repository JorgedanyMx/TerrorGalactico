using UnityEngine;

public class p_ShootLogic : MonoBehaviour
{
    public float bulletSpeed = 15f;
    public float bulletLifetime = 4f;

    [Header("Cadencia de Disparo")]
    [Tooltip("Tiempo de espera (en segundos) entre cada disparo.")]
    public float shootDelay = 0.2f;
    private float nextShootTime = 0f;

    [Header("Agrega sondio disparo")]
    public AudioClip ShootSFX;
    public AudioClip OfferSFX;
    private AudioSource audioSource;
    public GameEvent OfferLaunched;

    /// <summary>
    /// Intenta disparar un proyectil. Devuelve true si se disparó con éxito, false si está en cooldown.
    /// </summary>
    public bool Shoot(Vector3 from, Vector3 to, float damage, GameObject bulletPrefab, GameObject shooter, int bounce = 0, bool isOffer=false)
    {
        // Verifica si todavía estamos en el tiempo de espera (cooldown)
        if (Time.time < nextShootTime)
        {
            return false; // No se puede disparar todavía
        }

        // Actualiza el momento en el que se permitirá el próximo disparo
        nextShootTime = Time.time + shootDelay;

        Vector3 direction = to - from;
        direction.y = 0;
        direction.Normalize();

        GameObject bullet = Instantiate(bulletPrefab, from, Quaternion.LookRotation(direction));
        if (isOffer)
        {
            PlayWithRandomPitch(OfferSFX);
            OfferLaunched.Raise();
        }
        else
        {
            PlayWithRandomPitch(ShootSFX);

        }

        p_BulletStats stats = bullet.GetComponent<p_BulletStats>();
        stats.damage = damage;
        stats.shooter = shooter;
        stats.bounce = bounce;

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb == null)
            rb = bullet.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearVelocity = direction * bulletSpeed;

        Destroy(bullet, bulletLifetime);

        return true; // Disparo exitoso
    }
    public void PlayWithRandomPitch(AudioClip clip)
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        audioSource.pitch = Random.Range(0.8f, 1.2f);
        audioSource.PlayOneShot(clip);
    }
}