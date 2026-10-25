using UnityEngine;

/// <summary>
/// Follows a target keeping the offset the camera has in the scene (position and rotation are not touched).
/// Put it on the camera. If no target is assigned it looks for the object tagged "Player".
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("Approximate seconds to catch up. 0 = rigid follow.")]
    [SerializeField] private float smoothTime = 0.15f;

    private Vector3 offset;
    private Vector3 velocity;

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target != null) offset = transform.position - target.position;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime)
            : desired;
    }
}
