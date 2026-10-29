using UnityEngine;
using System.Collections;
public class j_HUD : MonoBehaviour
{

    [Header("Offcer")]
    [Tooltip("El GameObject de la oferta")]
    [SerializeField] private GameObject offer;

    [Header("Referencias")]
    [Tooltip("El GameObject del Panel que se desea mostrar/ocultar.")]
    [SerializeField] private GameObject panelCanvas;

    [Header("Configuración")]
    [Tooltip("Duración de la animación de aparición y desaparición en segundos.")]
    [SerializeField] private float duracionAnimacion = 0.5f;

    private CanvasGroup canvasGroup;
    private Coroutine rutinaActual;

    public void EnableOffer(bool setEnable)
    {
        offer.SetActive(setEnable);
    }

    private void Awake()
    {
        if (panelCanvas != null)
        {
            // Intentamos obtener el CanvasGroup del panel, si no lo tiene, lo agregamos automáticamente
            canvasGroup = panelCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panelCanvas.AddComponent<CanvasGroup>();
            }
        }
        offer.SetActive(false);
        panelCanvas.SetActive(false);

    }

    /// <summary>
    /// Método público para alternar entre mostrar y ocultar el panel.
    /// </summary>
    public void AlternarPanel()
    {
        if (panelCanvas == null) return;

        // Si el panel está activo y visible, lo ocultamos; de lo contrario, lo mostramos
        if (panelCanvas.activeSelf && canvasGroup.alpha > 0f)
        {
            OcultarPanel();
        }
        else
        {
            MostrarPanel();
        }
    }

    /// <summary>
    /// Hace aparecer el panel haciendo un Fade In (Alpha de 0 a 1).
    /// </summary>
    public void MostrarPanel()
    {
        if (panelCanvas == null) return;

        panelCanvas.SetActive(true);
        EjecutarFade(0f, 1f, null);
    }

    /// <summary>
    /// Hace desaparecer el panel haciendo un Fade Out (Alpha de 1 a 0) y lo desactiva al terminar.
    /// </summary>
    public void OcultarPanel()
    {
        if (panelCanvas == null) return;

        EjecutarFade(1f, 0f, () => {
            panelCanvas.SetActive(false);
        });
    }

    private void EjecutarFade(float alfaInicio, float alfaFin, System.Action alTerminar)
    {
        if (rutinaActual != null)
        {
            StopCoroutine(rutinaActual);
        }
        rutinaActual = StartCoroutine(AnimarFade(alfaInicio, alfaFin, alTerminar));
    }

    private IEnumerator AnimarFade(float alfaInicio, float alfaFin, System.Action alTerminar)
    {
        float tiempoTranscurrido = 0f;
        canvasGroup.alpha = alfaInicio;

        while (tiempoTranscurrido < duracionAnimacion)
        {
            tiempoTranscurrido += Time.deltaTime;
            // Interpola suavemente el alpha basándose en el tiempo transcurrido y la duración configurada (0.5s por defecto)
            canvasGroup.alpha = Mathf.Lerp(alfaInicio, alfaFin, tiempoTranscurrido / duracionAnimacion);
            yield return null;
        }

        canvasGroup.alpha = alfaFin;
        alTerminar?.Invoke();
    }
}
