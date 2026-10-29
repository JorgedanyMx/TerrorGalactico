using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class p_PlayerStats : MonoBehaviour, p_ITakeDamage, p_IHealDamage, p_ICanCollect
{
    public static event System.Action<float> OnHealthChanged;

    public float maxHealth = 100f;
    public float PlayerDamage = 10f;
    private int collected;
    public GameObject final;
    private bool canTakeDamage = true; // Controla si puede recibir daño
    private AudioSource audioSource;
    public AudioClip Damageclip;
    public AudioClip TakeItem;
    private bool playerIsDead = false;
    public GameEvent setDead;
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
        if(playerIsDead) return;
        // Si no puede recibir daño todavía, salimos de la función
        // countdown damaaaaaage---- if (!canTakeDamage) return;
        PlayWithRandomPitch(Damageclip);
        Health = Mathf.Max(Health - damage, 0f);

        // Nota: Asumo que en tu lógica original si la salud es 0 o menor muere, 
        // revisa si querías decir (Health <= 0) en lugar de (Health >= 0).
        if (Health <= 0) 
        {
            IsDead();
        }

        OnHealthChanged?.Invoke(Health);

        // Iniciamos el periodo de invulnerabilidad / espera de medio segundo
        // countdown damaaaaaage----StartCoroutine(DamageCooldown());
    }

    private IEnumerator DamageCooldown()
    {
        canTakeDamage = false;

        // Espera medio segundo (0.5 segundos)
        yield return new WaitForSeconds(0.5f);

        canTakeDamage = true;
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
    public void IsDead()
    {
        playerIsDead = true;
        Debug.Log("Se murio");
        setDead.Raise();
        StartCoroutine(EnableDeadScreen());
    }
    private IEnumerator EnableDeadScreen()
    {
        
        yield return new WaitForSeconds(2f);
        final.SetActive(true);
        StartCoroutine(WaitAndReloadScene());

        SceneManager.LoadScene(0);
    }
    private IEnumerator WaitAndReloadScene()
    {
        // Espera 10 segundos exactos (afectado por el Time.timeScale)
        yield return new WaitForSeconds(5f);

        // Si prefieres que ignore si el juego está en pausa, usa:
        // yield return new WaitForSecondsRealtime(10f);

        // Recarga la escena actual
        SceneManager.LoadScene(0);
    }
    public void PlayWithRandomPitch(AudioClip audio)
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(audio);
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