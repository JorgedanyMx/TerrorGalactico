using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class Enemy_FSM : MonoBehaviour
{
    public enum EnemyState
    {
        NONE,
        PATROL,
        CHASE,
        ALTARED,
        ATTACK
    }


    [SerializeField] protected Transform m_objective;
    protected NavMeshAgent m_agent;
    [SerializeField] protected EnemyState m_enemyState;

    [SerializeField] private bool m_objectiveOnSight;
    [SerializeField] protected Vector3 m_patrolPosition;
    protected Vector3 m_PatrolPositionDefault;
    [SerializeField] GameObject m_attackTrigger;
    [SerializeField] float m_sightRadious;
    [SerializeField] float m_sightAngle;
    [SerializeField] float m_patrolRadious;
    [SerializeField] float m_attackRadious;
    [SerializeField] float m_newPatrolPointDistance;
    [SerializeField] float m_restTime;
    [SerializeField] float m_attackTime;
    [SerializeField] float m_WaitAttackTime;
    bool m_isResting = false;
    bool m_isAttacking = false;
    [SerializeField] private LayerMask m_playerMask;
    [SerializeField] private LayerMask m_obstacleMask;
    [SerializeField] private LayerMask m_AIGroundMask;
    

    private void Awake()
    {
        m_agent = GetComponent<NavMeshAgent>();
        m_enemyState = EnemyState.PATROL;
        SetNewPatrolPoint();
        m_attackTrigger.SetActive(false);
        m_PatrolPositionDefault = new Vector3(0, 0, 0);
    }

    protected void EnemyFSM_Update()
    {
        IsObjectiveOnSight();
        switch (m_enemyState)
        {
            case EnemyState.NONE:
                m_enemyState = EnemyState.PATROL;
                break;
            case EnemyState.PATROL:
                PatrolState();
                break;
            case EnemyState.CHASE:
                ChaseState();
                break;
            case EnemyState.ALTARED:
                AlteredState();
                
                break;
            case EnemyState.ATTACK:
                AttackState();
                break;
        }
        
    }

    #region StateFunctions
    private void PatrolState()
    {
        if (m_objectiveOnSight){
            m_enemyState = EnemyState.CHASE;
        }
        else{
            if(m_patrolPosition != null && m_patrolPosition != m_PatrolPositionDefault){
                m_agent.SetDestination(m_patrolPosition);
                Vector3 p_distanceToPatrolPoint = transform.position - m_patrolPosition;
                if(p_distanceToPatrolPoint.magnitude < m_newPatrolPointDistance && !m_isResting){
                    StartCoroutine(PlayerRest());
                }
            }
            else{
                SetNewPatrolPoint();
            }
        }
    }

    private void ChaseState()
    {
        if (m_objectiveOnSight){
            m_agent.SetDestination(m_objective.position);
            Vector3 p_distanceToPlayer = transform.position - m_objective.position;
            if( p_distanceToPlayer.magnitude < m_attackRadious)
            {
                m_enemyState = EnemyState.ATTACK;
            }
        }
        else{
            LastKnownLocation();
        }
    }

    public virtual void AlteredState()
    {
        
    }

    private void AttackState(){
        if (!m_isAttacking){
            float p_distanceToPlayer = Vector3.Distance(transform.position, m_objective.position);
            
            StartCoroutine(AttackPlayer(p_distanceToPlayer));
        }
        
    }

    #endregion StateFunctions

    #region PrivateFunctions
    private void OnDrawGizmos()
    {
        //Dibuja el radio de ataque
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, m_attackRadious);

        // Dibuja el radio de visión
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, m_sightRadious);

        // Dibuja las líneas del ángulo de visión
        Vector3 leftLimit = Quaternion.Euler(0, -m_sightAngle, 0) * transform.forward;
        Vector3 rightLimit = Quaternion.Euler(0, m_sightAngle, 0) * transform.forward;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftLimit * m_sightRadious);
        Gizmos.DrawLine(transform.position, transform.position + rightLimit * m_sightRadious);
    }

    private void IsObjectiveOnSight(){
        Collider[] p_playerOnSight = Physics.OverlapSphere(transform.position, m_sightRadious, m_playerMask);

        if(p_playerOnSight.Length <= 0){
            m_objectiveOnSight = false;
            return;
        }

        Vector3 p_directionToPlayer = (m_objective.position - transform.position).normalized;
        

        if(Vector3.Angle(transform.forward, p_directionToPlayer) < m_sightAngle){
            float p_distanceToPlayer = Vector3.Distance(transform.position, m_objective.position);
            if (!Physics.Raycast(transform.position, p_directionToPlayer, p_distanceToPlayer, m_obstacleMask)){
                //CheckForObstacles();
                m_objectiveOnSight = true;
            }
            else{
                m_objectiveOnSight = false;
            }
        }

    }

    private void SetNewPatrolPoint(){
        float p_randomX = Random.Range(-m_patrolRadious, m_patrolRadious);
        float p_randomZ = Random.Range(-m_patrolRadious, m_patrolRadious);

        m_patrolPosition = new Vector3(transform.position.x + p_randomX,
            transform.position.y, transform.position.z + p_randomZ);

        if (!Physics.Raycast(m_patrolPosition, -transform.up, 5f, m_AIGroundMask) || !IsWalkableArea(m_patrolPosition)){
            m_patrolPosition = m_PatrolPositionDefault;
        }
        
    }

    private void LastKnownLocation()
    {
        Vector3 p_lastKnownLocation = new Vector3(m_objective.position.x, m_objective.position.y, m_objective.position.z);
        m_patrolPosition = p_lastKnownLocation;
        m_enemyState = EnemyState.PATROL;
    }

    private bool IsWalkableArea(Vector3 p_targetPosition)
    {
        NavMeshPath path = new NavMeshPath();

        // Calcula un camino desde la posición actual hasta el target
        if (m_agent.CalculatePath(p_targetPosition, path))
        {
            // Checa si la ruta está completa
            return path.status == NavMeshPathStatus.PathComplete;
        }

        return false;
    }

    IEnumerator AttackPlayer(float p_distanceToPlayer)
    {
        Debug.Log("atacado");
        m_agent.SetDestination(transform.position);
        m_isAttacking = true;
        yield return new WaitForSeconds(m_WaitAttackTime);
        m_attackTrigger.SetActive(true);
        yield return new WaitForSeconds(m_attackTime - m_WaitAttackTime);
        m_attackTrigger.SetActive(false);
        m_isAttacking = false;
        if (p_distanceToPlayer > m_attackRadious)
        {
            m_enemyState = EnemyState.PATROL;
        }
        
    }

    IEnumerator PlayerRest()
    {
        m_isResting = true;
        yield return new WaitForSeconds(m_restTime);
        m_isResting = false;
        SetNewPatrolPoint();
    }


    #endregion PrivateFunctions
}
