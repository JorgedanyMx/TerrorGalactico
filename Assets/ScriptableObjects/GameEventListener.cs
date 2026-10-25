using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameEventListener : MonoBehaviour
{
    public GameEvent Event;
    public UnityEvent Response;
    public UnityEvent<GameObject> ResponseWithTarget = new UnityEvent<GameObject>();
    public void OnEventRise()
    {
            Response.Invoke();
    }

    public void OnEventRise(GameObject target)
    {
        ResponseWithTarget?.Invoke(target);
    }
    private void OnEnable(){
        Event.RegisterListener(this);
    } 
    private void OnDisable(){
        Event.UnregisterListener(this);
    }
}

