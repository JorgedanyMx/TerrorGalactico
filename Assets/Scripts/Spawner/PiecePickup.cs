using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Collectible piece. Needs a trigger collider; whoever has the "Player" tag (and a Rigidbody
/// or CharacterController) collects it by touching it.
/// If the piece has a PieceLifecycle (added by the spawner) it shrinks away like when it expires,
/// otherwise it is destroyed right away.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PiecePickup : MonoBehaviour
{
    /// <summary>Raised for every collected piece, so other systems can listen without references.</summary>
    public static event Action<PiecePickup> Collected;

    [SerializeField] private string collectorTag = "Player";
    [SerializeField] private UnityEvent onCollected;
    [Tooltip("Same event, but it also gives the piece id (0, 1, 2...), set by the spawner. -1 if the piece was placed by hand.")]
    [SerializeField] private UnityEvent<int> onCollectedWithId;

    private bool taken;

    /// <summary>Piece type, assigned by the spawner when it drops the piece (-1 if placed by hand).</summary>
    public int Id { get; private set; } = -1;

    public void SetId(int id) => Id = id;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (taken || other.tag != collectorTag) return;

        taken = true;
        onCollected?.Invoke();
        onCollectedWithId?.Invoke(Id);
        Collected?.Invoke(this);

        if (TryGetComponent(out PieceLifecycle lifecycle)) lifecycle.Dismiss();
        else Destroy(gameObject);
    }
}
