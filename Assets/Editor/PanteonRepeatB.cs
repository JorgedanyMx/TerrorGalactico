using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PanteonRepeatB
{
    const string Request = "Temp/PanteonRepeatB.request";
    static PanteonRepeatB() { EditorApplication.delayCall += Consume; }
    static void Consume()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Consume; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Apply(); }
        catch (Exception e) { File.WriteAllText("Temp/PanteonRepeatB.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Panteon/Repeat Zone B In Zones 2 3 4")]
    public static void Apply()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Panteon.unity");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Panteon.unity", OpenSceneMode.Additive);
        var layout = Array.Find(scene.GetRootGameObjects(), go => go.name == "Layout 150x150");
        var zones = layout.transform.Find("Zonas 1 a 4 - tumbas permanentes");
        var source = zones.Find("Zona 1.B");
        if (source == null) throw new InvalidOperationException("Missing Zone 1.B.");
        for (int n = 2; n <= 4; n++)
            if (zones.Find("Zona " + n + ".B") != null) throw new InvalidOperationException("Target B zone already exists.");
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-repeat-B.unity", true);
        int created = 0, removed = 0;
        for (int n = 2; n <= 4; n++)
        {
            var target = new GameObject("Zona " + n + ".B");
            target.transform.SetParent(zones, false);
            var main = zones.Find("Zona " + n + " - densidad gradual");
            foreach (Transform original in source)
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(original.gameObject);
                if (string.IsNullOrEmpty(path)) path = "Assets/Prefabs/pf_GraveSimple.prefab";
                var tomb = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), target.transform);
                tomb.name = original.name;
                Vector3 position = original.position;
                if (n == 2 || n == 3) position.x = -position.x;
                if (n == 3 || n == 4) position.z = -position.z;
                float yaw = original.eulerAngles.y;
                if (n == 2 || n == 3) yaw = -yaw;
                if (n == 3 || n == 4) yaw = 180f - yaw;
                tomb.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
                tomb.transform.localScale = original.lossyScale;
                var lifecycle = tomb.GetComponent<Grave>();
                if (lifecycle != null) UnityEngine.Object.DestroyImmediate(lifecycle);
                created++;
                // B takes precedence: remove main-zone graves whose X/Z footprints overlap.
                var bBounds = BoundsOf(tomb);
                if (main != null)
                    for (int i = main.childCount - 1; i >= 0; i--)
                    {
                        var other = main.GetChild(i);
                        var bounds = BoundsOf(other.gameObject);
                        if (bBounds.min.x < bounds.max.x && bBounds.max.x > bounds.min.x &&
                            bBounds.min.z < bounds.max.z && bBounds.max.z > bounds.min.z)
                        { UnityEngine.Object.DestroyImmediate(other.gameObject); removed++; }
                    }
            }
            if (target.transform.childCount != source.childCount) throw new InvalidOperationException("B copy count mismatch.");
        }
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/PanteonRepeatB.result", "SUCCESS: created Zones 2.B, 3.B, 4.B with " + source.childCount + " graves each (" + created + " linked prefab instances); removed " + removed + " overlapping main-zone graves, preserving B. Original 1.B retained.");
        Debug.Log("Panteon: repeated corner B in zones 2-4 with B overlap priority.");
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("Grave has no renderers.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
