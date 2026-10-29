using UnityEngine;
using UnityEngine.InputSystem;

public class p_CursorHandler : MonoBehaviour
{
    public float cursorDistance = 1f;

    [SerializeField] private Transform cursorObject;
    [SerializeField] private p_ShootLogic shootLogic;
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Look At Cursor")]
    [SerializeField] private Transform lookObject;   // the object that turns toward the cursor
    [SerializeField] private float turnSpeed = 0f;   // 0 = snap instantly, higher = smoother

    [Header("Offer")]
    [SerializeField] private GameObject offerPrefab;
    public float offerDamage = 0f;
    public int offerBounce = 0;

    private p_PlayerStats stats;
    private InputAction cursorAction;
    private InputAction cursorClickAction;


    void OnEnable()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        stats = player.GetComponent<p_PlayerStats>();

        cursorAction = InputSystem.actions.FindAction("Cursor");
        cursorAction.Enable();
        cursorAction.performed += OnCursorMove;

        cursorClickAction = InputSystem.actions.FindAction("Shoot");
        cursorClickAction.Enable();
        cursorClickAction.started += OnCursorClick;
    }

    void OnDisable()
    {
        cursorAction.performed -= OnCursorMove;
        cursorClickAction.started -= OnCursorClick;
        cursorAction.Disable();
        cursorClickAction.Disable();
    }

    void LateUpdate()
    {
        if (lookObject == null) return;

        Vector3 dir = GetAimDirection();
        if (dir == Vector3.zero) return;

        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);

        lookObject.rotation = turnSpeed <= 0f
            ? target
            : Quaternion.Slerp(lookObject.rotation, target, turnSpeed * Time.deltaTime);
    }

    void OnCursorMove(InputAction.CallbackContext context)
    {
        Vector2 delta = context.ReadValue<Vector2>() * Time.deltaTime;
        cursorObject.localPosition =
            (cursorObject.localPosition + new Vector3(delta.x, 0, delta.y)).normalized * cursorDistance;
    }

    void OnCursorClick(InputAction.CallbackContext context)
    {
        Fire(bulletPrefab, stats.PlayerDamage, 0,false);
    }

    // Called by p_PlayerController.PlayerOffer
    public void ShootOffer()
    {
        if (offerPrefab == null)
        {
            Debug.LogWarning("p_CursorHandler: offerPrefab is not assigned.");
            return;
        }
        Fire(offerPrefab, offerDamage, offerBounce,true);
    }

    // Single source of truth for aiming, used by both shooting and looking
    private Vector3 GetAimDirection()
    {
        return new Vector3(cursorObject.localPosition.x, 0, cursorObject.localPosition.z).normalized;
    }

    private void Fire(GameObject prefab, float damage, int bounce, bool isOffer)
    {
        Vector3 direction = GetAimDirection();
        shootLogic.Shoot(player.transform.position, player.transform.position + direction,
                         damage, prefab, player, bounce, isOffer);
    }
}