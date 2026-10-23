using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>Opens a gate leaf around its hinge when a Player enters the stationary trigger.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class GateLeaf : MonoBehaviour
{
    [SerializeField] private Transform hinge;
    [Tooltip("Signed rotation around local Y. Reverse the sign to change the opening direction.")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField, Min(0.01f)] private float openingDuration = 1.5f;
    [SerializeField] private string playerTag = "Player";

    private Quaternion closedRotation;
    private float progress;
    private bool opening;

    public bool IsOpen => progress >= 1f;

    private void Awake()
    {
        if (hinge == null)
        {
            Debug.LogError("GateLeaf needs a hinge transform.", this);
            enabled = false;
            return;
        }
        closedRotation = hinge.localRotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        // Support both a tagged collider and colliders nested under the player root.
        for (Transform actor = other.transform; actor != null; actor = actor.parent)
        {
            if (!actor.CompareTag(playerTag)) continue;
            Open();
            return;
        }
    }

    public void Open() => opening = true;

#if UNITY_EDITOR
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            Open();
    }
#endif

    private void FixedUpdate()
    {
        if (!opening || IsOpen) return;
        progress = Mathf.MoveTowards(progress, 1f, Time.fixedDeltaTime / Mathf.Max(0.01f, openingDuration));
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        hinge.localRotation = closedRotation * Quaternion.Euler(0f, openAngle * eased, 0f);
    }
}
