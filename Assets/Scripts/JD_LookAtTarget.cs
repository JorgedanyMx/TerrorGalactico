using UnityEngine;

public class JD_LookAtTarget : MonoBehaviour
{
    public GameObject target;

    void Update()
    {
        if (target != null)
        {
            // Calculamos la dirección hacia el objetivo
            Vector3 direction = target.transform.position - transform.position;

            // Forzamos la altura (Y) a cero para que no se incline hacia arriba o abajo
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                // Creamos la rotación únicamente orientada en el plano horizontal
                Quaternion rotacionBuscada = Quaternion.LookRotation(direction);
                transform.rotation = rotacionBuscada;
            }
        }
    }
}
