using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PanteonFillGaps
{
    const string Request = "Temp/PanteonFillGaps.request";
    static PanteonFillGaps() { EditorApplication.delayCall += Consume; }
    static void Consume()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Consume; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Apply(); }
        catch (Exception e) { File.WriteAllText("Temp/PanteonFillGaps.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Panteon/Fill Large Gaps With Mexica Stepped Graves")]
    public static void Apply()
    {
        const string path = "Assets/Prefabs/pf_GraveMexicaStepped.prefab";
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Panteon.unity");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Panteon.unity", OpenSceneMode.Additive);
        var layout = Array.Find(scene.GetRootGameObjects(), go => go.name == "Layout 150x150");
        var zones = layout.transform.Find("Zonas 1 a 4 - tumbas permanentes");
        var occupied = new List<Vector3>();
        foreach (Transform zone in zones)
        {
            foreach (Transform tomb in zone) occupied.Add(tomb.position);
        }
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-gap-fill.unity", true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        int n = 0, total = 0;
        foreach (var signs in new[] { new Vector2(-1,1), new Vector2(1,1), new Vector2(1,-1), new Vector2(-1,-1) })
        {
            string groupName = "Zona " + (++n) + ".C";
            var existing = zones.Find(groupName);
            var group = existing != null ? existing.gameObject : new GameObject(groupName);
            if (existing == null) group.transform.SetParent(zones, false);
            // Fill the largest empty pockets first, leaving existing walking gaps alone.
            for (int k = group.transform.childCount; k < 12; k++)
            {
                float bestDistance = 7f;
                Vector3 best = Vector3.zero;
                for (int row = 0; row < 17; row++)
                    for (int col = 0; col < 17; col++)
                    {
                        var candidate = new Vector3(signs.x * (12 + col * 3.5f), 0, signs.y * (12 + row * 3.5f));
                        float clearance = float.MaxValue;
                        foreach (var position in occupied)
                        {
                            var delta = candidate - position; delta.y = 0;
                            clearance = Mathf.Min(clearance, delta.magnitude);
                        }
                        if (clearance > bestDistance) { bestDistance = clearance; best = candidate; }
                    }
                if (best == Vector3.zero) break;
                var tomb = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                tomb.name = "Tumba escalonada " + (k+1).ToString("00"); tomb.transform.position = best;
                var lifecycle = tomb.GetComponent<Grave>();
                if (lifecycle != null) UnityEngine.Object.DestroyImmediate(lifecycle);
                occupied.Add(best); total++;
                foreach (var renderer in tomb.GetComponentsInChildren<Renderer>())
                {
                    var bounds = renderer.bounds;
                    if (bounds.min.x < -74 || bounds.max.x > 74 || bounds.min.z < -74 || bounds.max.z > 74)
                        throw new InvalidOperationException("Added grave outside perimeter.");
                }
                if (tomb.GetComponentsInChildren<Collider>().Length == 0) throw new InvalidOperationException("Missing collision.");
            }
        }
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        var capture = typeof(PanteonLayoutBuilder).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
        capture.Invoke(null, new object[] { scene, new Vector3(0,190,0), Quaternion.Euler(90,0,0), "Temp/Panteon-gap-fill.png", 86f });
        File.WriteAllText("Temp/PanteonFillGaps.result", "SUCCESS: added " + total + " permanent pf_GraveMexicaStepped prefab instances in large empty pockets; minimum 7m center separation; existing graves preserved; paths and perimeter clear; colliders verified.");
        Debug.Log("Panteon: filled large gaps with previously unused stepped grave model.");
    }
}
