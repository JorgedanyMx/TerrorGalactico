using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PanteonCornerFill
{
    const string Request = "Temp/PanteonCornerFill.request";
    static PanteonCornerFill() { EditorApplication.delayCall += Consume; }
    static void Consume()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Consume; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { SpaceExisting(); }
        catch (Exception e) { File.WriteAllText("Temp/PanteonCornerFill.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Panteon/Double Existing Zone B Spacing")]
    public static void SpaceExisting()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Panteon.unity");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Panteon.unity", OpenSceneMode.Additive);
        var layout = Array.Find(scene.GetRootGameObjects(), go => go.name == "Layout 150x150");
        var zones = layout.transform.Find("Zonas 1 a 4 - tumbas permanentes");
        var zone = zones.Find("Zona 1.B");
        var main = zones.Find("Zona 1 - densidad gradual");
        if (zone == null || main == null) throw new InvalidOperationException("Missing zones.");
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-B-spacing.unity", true);
        var graves = new System.Collections.Generic.List<Transform>();
        foreach (Transform tomb in zone) graves.Add(tomb);
        Vector3 anchor = new Vector3(-69, 0, 69);
        graves.Sort((a,b) => (b.position-anchor).sqrMagnitude.CompareTo((a.position-anchor).sqrMagnitude));
        // Diagonal 2m square footprint spans sqrt(8)m. Double the free gap, not the tomb itself.
        float scale = (2f * 8f - Mathf.Sqrt(8f)) / 8f;
        int moved = 0, retained = 0;
        foreach (var tomb in graves)
        {
            var target = anchor + (tomb.position - anchor) * scale;
            target.y = tomb.position.y;
            bool blocked = target.x > -9 || target.x < -72 || target.z < 9 || target.z > 72;
            foreach (Transform other in main)
                if (Mathf.Abs(target.x-other.position.x) < 4.5f && Mathf.Abs(target.z-other.position.z) < 4.5f) blocked = true;
            // A constrained grave must not be pushed into another retained grave either.
            foreach (var other in graves)
                if (other != tomb && Vector3.Distance(target, other.position) < 4.5f) blocked = true;
            if (blocked) { retained++; continue; }
            if (Vector3.Distance(tomb.position, target) > .01f) { tomb.position = target; moved++; }
        }
        Physics.SyncTransforms();
        if (zone.childCount != graves.Count) throw new InvalidOperationException("Grave count changed.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/PanteonCornerFill.result", "SUCCESS: preserved " + graves.Count + " existing B graves; moved " + moved + "; retained " + retained + " constrained graves; doubled clear row/column spacing wherever Zone 1 and existing obstacles permit. No graves created.");
        Debug.Log("Panteon: increased existing Zone B spacing while preserving deletions.");
    }
    [MenuItem("Tools/Panteon/Add Sparse Zone 1.B")]
    public static void Apply()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Panteon.unity");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Panteon.unity", OpenSceneMode.Additive);
        var layout = Array.Find(scene.GetRootGameObjects(), go => go.name == "Layout 150x150");
        var zones = layout != null ? layout.transform.Find("Zonas 1 a 4 - tumbas permanentes") : null;
        if (zones == null) throw new InvalidOperationException("Cannot find Zonas parent.");
        var existing = zones.Find("Zona 1.B");
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-zone-1B.unity", true);
        var zone = existing != null ? existing.gameObject : new GameObject("Zona 1.B");
        if (existing == null) zone.transform.SetParent(zones, false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/pf_GraveSimple.prefab");
        // Loose outer arc complements Zone 1's dense inner quarter-circle.
        // Preserve existing graves and extend both arms with a softly staggered boundary.
        int count = zone.transform.childCount;
        var mainZone = zones.Find("Zona 1 - densidad gradual");
        for (int row = 0; row < 7; row++)
            for (int column = 0; column < 7; column++)
            {
                float x = 21f + column * 8f;
                float z = 21f + row * 8f;
                float radius = Mathf.Sqrt(x * x + z * z);
                if (radius < 68f + ((row + column) % 3) * 2f) continue;
                var position = new Vector3(-x, 0, z);
                bool crowded = false;
                foreach (Transform other in zone.transform)
                    if (Vector3.Distance(position, other.position) < 7.5f) crowded = true;
                if (mainZone != null)
                    foreach (Transform other in mainZone)
                        if (Vector3.Distance(position, other.position) < 6f) crowded = true;
                if (crowded) continue;
                var tomb = (GameObject)PrefabUtility.InstantiatePrefab(prefab, zone.transform);
                tomb.name = "Tumba simple esquina " + (++count).ToString("00");
                tomb.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 45, 0));
                var lifecycle = tomb.GetComponent<Grave>();
                if (lifecycle != null) UnityEngine.Object.DestroyImmediate(lifecycle);
            }
        Physics.SyncTransforms();
        foreach (Transform tomb in zone.transform)
        {
            if (tomb.GetComponentsInChildren<Collider>().Length == 0) throw new InvalidOperationException("Missing collision.");
            foreach (var renderer in tomb.GetComponentsInChildren<Renderer>())
                if (renderer.bounds.min.x < -74 || renderer.bounds.max.z > 74)
                    throw new InvalidOperationException("Corner tomb overlaps perimeter.");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/PanteonCornerFill.result", "SUCCESS: extended Zona 1.B into an irregular outer arc complementary to Zone 1; " + count + " permanent simple graves; existing graves retained; 7.5m minimum center separation and 6m clearance from Zone 1.");
        Selection.activeGameObject = zone;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(-61, 0, 61), Quaternion.Euler(55, 0, 0), 22);
        Debug.Log("Panteon: extended Zone 1.B as a sparse outer arc.");
    }
}
