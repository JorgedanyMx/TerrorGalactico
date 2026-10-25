using UnityEngine;

[SelectionBase] // <-- Hace el prefab seleccionable
public class RotateItem : MonoBehaviour
{
    [Header("Variables para rotacion de objectos")]
    public float velocidad = 10;
    public bool EnableRotation = true;
    void Update()
    {
        if (!EnableRotation) return;
        // Ejemplo en Unity: rotación simple y económica
        transform.Rotate(Vector3.up * velocidad * Time.deltaTime);
    }
}
