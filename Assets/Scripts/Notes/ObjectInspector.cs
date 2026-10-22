using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
// IMPORTANTE: Asegúrate de tener instalado el paquete de Post Processing o estar en URP/HDRP.
// Descomenta la línea de abajo según el sistema que uses.
using UnityEngine.Rendering; // Necesario para Volume (URP/HDRP)
using UnityEngine.Rendering.Universal; // Descomenta si usas URP (Universal Render Pipeline)
// using UnityEngine.Rendering.PostProcessing; // Descomenta si usas el Post-Processing tradicional de la Built-in pipeline

public class ObjectInspector : MonoBehaviour
{
    [Header("Configuración de Inspección")]
    [Tooltip("Distancia desde la cámara a la que aparecerá el objeto.")]
    public float spawnDistance = 2.0f;
    [Tooltip("Padre de objetos que se activan/desactivan al usar el inspector")]
    public GameObject ActivableElements;
    [Tooltip("Velocidad de rotación del objeto al inspeccionarlo.")]
    public float rotationSpeed = 0.5f;

    [Header("Referencias de Input")]
    [Tooltip("Asigna aquí la acción Inspection/Inspect.")]
    public InputActionReference inspectAction;
    [Tooltip("Asigna aquí la acción Inspection/Exit.")]
    public InputActionReference exitAction;
    

    [Header("Efecto de Desenfoque (Post-Processing URP)")]
    [Tooltip("Arrastra aquí el Volume global de tu escena que contiene el Depth of Field.")]
    public Volume postProcessVolume;
    [Tooltip("Tiempo en segundos que tardará el desenfoque en activarse/desactivarse.")]
    public float blurTransitionTime = 0.5f;
    public TMP_Text textName;
    public TMP_Text textCont;

    private GameObject currentInspectedObject;
    private Transform cameraTransform;
    private DepthOfField depthOfField;
    private void Awake()
    {
        cameraTransform = Camera.main.transform;
        // Intentamos obtener el efecto de Depth Of Field del Volumen asignado
        if (postProcessVolume != null && postProcessVolume.profile.TryGet<DepthOfField>(out var dof))
        {
            depthOfField = dof;
        }
        else
        {
            Debug.LogWarning("No se encontró el efecto Depth of Field en el Volume. Asegúrate de añadirlo en el inspector del Volume.");
        }
        ActivableElements.SetActive(false);                 //Desactiva los objectos de la inspeccion
    }

    private void OnEnable()
    {
        if (exitAction != null)
        {
            exitAction.action.performed += OnExitPerformed;
        }
    }

    private void OnDisable()
    {
        if (exitAction != null)
        {
            exitAction.action.performed -= OnExitPerformed;
        }
        DisableInspectionInput();
    }

    /// <summary>
    /// Llama a esta función pasándole el GameObject (Prefab) que deseas inspeccionar.
    /// </summary>
    public void StartGeneralInspection(GameObject objectToInspect)
    {
        ActivableElements.SetActive(true);
        if (currentInspectedObject != null) return;

        // Calcular la posición centrada frente a la cámara
        Vector3 spawnPosition = cameraTransform.position + (cameraTransform.forward * spawnDistance);

        // Spawnear el objeto
        currentInspectedObject = Instantiate(objectToInspect, spawnPosition, Quaternion.identity);

        // Activar el Action Map
        EnableInspectionInput();

        // 🌟 ACTIVAR DESENFOQUE (Transición suave)
        TransitionBlur();
        IsNote(objectToInspect);
    }
    public void IsNote(GameObject gameObjectRef)
    {
        Notes tmpNote = gameObjectRef?.GetComponent<Notes>();
        if (tmpNote == null)
        {
            textName.text = string.Empty;
            textCont.text = string.Empty;
            return;
        }
        else
        {
            textName.text = tmpNote.Nname;
            textCont.text = tmpNote.NCont;
        }
    }

    private void Update()
    {
        if (inspectAction.action.ReadValue<Vector2>() == Vector2.zero)
            return;
        if (currentInspectedObject != null && inspectAction.action.IsPressed())
        {
            Vector2 inputDelta = inspectAction.action.ReadValue<Vector2>();

            // Rotar el objeto
            currentInspectedObject.transform.Rotate(cameraTransform.up, -inputDelta.x * rotationSpeed, Space.World);
            currentInspectedObject.transform.Rotate(cameraTransform.right, inputDelta.y * rotationSpeed, Space.World);
        }
    }

    private void OnExitPerformed(InputAction.CallbackContext context)
    {
        if (currentInspectedObject != null)
        {
            Destroy(currentInspectedObject);
            currentInspectedObject = null;

            DisableInspectionInput();
        }
        ActivableElements.SetActive(false);
    }

    private void TransitionBlur()
    {
        if (depthOfField == null) return;
        depthOfField.focusDistance.Override(depthOfField.focusDistance.value);

        depthOfField.focusDistance.value = spawnDistance;
    }

    private void EnableInspectionInput()
    {
        if (inspectAction != null) inspectAction.action.Enable();
        if (exitAction != null) exitAction.action.Enable();
    }

    private void DisableInspectionInput()
    {
        if (inspectAction != null) inspectAction.action.Disable();
        if (exitAction != null) exitAction.action.Disable();
    }
}