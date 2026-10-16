using System.Collections;
using UnityEngine;

/// <summary>
/// Cycle: wait a random time -> the spawner appears at a random point of what the camera sees,
/// drops the pieces inside that area -> stays a few seconds -> disappears -> repeat.
/// Works with orthographic (isometric) and perspective cameras: random viewport points are
/// projected onto a horizontal ground plane.
/// Put it on an empty GameObject in the scene (the "appearing" thing is the optional Spawner Visual prefab).
/// </summary>
public class CameraAreaSpawner : MonoBehaviour
{
    [Header("What to spawn")]
    [Tooltip("Pieces the player has to collect. One is picked at random for each drop.")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private int piecesPerAppearance = 3;
    [Tooltip("Optional. The thing that appears in the area, drops the pieces and then disappears.")]
    [SerializeField] private GameObject spawnerVisual;

    [Header("Timing (seconds)")]
    [SerializeField] private bool autoSpawn = true;
    [Tooltip("If on, the first appearance happens right away (handy for testing) instead of waiting the interval.")]
    [SerializeField] private bool appearOnStart = true;
    [Tooltip("Random wait between appearances: x = min, y = max.")]
    [SerializeField] private Vector2 intervalRange = new Vector2(30f, 60f);
    [Tooltip("How long the spawner stays after dropping the pieces: x = min, y = max.")]
    [SerializeField] private Vector2 stayRange = new Vector2(2f, 3f);

    [Header("Pieces")]
    [Tooltip("Pieces land at a random spot within this distance of the spawner.")]
    [SerializeField] private float dropRadius = 4f;
    [Tooltip("Pieces never land closer than this to the spawner (use about half the spawner's width). 0 = no limit.")]
    [SerializeField] private float minDropDistance = 1f;
    [Tooltip("Height of the piece's pivot above the floor (half its size if the pivot is centered).")]
    [SerializeField] private float pieceHeightOffset = 0.1f;
    [Tooltip("Seconds a piece stays before shrinking away if nobody collects it.")]
    [SerializeField] private float pieceLifetime = 10f;
    [SerializeField] private float pieceScaleTime = 0.3f;

    [Header("Spawner animation")]
    [Tooltip("The spawner starts this far below the floor and rises up.")]
    [SerializeField] private float riseDepth = 2f;
    [SerializeField] private float riseTime = 0.5f;

    [Header("Area (camera view)")]
    [Tooltip("Defaults to Camera.main when empty.")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Margin from the screen edges, 0 = full screen, 0.5 = center point only.")]
    [SerializeField, Range(0f, 0.45f)] private float viewportMargin = 0.1f;
    [Tooltip("Optional. If set (e.g. the floor plane), points outside its bounds are rejected, so the spawn area is the camera view clipped to the floor.")]
    [SerializeField] private Collider limitToCollider;
    [Tooltip("World Y of the plane objects are dropped on.")]
    [SerializeField] private float groundHeight = 0f;
    [Tooltip("Extra height above the ground so the object does not clip into it.")]
    [SerializeField] private float spawnHeightOffset = 0.5f;

    [Header("Placement")]
    [Tooltip("Spawn points overlapping these layers are rejected (walls, props...).")]
    [SerializeField] private LayerMask blockedLayers;
    [SerializeField] private float clearanceRadius = 0.5f;
    [SerializeField] private int maxPlacementAttempts = 10;

    private Coroutine cycle;
    private GameObject currentVisual;

    private Camera Cam => targetCamera != null ? targetCamera : (targetCamera = Camera.main);

    private void OnEnable()
    {
        if (autoSpawn) cycle = StartCoroutine(Cycle());
    }

    private void OnDisable()
    {
        if (cycle != null) StopCoroutine(cycle);
        cycle = null;
        if (currentVisual != null) Destroy(currentVisual);
    }

    /// <summary>Starts or stops the automatic cycle (callable from UnityEvents).</summary>
    public void SetAutoSpawn(bool value)
    {
        autoSpawn = value;
        if (!isActiveAndEnabled) return;

        if (cycle != null) StopCoroutine(cycle);
        cycle = value ? StartCoroutine(Cycle()) : null;
    }

    /// <summary>Runs one appearance right now, ignoring the interval.</summary>
    public void AppearNow()
    {
        StartCoroutine(Appearance());
    }

    private IEnumerator Cycle()
    {
        if (!appearOnStart) yield return new WaitForSeconds(RandomIn(intervalRange));

        while (true)
        {
            yield return Appearance();
            yield return new WaitForSeconds(RandomIn(intervalRange));
        }
    }

    private IEnumerator Appearance()
    {
        if (!TryGetRandomPoint(out Vector3 spawnerPoint)) yield break;

        // The visual stands on the floor, not at the floating drop height.
        Vector3 standPoint = spawnerPoint - Vector3.up * spawnHeightOffset;
        Vector3 hiddenPoint = standPoint - Vector3.up * riseDepth;

        if (spawnerVisual != null)
        {
            currentVisual = Instantiate(spawnerVisual, hiddenPoint, Quaternion.identity);
            yield return MoveTo(currentVisual.transform, hiddenPoint, standPoint, riseTime);
        }

        DropPieces(spawnerPoint);

        yield return new WaitForSeconds(RandomIn(stayRange));

        if (currentVisual != null)
        {
            yield return MoveTo(currentVisual.transform, standPoint, hiddenPoint, riseTime);
            Destroy(currentVisual);
            currentVisual = null;
        }
    }

    private IEnumerator MoveTo(Transform target, Vector3 from, Vector3 to, float duration)
    {
        for (float t = 0f; t < duration && target != null; t += Time.deltaTime)
        {
            target.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        if (target != null) target.position = to;
    }

    private void DropPieces(Vector3 center)
    {
        if (prefabs == null || prefabs.Length == 0) return;

        for (int i = 0; i < piecesPerAppearance; i++)
        {
            if (!TryGetPointAround(center, out Vector3 point)) continue;

            GameObject piece = Instantiate(prefabs[Random.Range(0, prefabs.Length)], point, Quaternion.identity);
            piece.AddComponent<PieceLifecycle>().Begin(pieceLifetime, pieceScaleTime, pieceScaleTime);
        }
    }

    private bool TryGetPointAround(Vector3 center, out Vector3 point)
    {
        point = default;
        for (int i = 0; i < Mathf.Max(1, maxPlacementAttempts); i++)
        {
            // Uniform point in the ring between minDropDistance and dropRadius.
            float inner = Mathf.Clamp(minDropDistance, 0f, dropRadius);
            float radius = Mathf.Sqrt(Random.Range(inner * inner, dropRadius * dropRadius));
            Vector2 offset = Random.insideUnitCircle.normalized * radius;
            Vector3 candidate = new Vector3(center.x + offset.x, groundHeight + pieceHeightOffset, center.z + offset.y);

            if (limitToCollider != null && !IsInsideXZ(limitToCollider.bounds, candidate)) continue;
            if (blockedLayers.value != 0 && Physics.CheckSphere(candidate, clearanceRadius, blockedLayers)) continue;

            point = candidate;
            return true;
        }
        return false;
    }

    private static float RandomIn(Vector2 range) => Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

    private bool TryGetRandomPoint(out Vector3 point)
    {
        point = default;
        Camera cam = Cam;
        if (cam == null)
        {
            Debug.LogWarning($"{nameof(CameraAreaSpawner)}: no camera found (tag one as MainCamera or assign it).", this);
            return false;
        }

        var ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        for (int i = 0; i < Mathf.Max(1, maxPlacementAttempts); i++)
        {
            Vector2 viewport = new Vector2(
                Random.Range(viewportMargin, 1f - viewportMargin),
                Random.Range(viewportMargin, 1f - viewportMargin));
            Ray ray = cam.ViewportPointToRay(viewport);
            if (!ground.Raycast(ray, out float distance)) continue;

            Vector3 groundPoint = ray.GetPoint(distance);
            if (limitToCollider != null && !IsInsideXZ(limitToCollider.bounds, groundPoint)) continue;

            Vector3 candidate = groundPoint + Vector3.up * spawnHeightOffset;
            if (blockedLayers.value != 0 && Physics.CheckSphere(candidate, clearanceRadius, blockedLayers)) continue;

            point = candidate;
            return true;
        }
        return false;
    }

    private static bool IsInsideXZ(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x && point.x <= bounds.max.x
            && point.z >= bounds.min.z && point.z <= bounds.max.z;
    }

    private void OnDrawGizmosSelected()
    {
        Camera cam = Cam;
        if (cam == null) return;

        var ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        Vector2[] corners =
        {
            new Vector2(viewportMargin, viewportMargin),
            new Vector2(1f - viewportMargin, viewportMargin),
            new Vector2(1f - viewportMargin, 1f - viewportMargin),
            new Vector2(viewportMargin, 1f - viewportMargin),
        };

        Gizmos.color = Color.cyan;
        Vector3? previous = null;
        Vector3? first = null;
        foreach (Vector2 corner in corners)
        {
            Ray ray = cam.ViewportPointToRay(corner);
            if (!ground.Raycast(ray, out float distance)) return;
            Vector3 p = ray.GetPoint(distance);
            first ??= p;
            if (previous.HasValue) Gizmos.DrawLine(previous.Value, p);
            previous = p;
        }
        Gizmos.DrawLine(previous.Value, first.Value);
    }
}
