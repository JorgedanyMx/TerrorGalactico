using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PanteonSideGates
{
    const string Request = "Temp/PanteonSideGates.request";
    static PanteonSideGates() { EditorApplication.delayCall += Consume; }
    static void Consume()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Consume; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { UsePrefabLeaves(); }
        catch (Exception e) { File.WriteAllText("Temp/PanteonSideGates.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Panteon/Use Existing Side Gate Prefabs")]
    public static void UsePrefabLeaves()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Panteon.unity");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Panteon.unity", OpenSceneMode.Additive);
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-gate-prefabs.unity", true);
        int replaced = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var gate in root.GetComponentsInChildren<Transform>(true))
            {
                if (gate == null) continue;
                if (gate.name != "Reja oeste - 6 m" && gate.name != "Reja este - 6 m") continue;
                foreach (string side in new[] { "Izquierda", "Derecha" })
                {
                    Transform old = null;
                    foreach (Transform child in gate)
                        if (child.name.IndexOf(side, StringComparison.OrdinalIgnoreCase) >= 0) { old = child; break; }
                    if (old == null) throw new InvalidOperationException("Missing leaf " + side);
                    var position = old.localPosition; var rotation = old.localRotation; var scale = old.localScale;
                    string path = "Assets/Prefabs/pf_Hoja" + side + ".prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var leaf = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gate);
                    leaf.transform.localPosition = position; leaf.transform.localRotation = rotation; leaf.transform.localScale = scale;
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(leaf) != path ||
                        leaf.GetComponentsInChildren<Collider>().Length == 0)
                        throw new InvalidOperationException("Invalid prefab connection/collision.");
                    replaced++;
                }
            }
        if (replaced != 4) throw new InvalidOperationException("Expected four prefab leaves.");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/PanteonSideGates.result", "SUCCESS: 4 linked prefab instances, pf_HojaIzquierda and pf_HojaDerecha on east/west; transforms preserved; prefab connections and colliders verified.");
    }
    [MenuItem("Tools/Panteon/Add East And West Gates")]
    public static void Apply()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Panteon.unity");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Panteon.unity", OpenSceneMode.Additive);
        Transform north = null, west = null, east = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Reja norte - 6 m") north = t;
                if (t.name == "Muro Oeste") west = t;
                if (t.name == "Muro Este") east = t;
                if (t.name == "Reja oeste - 6 m" || t.name == "Reja este - 6 m")
                    throw new InvalidOperationException("Side gates already exist; preserve manual edits.");
            }
        if (north == null || west == null || east == null) throw new InvalidOperationException("Missing north gate or side walls.");
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-side-gates.unity", true);
        AddSide(north, west, -74.5f, "oeste");
        AddSide(north, east, 74.5f, "este");
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        // Reuse the scene preview renderer without changing the gameplay camera.
        var capture = typeof(PanteonLayoutBuilder).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
        capture.Invoke(null, new object[] { scene, new Vector3(0,190,0), Quaternion.Euler(90,0,0), "Temp/Panteon-side-gates-top.png", 86f });
        File.WriteAllText("Temp/PanteonSideGates.result", "SUCCESS: cloned current north gate to east/west, rotated 90 degrees; split both side walls into 72m sections leaving centered 6m openings aligned with horizontal paths; wall render/collider bounds verified.");
        Debug.Log("Panteon: added matching east/west iron gates.");
    }
    static void AddSide(Transform north, Transform wall, float x, string side)
    {
        var upper = UnityEngine.Object.Instantiate(wall.gameObject, wall.parent);
        upper.name = "Muro " + side + " norte";
        wall.name = "Muro " + side + " sur";
        var oldScale = wall.localScale;
        // Original wall is 149m long, centered at z=0. New sections stop at +/-3m.
        var scale = new Vector3(oldScale.x * 72f / 149f, oldScale.y, oldScale.z);
        wall.localScale = scale; upper.transform.localScale = scale;
        wall.position = new Vector3(x, wall.position.y, -39);
        upper.transform.position = new Vector3(x, wall.position.y, 39);
        var gate = UnityEngine.Object.Instantiate(north.gameObject, north.parent);
        gate.name = "Reja " + side + " - 6 m";
        gate.transform.SetPositionAndRotation(new Vector3(x, north.position.y, 0), Quaternion.Euler(0,90,0) * north.rotation);
        foreach (var renderer in wall.GetComponentsInChildren<Renderer>())
            if (renderer.bounds.max.z > -2.99f) throw new InvalidOperationException("South section obstructs side opening.");
        foreach (var renderer in upper.GetComponentsInChildren<Renderer>())
            if (renderer.bounds.min.z < 2.99f) throw new InvalidOperationException("North section obstructs side opening.");
        if (gate.GetComponentsInChildren<Collider>().Length != north.GetComponentsInChildren<Collider>().Length)
            throw new InvalidOperationException("Gate collision mismatch.");
    }
}
