using Unity.VisualScripting;
using UnityEngine;

public class p_Collectable : MonoBehaviour
{
    bool collecting = false;
    p_ICanCollect collector;
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Triggering");
        if (collecting)
            return;
        collector = other.GetComponent<p_ICanCollect>();
        if (collector != null)
        {
            collecting = true;
            collector.Collect();
            Destroy(gameObject);
        }

    }
}
