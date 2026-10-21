using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class GraveSpawner : MonoBehaviour
{
    [SerializeField] private GravePool pool;
    [Tooltip("World-space square centered on this object, aligned with X/Z. Keep inside the walls.")]
    [SerializeField, Min(1f)] private float areaSize = 88f;
    [Tooltip("Side length of the grave footprint. pf_GraveSimple is 2 metres square.")]
    [SerializeField, Min(0.1f)] private float graveSize = 2f;
    [SerializeField, Min(0f)] private float spacing = 0.5f;
    [SerializeField, Min(1)] private int placementAttempts = 80;
    [Tooltip("Optional world-space exclusion near the entrance, centered on this transform.")]
    [SerializeField] private Transform entranceExclusion;
    [SerializeField] private Vector2 entranceClearSize = new Vector2(24f, 30f);
    [Tooltip("Optional fountain or landmark center. Graves stay outside this circular area.")]
    [SerializeField] private Transform reservedAreaCenter;
    [SerializeField, Min(0f)] private float reservedAreaRadius;
    [Tooltip("Obstacles only. Exclude the ground layer, otherwise every position will be blocked.")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField, Min(0.1f)] private float graveHeight = 1.65f;

    [Tooltip("Keep both central paths clear; zero disables the reserved cross.")]
    [SerializeField, Min(0f)] private float reservedCrossHalfWidth;

    [Header("Temporary spawn test")]
    [Tooltip("Press Space in the Game view to spawn graves without real enemies.")]
    [SerializeField] private bool enableSpaceTest = true;
    [SerializeField, Min(1)] private int testGravesPerPress = 20;

    private readonly List<TestEnemy> testEnemies = new List<TestEnemy>();

    // Each test grave needs its own identity to preserve the pool's one-enemy-per-grave rule.
    private sealed class TestEnemy : IGraveEnemy
    {
        public Grave Grave;
        public event Action Defeated { add { } remove { } }
    }

    private void Update()
    {
        if (!enableSpaceTest) return;
#if ENABLE_INPUT_SYSTEM
        bool pressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        bool pressed = Input.GetKeyDown(KeyCode.Space);
#else
        bool pressed = false;
#endif
        if (pressed) SpawnTestGraves();
    }

    private void SpawnTestGraves()
    {
        if (pool == null)
        {
            Debug.LogWarning("GraveSpawner: assign the Pool reference before testing.", this);
            return;
        }

        testEnemies.RemoveAll(enemy => enemy.Grave == null ||
            !ReferenceEquals(enemy.Grave.Enemy, enemy));
        int spawned = 0;
        for (int i = 0; i < testGravesPerPress; i++)
        {
            var enemy = new TestEnemy();
            if (!TrySpawn(enemy, out Grave grave)) break;
            enemy.Grave = grave;
            testEnemies.Add(enemy);
            spawned++;
        }

        if (spawned == 0)
            Debug.LogWarning("GraveSpawner: no test graves spawned. Check the prefab, pool capacity, " +
                "area size and Obstacle Layers (exclude the ground layer).", this);
        else
            Debug.Log($"GraveSpawner: spawned {spawned} test graves. Active: {pool.ActiveCount}.", this);
    }

    private void OnDisable()
    {
        foreach (var enemy in testEnemies)
            if (enemy.Grave != null && ReferenceEquals(enemy.Grave.Enemy, enemy))
                enemy.Grave.Release();
        testEnemies.Clear();
    }

    /// <summary>Call from the enemy spawning system with its IGraveEnemy implementation.</summary>
    public bool TrySpawn(IGraveEnemy enemy, out Grave grave)
    {
        grave = null;
        if (!isActiveAndEnabled || pool == null || enemy == null || pool.HasEnemy(enemy)) return false;
        float range = (areaSize - graveSize) * 0.5f;
        if (range < 0f) return false;

        for (int i = 0; i < placementAttempts; i++)
        {
            var position = transform.position + new Vector3(Random.Range(-range, range), 0f,
                Random.Range(-range, range));
            if (!IsPositionFree(position)) continue;
            // Fixed orientation preserves square spacing and faces the southern entrance.
            return pool.TryGet(position, Quaternion.identity, enemy, out grave);
        }
        return false;
    }

    private bool IsPositionFree(Vector3 position)
    {
        Vector3 local = position - transform.position;
        if (reservedCrossHalfWidth > 0f &&
            (Mathf.Abs(local.x) < reservedCrossHalfWidth + graveSize * .5f ||
             Mathf.Abs(local.z) < reservedCrossHalfWidth + graveSize * .5f)) return false;

        float separation = graveSize + spacing;
        foreach (var grave in pool.ActiveGraves)
        {
            if (grave == null) continue;
            Vector3 delta = grave.transform.position - position;
            if (Mathf.Abs(delta.x) < separation && Mathf.Abs(delta.z) < separation) return false;
        }

        if (entranceExclusion != null)
        {
            Vector3 delta = position - entranceExclusion.position;
            if (Mathf.Abs(delta.x) < (entranceClearSize.x + graveSize) * 0.5f &&
                Mathf.Abs(delta.z) < (entranceClearSize.y + graveSize) * 0.5f) return false;
        }

        if (reservedAreaCenter != null && reservedAreaRadius > 0f)
        {
            Vector3 delta = position - reservedAreaCenter.position;
            // Include the footprint's half-diagonal so its corners also stay clear.
            float radius = reservedAreaRadius + graveSize * 0.7071068f;
            if (delta.x * delta.x + delta.z * delta.z < radius * radius) return false;
        }

        var halfExtents = new Vector3(graveSize * 0.5f, graveHeight * 0.5f, graveSize * 0.5f);
        return !Physics.CheckBox(position + Vector3.up * (halfExtents.y + 0.01f), halfExtents,
            Quaternion.identity, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.12f);
        var size = new Vector3(areaSize, 0.1f, areaSize);
        Gizmos.DrawCube(transform.position, size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, size);
        if (reservedAreaCenter != null && reservedAreaRadius > 0f)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = reservedAreaCenter.position;
            const int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Gizmos.DrawLine(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reservedAreaRadius,
                    center + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * reservedAreaRadius);
            }
        }
        if (entranceExclusion != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(entranceExclusion.position,
                new Vector3(entranceClearSize.x, 0.2f, entranceClearSize.y));
        }
    }

    private void OnValidate()
    {
        reservedCrossHalfWidth = Mathf.Max(0f, reservedCrossHalfWidth);
        areaSize = Mathf.Max(1f, areaSize);
        graveSize = Mathf.Max(0.1f, graveSize);
        spacing = Mathf.Max(0f, spacing);
        placementAttempts = Mathf.Max(1, placementAttempts);
        graveHeight = Mathf.Max(0.1f, graveHeight);
        testGravesPerPress = Mathf.Max(1, testGravesPerPress);
        entranceClearSize.x = Mathf.Max(0f, entranceClearSize.x);
        entranceClearSize.y = Mathf.Max(0f, entranceClearSize.y);
        reservedAreaRadius = Mathf.Max(0f, reservedAreaRadius);
    }
}
