using UnityEngine;
using TMPro;

public class Notes : MonoBehaviour
{
    [Header("Textos")]
    public string Nname = "";
    public string NCont = "";

    [Header("Referencias")]
    public TMP_Text NameNoteRef;
    public TMP_Text ContentNoteRef;

    private GameObject inspectionPrefab;
    private GameObject inspectionUiPrefab;
    private bool pickupEnabled;
    private bool collected;
    [SerializeField, Min(0.1f)] private float noteInspectionScale = 1.6f;
    public GameEvent NoteCollected;
    private void Awake()
    {
        // Existing note prefabs can leave these references empty.
        foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
        {
            if (NameNoteRef == null && label.name == "Nombre") NameNoteRef = label;
            else if (ContentNoteRef == null && label.name == "Contenido") ContentNoteRef = label;
        }
        RefreshText();
    }

    public void SetNoteText(string sname, string sCont)
    {
        Nname = sname ?? "";
        NCont = sCont ?? "";
        RefreshText();
    }

    private void RefreshText()
    {
        if (NameNoteRef != null) NameNoteRef.text = Nname;
        if (ContentNoteRef != null) ContentNoteRef.text = NCont;
    }

    /// <summary>Turns a world note visual into a collectible that opens the inspection UI.</summary>
    public void ConfigurePickup(string name, string content, GameObject notePrefab, GameObject uiPrefab)
    {
        SetNoteText(name, content);
        inspectionPrefab = notePrefab;
        inspectionUiPrefab = uiPrefab;

        Collider pickupCollider = GetComponentInChildren<Collider>();
        if (pickupCollider == null) pickupCollider = gameObject.AddComponent<BoxCollider>();
        pickupCollider.isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        pickupEnabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!pickupEnabled || collected || !other.CompareTag("Player")) return;
        if (inspectionPrefab == null)
        {
            Debug.LogWarning("Notes: assign the NotePrefab inspection visual on EnemyGraveReturn.", this);
            return;
        }

        ObjectInspector inspector = FindFirstObjectByType<ObjectInspector>();
        if (inspector == null && inspectionUiPrefab != null)
        {
            GameObject ui = Instantiate(inspectionUiPrefab);
            inspector = ui.GetComponentInChildren<ObjectInspector>();
            if (inspector == null) Destroy(ui);
        }
        if (inspector == null)
        {
            Debug.LogWarning("Notes: add InspectItems to the scene or assign its prefab on EnemyGraveReturn.", this);
            return;
        }

        if (!inspector.TryStartNoteInspection(inspectionPrefab, Nname, NCont)) return;
        collected = true;
        NoteCollected.Raise();
        Destroy(gameObject);
    }
}
