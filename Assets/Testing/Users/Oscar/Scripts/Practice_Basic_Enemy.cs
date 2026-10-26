using UnityEngine;

public class Practice_Basic_Enemy : Enemy_FSM
{
    private Animator m_animator;
    private bool frozen;
    
    void Start()
    {
        m_animator = GetComponent<Animator>();
        if(m_objective == null)
        {
            GameObject jugadorObj = GameObject.FindWithTag("Player");
            SetTarget(jugadorObj.transform);

        }
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
                m_animator.SetBool("IsAttacking", true);
                break;

        }
    }
}
