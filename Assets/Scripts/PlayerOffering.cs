using UnityEngine;
using UnityEngine.Events;

/// <summary>Turns collected sets of pieces into offerings aimed at the player's cursor.</summary>
[DisallowMultipleComponent]
public sealed class PlayerOffering : MonoBehaviour
{
    [Tooltip("Throwable prefab with a Rigidbody, collider and GhostHitTrigger. Do not use a damage bullet.")]
    [SerializeField] private GameObject offeringPrefab;
    [SerializeField] private Transform aimTarget;
    [Tooltip("Optional spawn point. The player position is used when this is empty.")]
    [SerializeField] private Transform throwOrigin;
    [SerializeField, Min(0.01f)] private float throwSpeed = 12f;
    [SerializeField, Min(0.01f)] private float lifetime = 4f;
    [SerializeField] private UnityEvent<int> onOfferingsChanged = new UnityEvent<int>();

    public int AvailableOfferings { get; private set; }

    /// <summary>Connect PieceCounter.onTargetReached to this method.</summary>
    public void AddOffering()
    {
        AvailableOfferings++;
        onOfferingsChanged?.Invoke(AvailableOfferings);
    }

    /// <summary>Consumes an offering only after a valid projectile has been launched.</summary>
    public bool TryThrowOffering()
    {
        if (!isActiveAndEnabled || AvailableOfferings <= 0 || offeringPrefab == null || aimTarget == null
            || throwSpeed <= 0f || lifetime <= 0f
            || offeringPrefab.GetComponent<Rigidbody>() == null
            || offeringPrefab.GetComponent<GhostHitTrigger>() == null
            || offeringPrefab.GetComponentInChildren<Collider>() == null)
            return false;

        Vector3 direction = aimTarget.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;
        direction.Normalize();

        Vector3 origin = throwOrigin != null ? throwOrigin.position : transform.position;
        GameObject offering = Instantiate(offeringPrefab, origin, Quaternion.LookRotation(direction));
        Collider[] playerColliders = GetComponentsInChildren<Collider>(true);
        Collider[] offeringColliders = offering.GetComponentsInChildren<Collider>(true);
        foreach (Collider offeringCollider in offeringColliders)
            foreach (Collider playerCollider in playerColliders)
                Physics.IgnoreCollision(offeringCollider, playerCollider);

        Rigidbody body = offering.GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = direction * throwSpeed;

        OfferingProjectile projectile = offering.GetComponent<OfferingProjectile>();
        if (projectile == null) projectile = offering.AddComponent<OfferingProjectile>();
        projectile.Initialize(transform, lifetime);

        AvailableOfferings--;
        onOfferingsChanged?.Invoke(AvailableOfferings);
        return true;
    }
}
