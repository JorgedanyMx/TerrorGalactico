using UnityEngine;

public class p_ShootLogic : MonoBehaviour
{
    public float bulletSpeed = 15f;
    public float bulletLifetime = 4f;

    public void Shoot(Vector3 from, Vector3 to, float damage, GameObject bulletPrefab, GameObject shooter, int bounce = 0)
    {
        
        Vector3 direction = to - from;
        direction.y = 0;
        direction.Normalize();

        GameObject bullet = Instantiate(bulletPrefab, from, Quaternion.LookRotation(direction));

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
    }
}