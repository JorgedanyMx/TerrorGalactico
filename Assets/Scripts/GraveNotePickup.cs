using TMPro;
using UnityEngine;

/// <summary>Adapts grave drops to the existing Notes and ObjectInspector without modifying them.</summary>
[DisallowMultipleComponent]
public sealed class GraveNotePickup : MonoBehaviour
{
    private string noteName;
    private string noteContent;
    private GameObject inspectionPrefab;
    private GameObject uiPrefab;
    private bool collected;

    public void Configure(string title, string content, GameObject visual, GameObject ui)
    {
        noteName = title;
        noteContent = content;
        inspectionPrefab = visual;
        uiPrefab = ui;
        Collider trigger = GetComponent<Collider>();
        if (trigger == null) trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other) => TryCollect(other);
    private void OnTriggerStay(Collider other) => TryCollect(other);

    private void TryCollect(Collider other)
    {
        if (collected || inspectionPrefab == null || Camera.main == null) return;
        bool isPlayer = false;
        for (Transform current = other.transform; current != null; current = current.parent)
            if (current.CompareTag("Player")) { isPlayer = true; break; }
        if (!isPlayer) return;

        ObjectInspector inspector = FindFirstObjectByType<ObjectInspector>();
        if (inspector == null && uiPrefab != null)
        {
            GameObject ui = Instantiate(uiPrefab);
            inspector = ui.GetComponentInChildren<ObjectInspector>();
            if (inspector == null) Destroy(ui);
        }
        if (inspector == null || !inspector.isActiveAndEnabled || inspector.ActivableElements == null ||
            inspector.textName == null || inspector.textCont == null ||
            inspector.inspectAction == null || inspector.exitAction == null ||
            inspector.ActivableElements.activeSelf) return;

        // Prepare a private template while inactive. The original Notes.Start requires both labels.
        GameObject staging = new GameObject("Grave note template");
        staging.SetActive(false);
        GameObject template = Instantiate(inspectionPrefab, staging.transform);
        Notes note = template.GetComponent<Notes>();
        if (note == null) note = template.AddComponent<Notes>();
        foreach (TMP_Text label in template.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.name == "Nombre") note.NameNoteRef = label;
            if (label.name == "Contenido") note.ContentNoteRef = label;
        }
        if (note.NameNoteRef == null || note.ContentNoteRef == null)
        {
            Destroy(staging);
            return;
        }
        note.Nname = noteName;
        note.NCont = noteContent;
        template.SetActive(true);
        inspector.StartGeneralInspection(template);
        Destroy(staging);
        collected = true;
        Destroy(gameObject);
    }
}
