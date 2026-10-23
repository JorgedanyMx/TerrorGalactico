using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PanteonStaticGraves
{
    const string Request = "Temp/PanteonStaticGraves.request";
    static PanteonStaticGraves() { EditorApplication.delayCall += Consume; }
    static void Consume()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Consume; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Apply(); }
        catch (Exception e) { File.WriteAllText("Temp/PanteonStaticGraves.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Panteon/Make Grave Prefabs Static")]
    public static void Apply()
    {
        int prefabs = 0, objects = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith("pf_Grave") && !name.StartsWith("pf_TumbaBaja")) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                { t.gameObject.isStatic = true; objects++; }
                PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var t in asset.GetComponentsInChildren<Transform>(true))
                if (!t.gameObject.isStatic) throw new InvalidOperationException("Non-static grave object in " + path);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/PanteonStaticGraves.result", "SUCCESS: marked " + prefabs + " grave prefabs and all " + objects + " objects in their hierarchies Static; saved and verified.");
        Debug.Log("Panteon: grave prefabs marked Static recursively.");
    }
}
