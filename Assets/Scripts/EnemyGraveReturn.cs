using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

/// <summary>Sends the enemy supplied by a GameEvent to a random reachable grave.</summary>
[DisallowMultipleComponent]
public sealed class EnemyGraveReturn : MonoBehaviour
{
    [Tooltip("Scene transforms beside graves, on walkable ground. Works with permanent and pooled graves.")]
    [SerializeField] private Transform[] gravePoints = new Transform[0];
    [Tooltip("Maximum distance from a grave point to a walkable NavMesh position.")]
    [SerializeField, Min(0.01f)] private float sampleRadius = 2f;
    [Header("Arrival disappearance")]
    [Tooltip("Seconds spent sinking into the ground after reaching the grave.")]
    [SerializeField, Min(0.01f)] private float disappearanceDuration = 1f;
    [Tooltip("World-space distance to sink before deactivating the enemy.")]
    [SerializeField, Min(0.01f)] private float sinkDepth = 3f;

    private readonly List<Transform> candidates = new List<Transform>();
    private readonly List<GraveJourney> journeys = new List<GraveJourney>();
    private AudioSource audioSource;

    private sealed class GraveJourney
    {
        public Enemy_FSM enemy;
        public NavMeshAgent agent;
        public Vector3 destination;
        public bool sinking;
        public float elapsed;
        public Vector3 arrivalPosition;
        public Collider[] colliders;
        public bool[] colliderStates;
        public Animator[] animators;
        public bool[] animatorStates;
        public Rigidbody[] bodies;
        public bool[] kinematicStates;
    }

    /// <summary>Connect GameEventListener.ResponseWithTarget to this method (Dynamic GameObject).</summary>
    public void SendEnemyToRandomGrave(GameObject target)
    {
        if (!isActiveAndEnabled || target == null) return;

        // Offering collisions may come from a child collider of the enemy.
        Enemy_FSM enemy = target.GetComponentInParent<Enemy_FSM>();
        if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsReturningToGrave) return;

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Debug.LogWarning("EnemyGraveReturn: the enemy needs an active agent on a baked NavMesh.", target);
            return;
        }

        candidates.Clear();
        if (gravePoints != null)
        {
            foreach (Transform point in gravePoints)
                if (point != null && point.gameObject.activeInHierarchy && !candidates.Contains(point))
                    candidates.Add(point);
        }

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        while (candidates.Count > 0)
        {
            // Try each point at most once in random order, skipping unreachable graves.
            int index = Random.Range(0, candidates.Count);
            Transform point = candidates[index];
            candidates[index] = candidates[candidates.Count - 1];
            candidates.RemoveAt(candidates.Count - 1);

            if (NavMesh.SamplePosition(point.position, out NavMeshHit hit, Mathf.Max(0.01f, sampleRadius), filter)
                && enemy.TryReturnToGrave(hit.position))
            {
                // A return may have been cancelled and restarted before our next LateUpdate.
                journeys.RemoveAll(journey => journey.enemy == enemy);
                journeys.Add(new GraveJourney { enemy = enemy, agent = agent, destination = hit.position });
                return;
            }
        }

        Debug.LogWarning("EnemyGraveReturn: no active, reachable grave point is assigned. " +
            "Place Grave Points beside the graves and bake the NavMesh.", this);
    }

    private void LateUpdate()
    {
        for (int i = journeys.Count - 1; i >= 0; i--)
        {
            GraveJourney journey = journeys[i];
            if (journey.enemy == null || !journey.enemy.gameObject.activeInHierarchy ||
                !journey.enemy.IsReturningToGrave || journey.agent == null)
            {
                Restore(journey);
                journeys.RemoveAt(i);
                continue;
            }

            if (!journey.sinking)
            {
                if (!journey.enemy.isActiveAndEnabled || !journey.agent.isActiveAndEnabled || !journey.agent.isOnNavMesh)
                {
                    journeys.RemoveAt(i);
                    continue;
                }

                // Observe the existing FSM after Update; it resets the path and stops on arrival.
                // A cancelled or incomplete path must never cause a premature disappearance.
                Vector3 offset = journey.agent.nextPosition - journey.destination;
                offset.y = 0f;
                float arrivalDistance = Mathf.Max(journey.agent.stoppingDistance, 0.1f) + 0.05f;
                if (journey.agent.pathPending || journey.agent.hasPath || !journey.agent.isStopped ||
                    offset.sqrMagnitude > arrivalDistance * arrivalDistance) continue;

                BeginDisappearance(journey);
            }

            journey.elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(journey.elapsed / Mathf.Max(0.01f, disappearanceDuration));
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            journey.enemy.transform.position = journey.arrivalPosition - Vector3.up * (Mathf.Max(0.01f, sinkDepth) * eased);
            if (progress < 1f) continue;

            journey.enemy.gameObject.SetActive(false);
            // Restore while inactive so a future spawn does not inherit disabled collisions or a sunken pose.
            Restore(journey);
            journey.enemy.ResumePatrol();
            journeys.RemoveAt(i);
        }
    }

    private void BeginDisappearance(GraveJourney journey)
    {
        PlayWithRandomPitch();
        journey.sinking = true;
        journey.arrivalPosition = journey.enemy.transform.position;
        journey.enemy.StopAllCoroutines();
        journey.enemy.enabled = false;
        journey.agent.enabled = false;

        journey.colliders = journey.enemy.GetComponentsInChildren<Collider>(true);
        journey.colliderStates = new bool[journey.colliders.Length];
        for (int i = 0; i < journey.colliders.Length; i++)
        {
            journey.colliderStates[i] = journey.colliders[i].enabled;
            journey.colliders[i].enabled = false;
        }

        journey.animators = journey.enemy.GetComponentsInChildren<Animator>(true);
        journey.animatorStates = new bool[journey.animators.Length];
        for (int i = 0; i < journey.animators.Length; i++)
        {
            journey.animatorStates[i] = journey.animators[i].enabled;
            journey.animators[i].enabled = false;
        }

        journey.bodies = journey.enemy.GetComponentsInChildren<Rigidbody>(true);
        journey.kinematicStates = new bool[journey.bodies.Length];
        for (int i = 0; i < journey.bodies.Length; i++)
        {
            journey.kinematicStates[i] = journey.bodies[i].isKinematic;
            journey.bodies[i].isKinematic = true;
        }
    }

    private static void Restore(GraveJourney journey)
    {
        if (!journey.sinking || journey.enemy == null) return;
        journey.enemy.transform.position = journey.arrivalPosition;
        for (int i = 0; i < journey.colliders.Length; i++)
            if (journey.colliders[i] != null) journey.colliders[i].enabled = journey.colliderStates[i];
        for (int i = 0; i < journey.animators.Length; i++)
            if (journey.animators[i] != null) journey.animators[i].enabled = journey.animatorStates[i];
        for (int i = 0; i < journey.bodies.Length; i++)
            if (journey.bodies[i] != null) journey.bodies[i].isKinematic = journey.kinematicStates[i];
        if (journey.agent != null) journey.agent.enabled = true;
        journey.enemy.enabled = true;
        journey.sinking = false;
    }

    private void OnDisable()
    {
        // Cancelling this manager must not leave an active enemy without navigation or collisions.
        foreach (GraveJourney journey in journeys) Restore(journey);
        journeys.Clear();
    }
    public void PlayWithRandomPitch()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(audioSource.clip);
    }
}
