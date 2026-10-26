using System.Collections;
using System.Security.Cryptography;
using UnityEngine;

public class p_PlayerStats : MonoBehaviour, p_ITakeDamage, p_IHealDamage, p_ICanCollect
{
    public static event System.Action<float> OnHealthChanged;

    public float maxHealth = 100f;
    public float PlayerDamage = 10f;
    private int collected;
    public int Collected
    {
        get { return collected; }
        set { collected = 0; }
    }

    public float Health { get; private set; }

    void Start()
    {
        Health = maxHealth;
        OnHealthChanged?.Invoke(Health);
    }

    public void TakeDamage(float damage)
    {
        Health = Mathf.Max(Health - damage, 0f);
        OnHealthChanged?.Invoke(Health);
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

    public void Collect()
    {
        if (collected == 3)
            return;
        collected++;
    }



    // void OnCollisionEnter(Collision collision)
    // {
    //     p_ICanCollect obj = collision.gameObject.GetComponent<p_ICanCollect>();
    //     if (obj != null)
    //     {
    //         obj.Collect();

    //     }
            
    // }
}