using UnityEngine;

/// <summary>Drives visual keyframe clips without moving the player's physics body or aim.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(p_PlayerStats))]
public sealed class p_PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator visualAnimator;
    [SerializeField, Min(0.01f)] private float walkingSpeed = 5f;
    [SerializeField, Min(0f)] private float movementThreshold = 0.1f;

    private static readonly int Moving = Animator.StringToHash("IsMoving");
    private static readonly int WalkSpeed = Animator.StringToHash("WalkSpeed");
    private static readonly int Damage = Animator.StringToHash("Damage");
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private Rigidbody body;
    private p_PlayerStats stats;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        stats = GetComponent<p_PlayerStats>();
    }

    private void OnEnable()
    {
        stats.Damaged += OnDamaged;
        if (visualAnimator == null || visualAnimator.runtimeAnimatorController == null) return;
        visualAnimator.ResetTrigger(Damage);
        visualAnimator.SetBool(Moving, false);
        visualAnimator.SetFloat(WalkSpeed, 1f);
        visualAnimator.Play(Idle, 0, 0f);
    }

    private void OnDisable()
    {
        if (stats != null) stats.Damaged -= OnDamaged;
    }

    private void Update()
    {
        if (visualAnimator == null || visualAnimator.runtimeAnimatorController == null) return;
        Vector3 velocity = body.linearVelocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        // Suppress the ground stride during the ascent/descent of a jump.
        visualAnimator.SetBool(Moving, speed > movementThreshold && Mathf.Abs(velocity.y) < 0.3f);
        visualAnimator.SetFloat(WalkSpeed, Mathf.Clamp(speed / walkingSpeed, 0.25f, 2f));
    }

    private void OnDamaged(float amount)
    {
        if (visualAnimator == null || !visualAnimator.isActiveAndEnabled ||
            visualAnimator.runtimeAnimatorController == null) return;
        visualAnimator.ResetTrigger(Damage);
        visualAnimator.SetTrigger(Damage);
    }
}
