using System.Collections;
using System.Security.Cryptography;
using UnityEngine;

public class p_PlayerStats : MonoBehaviour, p_ITakeDamage, p_IHealDamage
{
    public static event System.Action<float> OnHealthChanged;
    public event System.Action<float> Damaged;

    public float maxHealth = 100f;
    public float PlayerDamage = 10f;
    private int collected;
    public int Collected
    {
        set
        {
            collected = collected + value > 3 ? collected = 3 : collected + value;
        }
        get { return collected;}
    }

    public float Health { get; private set; }

    void Start()
    {
        Health = maxHealth;
        OnHealthChanged?.Invoke(Health);
    }

    public void TakeDamage(float damage)
    {
        float previousHealth = Health;
        Health = Mathf.Max(Health - damage, 0f);
        OnHealthChanged?.Invoke(Health);
        if (Health < previousHealth)
            Damaged?.Invoke(previousHealth - Health);
    }

    public void ResetCollected()
    {
        collected = 0;
    }

    public void Heal(float healAmount)
    {
        Health = Mathf.Min(Health + healAmount, maxHealth);
        OnHealthChanged?.Invoke(Health);
    }

    void OnCollisionEnter(Collision collision)
    {
        p_ICanCollect obj = collision.gameObject.GetComponent<p_ICanCollect>();
        if (obj != null)
        {
            obj.Collect();

        }
            
    }
}
