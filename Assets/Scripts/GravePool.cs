using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GravePool : MonoBehaviour
{
    [Tooltip("Assign Assets/Prefabs/pf_GraveSimple.prefab. Grave is added automatically if missing.")]
    [SerializeField] private GameObject gravePrefab;
    [SerializeField, Min(0)] private int initialSize = 20;
    [SerializeField, Min(1)] private int maxSize = 200;

    private readonly Queue<Grave> available = new Queue<Grave>();
    private readonly HashSet<Grave> active = new HashSet<Grave>();
    private readonly HashSet<Grave> owned = new HashSet<Grave>();
    private Transform storage;
    private bool initialized;
    private bool shuttingDown;

    public int ActiveCount => active.Count;
    public IReadOnlyCollection<Grave> ActiveGraves => active;

    private void Awake() => Initialize();

    private bool Initialize()
    {
        if (initialized) return true;
        if (gravePrefab == null)
        {
            Debug.LogError("GravePool: assign pf_GraveSimple to Grave Prefab.", this);
            return false;
        }

        // Instantiate under an inactive parent so pooled objects never briefly activate.
        var holder = new GameObject("Inactive Graves");
        holder.SetActive(false);
        storage = holder.transform;
        storage.SetParent(transform, false);
        initialized = true;
        for (int i = 0; i < Mathf.Clamp(initialSize, 0, maxSize); i++)
            available.Enqueue(CreateGrave());
        return true;
    }

    private Grave CreateGrave()
    {
        var instance = Instantiate(gravePrefab, storage);
        instance.SetActive(false);
        var grave = instance.GetComponent<Grave>();
        if (grave == null) grave = instance.AddComponent<Grave>();
        grave.Initialize(this);
        owned.Add(grave);
        return grave;
    }

    public bool HasEnemy(IGraveEnemy enemy)
    {
        foreach (var grave in active)
            if (grave != null && ReferenceEquals(grave.Enemy, enemy)) return true;
        return false;
    }

    /// <summary>Returns false when full, unconfigured, or this enemy already owns a grave.</summary>
    public bool TryGet(Vector3 position, Quaternion rotation, IGraveEnemy enemy, out Grave grave)
    {
        grave = null;
        if (shuttingDown || !isActiveAndEnabled || enemy == null ||
            (enemy is UnityEngine.Object enemyObject && enemyObject == null) ||
            !Initialize() || HasEnemy(enemy)) return false;

        owned.RemoveWhere(item => item == null);
        while (available.Count > 0 && grave == null) grave = available.Dequeue();
        if (grave == null)
        {
            if (owned.Count >= maxSize) return false;
            grave = CreateGrave();
        }

        grave.transform.SetParent(transform, true);
        grave.transform.SetPositionAndRotation(position, rotation);
        active.Add(grave);
        grave.Bind(enemy);
        grave.gameObject.SetActive(true);
        // An enemy could defeat itself in an OnEnable callback.
        if (!grave.IsInUse)
        {
            grave = null;
            return false;
        }
        return true;
    }

    public void Return(Grave grave)
    {
        if (shuttingDown || grave == null || !active.Remove(grave)) return;
        // Clear before disabling, because OnDisable also requests a return.
        grave.ClearAssociation();
        grave.gameObject.SetActive(false);
        grave.transform.SetParent(storage, true);
        available.Enqueue(grave);
    }

    private void OnDisable()
    {
        foreach (var grave in new List<Grave>(active)) Return(grave);
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        foreach (var grave in owned)
            if (grave != null) grave.ClearAssociation();
        active.Clear();
        available.Clear();
        owned.Clear();
    }

    private void OnValidate()
    {
        maxSize = Mathf.Max(1, maxSize);
        initialSize = Mathf.Clamp(initialSize, 0, maxSize);
    }
}
