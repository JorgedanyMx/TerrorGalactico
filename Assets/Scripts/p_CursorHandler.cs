using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class cursorHandler : MonoBehaviour
{
    public static event System.Action<int> OnPlayerShoot;
    public float cursorDistance = 1f;
    [SerializeField] private Transform cursorObject;
    // [SerializeField] private ShootLogic shootLogic;
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject bulletPrefab;
    public InputAction cursorAction;
    public InputAction cursorClickAction;

    // Player stats that will be carried from the Stats script
    private int bulletBounce;
    private float damage;


    void OnEnable()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        cursorAction = InputSystem.actions.FindAction("CursorMove");
        cursorAction.Enable();
        cursorAction.performed += OnCursorMove;
        cursorClickAction = InputSystem.actions.FindAction("CursorClick");
        cursorClickAction.Enable();
        cursorClickAction.started += OnCursorClick;
        CheckStats();   
    }

    private void OnDisable()
    {
        cursorAction.Disable();
        cursorClickAction.Disable();
    }

    private void OnDestroy()
    {
        cursorAction.performed -= OnCursorMove;
        cursorClickAction.started -= OnCursorClick;
    }

    private void CheckStats()
    {
        // bulletBounce = player.GetComponent<p_PlayerStats>().bulletBounce;
        // damage = player.GetComponent<p_PlayerStats>().PlayerDamage;
    }

    private void OnCursorMove(InputAction.CallbackContext context)
    {
        // Vector2 cursorInput = context.ReadValue<Vector2>();
        // cursorInput = cursorInput.normalized * cursorDistance;
        // cursorObject.localPosition = Vector3.Lerp(cursorObject.localPosition, new Vector3(cursorInput.x, cursorInput.y, 0), smoothSpeed);
        Vector2 cursorInput = context.ReadValue<Vector2>();
        Vector2 adjustedInput = cursorInput * Time.deltaTime;
        cursorObject.localPosition = (cursorObject.localPosition + new Vector3(adjustedInput.x, adjustedInput.y, 0)).normalized * cursorDistance;
    }

    private void OnCursorClick(InputAction.CallbackContext context)
    {
        Vector3 targetPosition = new Vector3(cursorObject.transform.localPosition.x, 0, cursorObject.transform.localPosition.y).normalized;
        // if (player.GetComponent<p_PlayerStats>().CurrentBullets > 0)
        // {
            // OnPlayerShoot?.Invoke(player.GetComponent<p_PlayerStats>().CurrentBullets);
            // player.GetComponent<p_PlayerStats>().CurrentBullets--;
            // shootLogic.Shoot(player.transform.position, player.transform.position + targetPosition, damage, bulletPrefab, player, bulletBounce);
        // }
        // else
        // {
        //     Debug.Log("Out of bullets!");
        // }
    }


}
