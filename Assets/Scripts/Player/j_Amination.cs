using UnityEngine;

public class CambioAnimacionesRigidbody : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El Rigidbody del personaje. Si se deja vacío, se buscará automáticamente en este GameObject.")]
    [SerializeField] private Rigidbody rb;
    [Tooltip("El Animator que controla las animaciones.")]
    [SerializeField] private Animator animator;
    [Tooltip("El AudioSource encargado de reproducir el sonido de los pasos.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Configuración")]
    [Tooltip("Velocidad mínima a partir de la cual se considera que el personaje está caminando.")]
    [SerializeField] private float umbralMovimiento = 0.1f;

    private void Awake()
    {
        // Buscar automáticamente los componentes si no se asignaron en el inspector
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (rb == null || animator == null) return;

        // Obtenemos la velocidad horizontal (ignorando el eje Y)
        Vector3 velocidadHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float velocidadActual = velocidadHorizontal.magnitude;

        // Comprobamos si la velocidad supera el umbral
        if (velocidadActual > umbralMovimiento)
        {
            // Activa el trigger de caminar
            animator.SetTrigger("SetWalking");

            // Reproduce el audio de los pasos si no se está reproduciendo ya
            if (audioSource != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            // Activa el trigger de estar quieto
            animator.SetTrigger("Idle");

            // Detiene el audio de los pasos si se está reproduciendo
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }
    public void IsDead()
    {
        animator.SetTrigger("IsDead");
    }
    public void ReceivingDamage()
    {
        animator.SetTrigger("ReceivingDamage");

    }
}