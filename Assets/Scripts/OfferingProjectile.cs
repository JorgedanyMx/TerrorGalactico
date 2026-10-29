using UnityEngine;

/// <summary>Limits an offering's lifetime and stops it at solid scenery.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class OfferingProjectile : MonoBehaviour
{
    private Transform owner;

    public void Initialize(Transform projectileOwner, float lifetime)
    {
        owner = projectileOwner;
        Destroy(gameObject, Mathf.Max(0.01f, lifetime));
    }

    private void OnTriggerEnter(Collider other)
    {
        // Pickups and detection volumes must not block an offering.
        if (other.isTrigger || IsOwner(other.transform)) return;
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsOwner(collision.transform)) return;
        Destroy(gameObject);
    }

    private bool IsOwner(Transform target)
    {
        return owner != null && target.IsChildOf(owner);
    }
}
