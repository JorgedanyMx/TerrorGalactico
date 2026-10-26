using UnityEngine;

public class p_BulletStats : MonoBehaviour
{
    public float damage;
    public GameObject shooter;
    public int bounce;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(shooter.tag) || other.CompareTag("bullet"))
            return;

        if (other.TryGetComponent(out p_ITakeDamage damageable))
        {
            damageable.TakeDamage(damage);
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}