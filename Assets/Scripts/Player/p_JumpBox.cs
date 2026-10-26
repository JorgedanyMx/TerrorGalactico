using UnityEngine;

public class p_JumpBox : MonoBehaviour
{
    private p_PlayerController pc;
    void Start()
    {
        pc = GetComponentInParent<p_PlayerController>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnTriggerEnter(Collider other)
    {
        if (other.tag != "Player")
            pc.Landed();
    }
}
