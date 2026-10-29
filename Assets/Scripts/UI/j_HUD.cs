using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class j_HUD : MonoBehaviour
{
    [Header("Offcer")]
    [Tooltip("El GameObject de la oferta")]
    [SerializeField] private GameObject offer;

    [Header("Referencias")]
    [Tooltip("La imagen UI que se desea mostrar/ocultar.")]
    [SerializeField] private Image targetImage;

    [Header("Configuración")]
    [Tooltip("Duración de la animación completa (subir y bajar el alpha) en segundos.")]
    [SerializeField] private float duracionAnimacion = 0.5f;

    private Coroutine rutinaActual;

    public void EnableOffer(bool setEnable)
    {
        if (offer != null)
        {
            offer.SetActive(setEnable);
        }
    }

    private void Awake()
    {
        if (offer != null)
        {
            offer.SetActive(false);
        }

        if (targetImage != null)
        {
            Color colorInicial = targetImage.color;
            colorInicial.a = 0f;
            targetImage.color = colorInicial;
            targetImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Activa el parpadeo completo (0 -> 1 -> 0). Si ya se está ejecutando, interrumpe la animación actual y la reinicia desde donde esté hacia 1.
    /// </summary>
    public void AlternarPanel()
    {
        if (targetImage == null) return;

        if (rutinaActual != null)
        {
            StopCoroutine(rutinaActual);
        }

        rutinaActual = StartCoroutine(AnimarEfectoCompleto());
    }

    private IEnumerator AnimarEfectoCompleto()
    {
        targetImage.gameObject.SetActive(true);
        Color tempColor = targetImage.color;

        // Tiempos divididos para la subida (20%) y la bajada (80%)
        float duracionSubida = duracionAnimacion * 0.2f;
        float duracionBajada = duracionAnimacion * 0.8f;

        // 1. INICIO (Subir alpha a 1 desde el valor actual para evitar saltos si se reinicia)
        float alfaInicio = targetImage.color.a;
        float tiempoTranscurrido = 0f;

        while (tiempoTranscurrido < duracionSubida)
        {
            tiempoTranscurrido += Time.deltaTime;
            tempColor.a = Mathf.Lerp(alfaInicio, 1f, tiempoTranscurrido / duracionSubida);
            targetImage.color = tempColor;
            yield return null;
        }

        tempColor.a = 1f;
        targetImage.color = tempColor;

        // 2. FIN (Bajar alpha de 1 a 0)
        tiempoTranscurrido = 0f;

        while (tiempoTranscurrido < duracionBajada)
        {
            tiempoTranscurrido += Time.deltaTime;
            tempColor.a = Mathf.Lerp(1f, 0f, tiempoTranscurrido / duracionBajada);
            targetImage.color = tempColor;
            yield return null;
        }

        tempColor.a = 0f;
        targetImage.color = tempColor;
        targetImage.gameObject.SetActive(false);

        rutinaActual = null;
    }
}