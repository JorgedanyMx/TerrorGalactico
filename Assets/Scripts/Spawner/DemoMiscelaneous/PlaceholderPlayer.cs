using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Placeholder pill that moves with WASD relative to the camera, just to test pickups.
/// Replace it with the real player later: the pickups only need the "Player" tag and a Rigidbody.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlaceholderPlayer : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    private Rigidbody body;
    private Vector2 input;

    private void Reset()
    {
        tag = "Player";
        var rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Awake() => body = GetComponent<Rigidbody>();

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        input = new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
        input = Vector2.ClampMagnitude(input, 1f);
    }

    private void FixedUpdate()
    {
        // Camera-relative on the floor, so it also works with an isometric camera yaw.
        Camera cam = Camera.main;
        Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;

        Vector3 move = (right * input.x + forward * input.y) * speed;
        body.linearVelocity = new Vector3(move.x, body.linearVelocity.y, move.z);
    }
}
