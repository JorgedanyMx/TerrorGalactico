using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Counts collected pieces. When the target is reached it fires onTargetReached
/// (hook the "can throw" logic there) and, if enabled, starts over.
/// </summary>
public class PieceCounter : MonoBehaviour
{
    [SerializeField] private int piecesNeeded = 3;
    [SerializeField] private bool resetAfterReached = true;
    [SerializeField] private UnityEvent<int> onCountChanged;
    [SerializeField] private UnityEvent onTargetReached;

    public int Count { get; private set; }

    private void OnEnable() => PiecePickup.Collected += HandleCollected;
    private void OnDisable() => PiecePickup.Collected -= HandleCollected;

    private void HandleCollected(PiecePickup _)
    {
        Count++;
        onCountChanged?.Invoke(Count);

        if (Count < piecesNeeded) return;

        onTargetReached?.Invoke();
        if (resetAfterReached)
        {
            Count = 0;
            onCountChanged?.Invoke(Count);
        }
    }
}
