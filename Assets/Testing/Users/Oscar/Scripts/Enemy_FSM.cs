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
        ATTACK,
        HIT,
        RETURNING_TO_GRAVE
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
    private bool hasReachedGrave;
    private Vector3 graveDestination;
    [SerializeField] private LayerMask m_playerMask;
    [SerializeField] private LayerMask m_obstacleMask;
    [SerializeField] private LayerMask m_AIGroundMask;
    [SerializeField] float countdownAttack =1f;
    public GameEvent AttackingPlayer;
    public bool triggerAttackAnim =false ;
    

    public bool IsReturningToGrave => m_enemyState == EnemyState.RETURNING_TO_GRAVE;

    private void Awake()
    {
        m_agent = GetComponent<NavMeshAgent>();
        m_enemyState = EnemyState.PATROL;
        SetNewPatrolPoint();
        m_attackTrigger.SetActive(false);
        m_PatrolPositionDefault = new Vector3(0, 0, 0);
    }
    public void SetTarget(Transform targetTrans)
    {
        m_objective = targetTrans;
    }
    protected void EnemyFSM_Update()
    {
        if (IsReturningToGrave)
        {
            ReturnToGraveState();
            return;
        }

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

    /// <summary>Accepts only complete paths; a failed request leaves the current AI behavior intact.</summary>
    public bool TryReturnToGrave(Vector3 destination)
    {
        if (!isActiveAndEnabled || IsReturningToGrave || m_agent == null ||
            !m_agent.isActiveAndEnabled || !m_agent.isOnNavMesh) return false;

        var path = new NavMeshPath();
        if (!m_agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete ||
            !m_agent.SetPath(path)) return false;

        // An attack coroutine can otherwise reactivate its hitbox or overwrite the state later.
        StopAllCoroutines();
        m_isAttacking = false;
        m_isResting = false;
        if (m_attackTrigger != null) m_attackTrigger.SetActive(false);
        hasReachedGrave = false;
        graveDestination = destination;
        m_enemyState = EnemyState.RETURNING_TO_GRAVE;
        m_agent.isStopped = false;
        return true;
    }

    private void ReturnToGraveState()
    {
        if (hasReachedGrave || m_agent == null || !m_agent.isActiveAndEnabled || !m_agent.isOnNavMesh ||
            m_agent.pathPending) return;

        float arrivalDistance = Mathf.Max(m_agent.stoppingDistance, 0.1f);
        Vector3 offset = m_agent.nextPosition - graveDestination;
        offset.y = 0f;
        // Unity can clear hasPath on arrival before this update observes the final distance.
        bool arrived = m_agent.hasPath
            ? m_agent.pathStatus == NavMeshPathStatus.PathComplete && m_agent.remainingDistance <= arrivalDistance
            : offset.sqrMagnitude <= arrivalDistance * arrivalDistance;
        if (arrived)
        {
            hasReachedGrave = true;
            m_agent.ResetPath();
            m_agent.isStopped = true;
        }
        else if (!m_agent.hasPath || m_agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            ResumePatrol();
        }
        // Stay at the grave until ResumePatrol is explicitly called.
    }

    public void ResumePatrol()
    {
        if (!IsReturningToGrave) return;
        if (m_agent != null && m_agent.isActiveAndEnabled && m_agent.isOnNavMesh)
        {
            m_agent.ResetPath();
            m_agent.isStopped = false;
        }
        m_patrolPosition = m_PatrolPositionDefault;
        m_enemyState = EnemyState.PATROL;
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

        // Dibuja el radio de visi�n
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, m_sightRadious);

        // Dibuja las l�neas del �ngulo de visi�n
        Vector3 leftLimit = Quaternion.Euler(0, -m_sightAngle, 0) * transform.forward;
        Vector3 rightLimit = Quaternion.Euler(0, m_sightAngle, 0) * transform.forward;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftLimit * m_sightRadious);
        Gizmos.DrawLine(transform.position, transform.position + rightLimit * m_sightRadious);
    }

    private void IsObjectiveOnSight(){

        if (m_objective == null)
    {
        m_objectiveOnSight = false;
        return;
    }
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
                //Debug.Log("Viendo al jugador");
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

        // Calcula un camino desde la posici�n actual hasta el target
        if (m_agent.CalculatePath(p_targetPosition, path))
        {
            // Checa si la ruta est� completa
            return path.status == NavMeshPathStatus.PathComplete;
        }

        return false;
    }

    IEnumerator AttackPlayer(float p_distanceToPlayer)
    {
        m_agent.SetDestination(transform.position);
        m_isAttacking = true;

        // Se lanza el evento 1 sola vez al iniciar el ataque
        AttackingPlayer.Raise();
        triggerAttackAnim = true;

        // Espera antes de activar el collider de ataque
        yield return new WaitForSeconds(m_WaitAttackTime);

        // Activa el trigger un breve momento para registrar el golpe
        m_attackTrigger.SetActive(true);
        yield return new WaitForSeconds(m_attackTime - m_WaitAttackTime);
        m_attackTrigger.SetActive(false);

        // Cooldown para evitar ataques continuos
        yield return new WaitForSeconds(countdownAttack);

        m_isAttacking = false;

        // Decide el siguiente estado dependiendo de la distancia del jugador
        if (p_distanceToPlayer > m_attackRadious)
        {
            m_enemyState = EnemyState.PATROL;
        }
        else
        {
            m_enemyState = EnemyState.CHASE;
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
