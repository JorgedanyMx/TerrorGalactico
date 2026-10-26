using UnityEngine;
using UnityEngine.Audio;

public class Practice_Basic_Enemy : Enemy_FSM, p_ITakeDamage
{
    public Animator m_animator;
    private bool frozen;
    public float Health = 100f;
    public AudioClip damagedAClip;
    private AudioSource m_audioSource;
    private bool m_hasAttackTrigger;
    void Start()
    {
        //m_animator = GetComponent<Animator>();
        if (m_animator != null)
            foreach (var parameter in m_animator.parameters)
                if (parameter.name == "Attack" && parameter.type == AnimatorControllerParameterType.Trigger)
                    m_hasAttackTrigger = true;
        GameObject jugadorObj = GameObject.FindWithTag("Player");
        SetTarget(jugadorObj.transform);
    }

    private void OnEnable()
    {
        if (!this.enabled)
        {
            this.enabled = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        EnemyFSM_Update();
        AnimationManager();
    }

    public override void AlteredState()
    {
        if (frozen) return;

        frozen = true;

        m_enemyState = EnemyState.ALTARED;
    }

    private void AnimationManager()
    {
        float p_speed;
        p_speed = m_agent.velocity.magnitude;
        switch(m_enemyState)
        {
            case EnemyState.PATROL:
            case EnemyState.RETURNING_TO_GRAVE:
                m_animator.SetFloat("Speed", p_speed, 0.1f, Time.deltaTime);
                m_animator.SetBool("IsAttacking", false);
                break;
            case EnemyState.CHASE:
                m_animator.SetFloat("Speed", p_speed, 0.1f, Time.deltaTime);
                m_animator.SetBool("IsAttacking", false);
                break;
            case EnemyState.ATTACK:
                //animator.SetBool("Walking", false);
                //m_animator.SetTrigger("Attacking");
                //m_animator.SetBool("IsAttacking", true);
                if (m_hasAttackTrigger) m_animator.SetTrigger("Attack");
                break;

        }
    }
    public void TakeDamage(float damage)
    {
        PlayWithRandomPitch(damagedAClip);
        Health = Mathf.Max(Health - damage, 0f);
        if (Health <= 0f)
        {
            Destroy(gameObject);
        }
    }
    public void PlayWithRandomPitch(AudioClip audio)
    {
        if (audio == null) return;
        if (m_audioSource == null) m_audioSource = GetComponent<AudioSource>();
        if (m_audioSource == null) m_audioSource = gameObject.AddComponent<AudioSource>();
        m_audioSource.pitch = Random.Range(0.9f, 1.1f);
        m_audioSource.PlayOneShot(audio);
    }
}
