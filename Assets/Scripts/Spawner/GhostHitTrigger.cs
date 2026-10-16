using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put it on the throwable object. When it hits something tagged "Ghost" it calls
/// whatever is hooked to onGhostHit (receives the ghost GameObject) or overrides OnGhostHit.
/// Works with solid colliders and triggers.
/// </summary>
public class GhostHitTrigger : MonoBehaviour
{
    [SerializeField] private string ghostTag = "Ghost";
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private UnityEvent<GameObject> onGhostHit;

    private void OnCollisionEnter(Collision collision) => TryHit(collision.gameObject);
    private void OnTriggerEnter(Collider other) => TryHit(other.gameObject);

    private void TryHit(GameObject other)
    {
        if (other.tag != ghostTag) return;

        OnGhostHit(other);
        onGhostHit?.Invoke(other);
        if (destroyOnHit) Destroy(gameObject);
    }

    /// <summary>The "function" that runs when a ghost is hit. Fill it in (or use the event).</summary>
    protected virtual void OnGhostHit(GameObject ghost)
    {
        Debug.Log($"Ghost hit: {ghost.name}", ghost);
    }
}
