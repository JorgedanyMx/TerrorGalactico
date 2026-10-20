using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

// Store the arch as native Mesh assets so the prefab does not rely on OBJ subasset IDs.
[InitializeOnLoad]
public static class CemeteryEntranceMeshRepair
{
    const string PrefabPath = "Assets/Prefabs/pf_CemeteryEntrance.prefab";
    static CemeteryEntranceMeshRepair() { EditorApplication.delayCall += RepairIfNeeded; }

    static void RepairIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RepairIfNeeded;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            if (filter.name.StartsWith("Arch", StringComparison.Ordinal) &&
                (filter.sharedMesh == null || !AssetDatabase.GetAssetPath(filter.sharedMesh).EndsWith(".asset", StringComparison.Ordinal)))
            { Repair(); return; }
    }

    [MenuItem("Tools/Panteon/Repair Entrance Meshes")]
    public static void Repair()
    {
        var names = new[] { "EntranceArch", "EntranceArchTrim", "EntranceArchTrimBack" };
        var objectNames = new[] { "Arch", "ArchTrimFront", "ArchTrimBack" };
        var meshes = new Mesh[3];
        GameObject contents = null;
        try
        {
            for (int k = 0; k < names.Length; k++)
            {
                string path = "Assets/Models/" + names[k];
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                foreach (string line in File.ReadLines(path + ".obj"))
                {
                    var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length == 0) continue;
                    if (tokens[0] == "v") vertices.Add(new Vector3(
                        float.Parse(tokens[1], CultureInfo.InvariantCulture),
                        float.Parse(tokens[2], CultureInfo.InvariantCulture),
                        float.Parse(tokens[3], CultureInfo.InvariantCulture)));
                    if (tokens[0] == "f")
                        for (int i = 2; i < tokens.Length - 1; i++)
                        {
                            triangles.Add(int.Parse(tokens[1].Split('/')[0], CultureInfo.InvariantCulture) - 1);
                            triangles.Add(int.Parse(tokens[i].Split('/')[0], CultureInfo.InvariantCulture) - 1);
                            triangles.Add(int.Parse(tokens[i + 1].Split('/')[0], CultureInfo.InvariantCulture) - 1);
                        }
                }
                if (vertices.Count < 3 || triangles.Count < 3) throw new InvalidDataException(path);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path + ".asset");
                bool create = mesh == null;
                if (create) mesh = new Mesh(); else mesh.Clear();
                mesh.name = names[k];
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                var uv = new Vector2[vertices.Count];
                for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(vertices[i].x / 12f, vertices[i].y / 12f);
                mesh.uv = uv;
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                if (create) AssetDatabase.CreateAsset(mesh, path + ".asset");
                EditorUtility.SetDirty(mesh);
                meshes[k] = mesh;
            }
            contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            for (int k = 0; k < names.Length; k++)
            {
                var child = contents.transform.Find(objectNames[k]);
                if (child == null) throw new InvalidDataException("Missing " + objectNames[k]);
                child.GetComponent<MeshFilter>().sharedMesh = meshes[k];
                var collider = child.GetComponent<MeshCollider>();
                collider.sharedMesh = null;
                collider.sharedMesh = meshes[k];
                if (!child.GetComponent<MeshRenderer>().enabled) throw new InvalidDataException("Renderer disabled");
            }
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            AssetDatabase.SaveAssets();
            for (int k = 0; k < names.Length; k++)
                if (meshes[k].vertexCount != 196 || meshes[k].triangles.Length != 1164)
                    throw new InvalidDataException("Unexpected mesh size: " + names[k]);
            File.WriteAllText("Temp/CemeteryEntranceRepair.txt", "SUCCESS: 3 native meshes assigned to prefab renderers and colliders; each 196 vertices, 388 triangles.");
            Debug.Log("Panteon: repaired entrance arch using 3 native Mesh assets.");
            SceneView.RepaintAll();
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/CemeteryEntranceRepair.txt", "ERROR: " + e);
            Debug.LogException(e);
        }
        finally { if (contents != null) PrefabUtility.UnloadPrefabContents(contents); }
    }
}
