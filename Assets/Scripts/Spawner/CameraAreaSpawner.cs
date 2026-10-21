using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Cycle: wait a random time -> the spawner appears at a random point of the spawn area,
/// drops the pieces in a ring around itself -> stays a few seconds -> disappears -> repeat.
/// The spawn area is the floor collider's footprint (XZ), optionally shrunk by an edge margin
/// and/or limited to a custom rectangle. It does not depend on the camera.
/// (The class keeps its old name so existing scenes and prefabs do not lose the reference.)
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

    [Header("Spawn area (floor)")]
    [Tooltip("The floor. Its top surface sets the height and its footprint (bounds) sets the area.")]
    [FormerlySerializedAs("limitToCollider")]
    [SerializeField] private Collider groundCollider;
    [Tooltip("Keeps objects this far from the edges of the area (nothing spawns right at the border).")]
    [SerializeField, Min(0f)] private float edgeMargin = 2f;
    [Tooltip("Limit the area to a custom rectangle instead of the whole floor. Always clipped to the floor.")]
    [SerializeField] private bool useCustomArea = false;
    [Tooltip("Custom rectangle center, as an offset (X, Z) from the floor's center.")]
    [SerializeField] private Vector2 customCenterOffset = Vector2.zero;
    [Tooltip("Custom rectangle size (X, Z).")]
    [SerializeField] private Vector2 customSize = new Vector2(20f, 20f);

    [Header("Placement")]
    [Tooltip("Spawn points overlapping these layers are rejected (walls, props...).")]
    [SerializeField] private LayerMask blockedLayers;
    [SerializeField] private float clearanceRadius = 0.5f;
    [SerializeField] private int maxPlacementAttempts = 10;

    private Coroutine cycle;
    private GameObject currentVisual;

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
        if (!TryGetArea(out Rect area, out float floorY)) yield break;
        if (!TryGetRandomPoint(area, floorY, out Vector3 standPoint)) yield break;

        Vector3 hiddenPoint = standPoint - Vector3.up * riseDepth;

        if (spawnerVisual != null)
        {
            currentVisual = Instantiate(spawnerVisual, hiddenPoint, Quaternion.identity);
            yield return MoveTo(currentVisual.transform, hiddenPoint, standPoint, riseTime);
        }

        DropPieces(area, standPoint);

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

    private void DropPieces(Rect area, Vector3 center)
    {
        if (prefabs == null || prefabs.Length == 0) return;

        for (int i = 0; i < piecesPerAppearance; i++)
        {
            if (!TryGetPointAround(area, center, out Vector3 point)) continue;

            GameObject piece = Instantiate(prefabs[Random.Range(0, prefabs.Length)], point, Quaternion.identity);
            piece.AddComponent<PieceLifecycle>().Begin(pieceLifetime, pieceScaleTime, pieceScaleTime);
        }
    }

    private bool TryGetPointAround(Rect area, Vector3 center, out Vector3 point)
    {
        point = default;
        for (int i = 0; i < Mathf.Max(1, maxPlacementAttempts); i++)
        {
            // Uniform point in the ring between minDropDistance and dropRadius.
            float inner = Mathf.Clamp(minDropDistance, 0f, dropRadius);
            float radius = Mathf.Sqrt(Random.Range(inner * inner, dropRadius * dropRadius));
            Vector2 offset = Random.insideUnitCircle.normalized * radius;
            Vector3 candidate = new Vector3(center.x + offset.x, center.y + pieceHeightOffset, center.z + offset.y);

            if (!area.Contains(new Vector2(candidate.x, candidate.z))) continue;
            if (IsBlocked(candidate)) continue;

            point = candidate;
            return true;
        }
        return false;
    }

    private bool TryGetRandomPoint(Rect area, float floorY, out Vector3 point)
    {
        point = default;
        for (int i = 0; i < Mathf.Max(1, maxPlacementAttempts); i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(area.xMin, area.xMax),
                floorY,
                Random.Range(area.yMin, area.yMax));

            if (IsBlocked(candidate + Vector3.up * clearanceRadius)) continue;

            point = candidate;
            return true;
        }
        return false;
    }

    private bool IsBlocked(Vector3 position)
    {
        return blockedLayers.value != 0 && Physics.CheckSphere(position, clearanceRadius, blockedLayers);
    }

    /// <summary>
    /// The spawn rectangle in world XZ (Rect.x = world X, Rect.y = world Z) and the floor's top height.
    /// </summary>
    private bool TryGetArea(out Rect area, out float floorY, bool logWarnings = true)
    {
        area = default;
        floorY = 0f;
        if (groundCollider == null)
        {
            if (logWarnings) Debug.LogWarning($"{nameof(CameraAreaSpawner)}: assign the floor collider in 'Ground Collider'.", this);
            return false;
        }

        Bounds bounds = groundCollider.bounds;
        floorY = bounds.max.y;

        float xMin = bounds.min.x, xMax = bounds.max.x;
        float zMin = bounds.min.z, zMax = bounds.max.z;

        if (useCustomArea)
        {
            Vector2 center = new Vector2(bounds.center.x, bounds.center.z) + customCenterOffset;
            Vector2 half = new Vector2(Mathf.Abs(customSize.x), Mathf.Abs(customSize.y)) * 0.5f;
            xMin = Mathf.Max(xMin, center.x - half.x);
            xMax = Mathf.Min(xMax, center.x + half.x);
            zMin = Mathf.Max(zMin, center.y - half.y);
            zMax = Mathf.Min(zMax, center.y + half.y);
        }

        xMin += edgeMargin;
        xMax -= edgeMargin;
        zMin += edgeMargin;
        zMax -= edgeMargin;

        if (xMin >= xMax || zMin >= zMax)
        {
            if (logWarnings) Debug.LogWarning($"{nameof(CameraAreaSpawner)}: the spawn area is empty (edge margin or custom area too big for the floor).", this);
            return false;
        }

        area = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCollider == null) return;

        // Whole floor, in grey, so you can see how the area sits inside it.
        Bounds bounds = groundCollider.bounds;
        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        Gizmos.DrawWireCube(
            new Vector3(bounds.center.x, bounds.max.y, bounds.center.z),
            new Vector3(bounds.size.x, 0f, bounds.size.z));

        if (!TryGetArea(out Rect area, out float floorY, false)) return;

        Vector3 center = new Vector3(area.center.x, floorY, area.center.y);
        Vector3 size = new Vector3(area.width, 0f, area.height);

        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawCube(center, new Vector3(size.x, 0.02f, size.z));
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);
    }

    private static float RandomIn(Vector2 range) => Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
}
