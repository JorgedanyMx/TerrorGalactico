using UnityEngine;
using UnityEngine.InputSystem;

public class p_CursorHandler : MonoBehaviour
{
    public float cursorDistance = 1f;

    [SerializeField] private Transform cursorObject;
    [SerializeField] private p_ShootLogic shootLogic;
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject bulletPrefab;

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

    void OnCursorMove(InputAction.CallbackContext context)
    {
        Vector2 delta = context.ReadValue<Vector2>() * Time.deltaTime;
        cursorObject.localPosition =
            (cursorObject.localPosition + new Vector3(delta.x, 0, delta.y)).normalized * cursorDistance;
    }

    void OnCursorClick(InputAction.CallbackContext context)
    {
        if (stats.CurrentBullets <= 0)
            return;

        Vector3 direction = new Vector3(cursorObject.localPosition.x, 0, cursorObject.localPosition.y).normalized;

        shootLogic.Shoot(player.transform.position, player.transform.position + direction,
                         stats.PlayerDamage, bulletPrefab, player, stats.bulletBounce);
        stats.CurrentBullets--;
    }
}