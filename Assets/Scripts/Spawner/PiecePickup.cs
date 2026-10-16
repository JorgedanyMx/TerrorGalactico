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

    private bool taken;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (taken || other.tag != collectorTag) return;

        taken = true;
        onCollected?.Invoke();
        Collected?.Invoke(this);

        if (TryGetComponent(out PieceLifecycle lifecycle)) lifecycle.Dismiss();
        else Destroy(gameObject);
    }
}
