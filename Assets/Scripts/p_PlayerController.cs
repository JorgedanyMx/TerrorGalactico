using UnityEngine.InputSystem;
using UnityEngine;
using Unity.VisualScripting;
using System.Runtime.InteropServices;

public class p_PlayerController : MonoBehaviour
{
    private Camera cam;
    private Rigidbody rb;
    private bool jumping;
    private float JumpHeight = 5f;

    [SerializeField]
    Collider cd;
    
    Vector3 dir;
    InputActionMap Map;
    InputAction Move;
    InputAction Jump;
    InputAction Shoot;
    InputAction Offer;

    private float Speed = 5f;

    void OnEnable()
    {

        Map = InputSystem.actions.FindActionMap("p_Player");
        Move = Map.FindAction("Move");
        Jump = Map.FindAction("Jump");
        Shoot = Map.FindAction("Shoot");
        Offer = Map.FindAction("Offer");

        Move.performed += PlayerMove;
        Move.canceled += PlayerStoppedMoving;
        Jump.started += PlayerJump;
        Shoot.started += PlayerShoot;
        Offer.started += PlayerOffer;

        rb = gameObject.GetComponent<Rigidbody>() == null ? gameObject.AddComponent<Rigidbody>() : gameObject.GetComponent<Rigidbody>();  
        // cd = gameObject.GetComponent<Collider>() == null ? gameObject.AddComponent<Collider>() : gameObject.GetComponent<Collider>();
        cam = Camera.main;
    }

    void Start()
    {

    }

    void PlayerMove(InputAction.CallbackContext context)
    {
        if (!jumping)
            dir = new Vector3(context.ReadValue<Vector2>().x , rb.linearVelocity.y, context.ReadValue<Vector2>().y).normalized * Speed;
    }

    void PlayerStoppedMoving(InputAction.CallbackContext context)
    {
        dir = Vector3.zero;
    }
    void PlayerJump(InputAction.CallbackContext context)
    {
        if(jumping)
            return;
        jumping = true;
        rb.AddForce(Vector3.up * 150, ForceMode.Impulse);
    }
    void PlayerShoot(InputAction.CallbackContext context)
    {
        
    }
    void PlayerOffer(InputAction.CallbackContext context)
    {
        
    }
    
    void FixedUpdate()
    {
        rb.linearVelocity = dir;
        if (rb.linearVelocity.y == 0 && jumping)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y;
        }
        // if (rb.linearVelocity.y != 0)
        //     Debug.Log("Velocity in Y: " + rb.linearVelocity.y);
    }

    void OnTriggerEnter(Collider other)
    {
        if (jumping)
            jumping = false;
    }
}
