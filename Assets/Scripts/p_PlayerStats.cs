using System.Collections;
using UnityEngine;

public class p_PlayerStats : MonoBehaviour, p_ITakeDamage, p_IHealDamage
{
    public static event System.Action<float> OnHealthChanged;
    public static event System.Action<bool> OnPlayerReload;
    public static event System.Action<int, int> OnAmmoChanged;

    public float maxHealth = 100f;
    public float PlayerDamage = 10f;
    public int bulletBounce = 2;
    public int maxBullets = 5;
    public float reloadTime = 4f;

    public float Health { get; private set; }

    private int currentBullets;
    public int CurrentBullets
    {
        get { return currentBullets; }
        set
        {
            currentBullets = value;
            OnAmmoChanged?.Invoke(currentBullets, maxBullets);

            if (currentBullets == 0)
                StartCoroutine(Reload());
        }
    }

    void Start()
    {
        Health = maxHealth;
        currentBullets = maxBullets;
        OnHealthChanged?.Invoke(Health);
        OnAmmoChanged?.Invoke(currentBullets, maxBullets);
    }

    public void TakeDamage(float damage)
    {
        Health = Mathf.Max(Health - damage, 0f);
        OnHealthChanged?.Invoke(Health);
    }

    public void Heal(float healAmount)
    {
        Health = Mathf.Min(Health + healAmount, maxHealth);
        OnHealthChanged?.Invoke(Health);
    }

    IEnumerator Reload()
    {
        OnPlayerReload?.Invoke(true);
        yield return new WaitForSeconds(reloadTime);
        currentBullets = maxBullets;
        OnPlayerReload?.Invoke(false);
        OnAmmoChanged?.Invoke(currentBullets, maxBullets);
    }
}