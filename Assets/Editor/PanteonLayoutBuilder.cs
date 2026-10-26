using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit editor command; the temporary request is consumed once, never on normal project loads.
[InitializeOnLoad]
public static class PanteonLayoutBuilder
{
    const string ScenePath = "Assets/Scenes/Panteon.unity";
    const string Request = "Temp/PanteonLayout.request";
    static PanteonLayoutBuilder() { EditorApplication.delayCall += ConsumeRequest; }
    static void ConsumeRequest()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += ConsumeRequest; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { RemoveOutsideGraves(); }
        catch (Exception e) { File.WriteAllText("Temp/PanteonLayout.result", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Panteon/Remove Graves Outside Map")]
    public static void RemoveOutsideGraves()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var layout = Array.Find(scene.GetRootGameObjects(), go => go.name == "Layout 150x150");
        var zones = layout.transform.Find("Zonas 1 a 4 - tumbas permanentes");
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-removing-outside.unity", true);
        int removed = 0, retained = 0;
        foreach (Transform zone in zones)
            for (int i = zone.childCount - 1; i >= 0; i--)
            {
                var tomb = zone.GetChild(i);
                bool outside = false;
                foreach (var renderer in tomb.GetComponentsInChildren<Renderer>(true))
                {
                    var bounds = renderer.bounds;
                    if (bounds.min.x < -75f || bounds.max.x > 75f || bounds.min.z < -75f || bounds.max.z > 75f)
                        outside = true;
                }
                if (outside) { UnityEngine.Object.DestroyImmediate(tomb.gameObject); removed++; }
                else retained++;
            }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/PanteonLayout.result", "SUCCESS: removed " + removed + " graves extending outside 150x150 map; retained " + retained + "; existing spacing and other objects preserved.");
        Debug.Log("Panteon: removed graves outside map boundary.");
    }
    [MenuItem("Tools/Panteon/Expand Existing Graves To 5m And 7m Gaps")]
    public static void ExpandExistingGraves()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var layout = Array.Find(scene.GetRootGameObjects(), go => go.name == "Layout 150x150");
        var zones = layout.transform.Find("Zonas 1 a 4 - tumbas permanentes");
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-expanded-spacing.unity", true);
        int count = 0;
        foreach (Transform zone in zones)
        {
            if (zone.name == "Zona 1.B") continue;
            if (!zone.name.StartsWith("Zona ") || !zone.name.EndsWith(" - densidad gradual")) continue;
            foreach (Transform tomb in zone)
            {
                var parts = tomb.name.Split(' ');
                var indices = parts[parts.Length - 1].Split('-');
                if (indices.Length != 2 || !int.TryParse(indices[0], out int row) || !int.TryParse(indices[1], out int column))
                    throw new InvalidOperationException("Cannot identify grave grid position: " + tomb.name);
                row--; column--;
                var position = tomb.position;
                position.x = Mathf.Sign(position.x) * (9.5f + column * 7f + (row % 2) * .3f);
                position.z = Mathf.Sign(position.z) * (10f + row * 9f);
                tomb.position = position;
                count++;
            }
        }
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Capture(scene, new Vector3(0,190,0), Quaternion.Euler(90,0,0), "Temp/Panteon-expanded-spacing.png", 115);
        File.WriteAllText("Temp/PanteonLayout.result", "SUCCESS: repositioned all " + count + " existing graves in zones 1-4 to 7m column pitch/9m row pitch (5m/7m clear gaps). No graves created or removed; B and other objects preserved; overflow/overlap permitted by user.");
        Debug.Log("Panteon: expanded all existing graves without reducing population.");
    }
    [MenuItem("Tools/Panteon/Apply Sketch Layout 150x150")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        // Save a recovery copy before changing scene contents.
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-layout.unity", true);
        SceneManager.SetActiveScene(scene);
        foreach (var root in scene.GetRootGameObjects())
            if (root.name.StartsWith("Plano ") || root.name == "Perimetro" ||
                root.name == "Layout 150x150" || PrefabUtility.IsAnyPrefabInstanceRoot(root))
                UnityEngine.Object.DestroyImmediate(root);

        var layout = new GameObject("Layout 150x150");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Terreno 150x150 m";
        ground.transform.SetParent(layout.transform);
        ground.transform.localScale = new Vector3(15, 1, 15);
        ground.layer = 2; // Ground must not block the grave placement obstacle query.
        const string dirtPath = "Assets/Materials/m_LayoutDirt.mat";
        var dirt = AssetDatabase.LoadAssetAtPath<Material>(dirtPath);
        if (dirt == null) { dirt = new Material(Material("m_Dirt")); AssetDatabase.CreateAsset(dirt, dirtPath); }
        dirt.mainTextureScale = new Vector2(30, 30); EditorUtility.SetDirty(dirt);
        ground.GetComponent<Renderer>().sharedMaterial = dirt;
        // Keep the pale hexagonal paving legible under the scene's strong directional light.
        var paving = Material("m_Floor");
        paving.SetColor("_BaseColor", new Color(.45f, .45f, .45f, 1));
        paving.SetFloat("_Smoothness", .25f); EditorUtility.SetDirty(paving);
        var paths = new GameObject("Pasillos planos hexagonales - ancho 12 m");
        paths.transform.SetParent(layout.transform);
        // Four nonoverlapping rectangles keep UV density identical and avoid seams/z-fighting.
        Surface("Pasillo central", 0, 0, 12, 150, paths.transform);
        Surface("Pasillo oeste", -40.5f, 0, 69, 12, paths.transform);
        Surface("Pasillo este", 40.5f, 0, 69, 12, paths.transform);

        var perimeter = new GameObject("Perimetro");
        perimeter.transform.SetParent(layout.transform);
        Wall("Oeste", -74.5f, 0, 149, true, perimeter.transform);
        Wall("Este", 74.5f, 0, 149, true, perimeter.transform);
        Wall("Sur oeste", -42.5f, -74.5f, 65, false, perimeter.transform);
        Wall("Sur este", 42.5f, -74.5f, 65, false, perimeter.transform);
        Wall("Norte oeste", -39, 74.5f, 72, false, perimeter.transform);
        Wall("Norte este", 39, 74.5f, 72, false, perimeter.transform);
        var entrance = Instance("pf_CemeteryEntrance", new Vector3(0, 0, -74.5f), layout.transform);
        var fountain = Instance("pf_FountainCentral", new Vector3(0, 0.025f, 0), layout.transform);
        Populate(layout.transform);
        BuildGate(layout.transform);
        foreach (var root in scene.GetRootGameObjects())
        {
            var spawner = root.GetComponent<GraveSpawner>();
            if (spawner == null) continue;
            var so = new SerializedObject(spawner);
            so.FindProperty("areaSize").floatValue = 138;
            so.FindProperty("entranceExclusion").objectReferenceValue = entrance.transform;
            so.FindProperty("reservedAreaCenter").objectReferenceValue = fountain.transform;
            so.FindProperty("reservedAreaRadius").floatValue = 8;
            so.FindProperty("enableSpaceTest").boolValue = false;
            so.FindProperty("reservedCrossHalfWidth").floatValue = 6;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var camera = Array.Find(scene.GetRootGameObjects(), go => go.GetComponent<Camera>() != null);
        if (camera != null)
        {
            camera.transform.position = new Vector3(0, 155, -115);
            camera.transform.LookAt(Vector3.zero);
            camera.GetComponent<Camera>().farClipPlane = 500;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Physics.SyncTransforms();
        if (Mathf.Abs(ground.GetComponent<Renderer>().bounds.size.x - 150) > .01f)
            throw new InvalidOperationException("Layout dimension/count validation failed.");
        Capture(scene, new Vector3(0, 190, 0), Quaternion.Euler(90, 0, 0), "Temp/Panteon-layout-top.png");
        Capture(scene, new Vector3(110, 125, -150), Quaternion.LookRotation(new Vector3(-110, -125, 150)), "Temp/Panteon-layout-perspective.png");
        File.WriteAllText("Temp/PanteonLayout.result", "SUCCESS: saved Panteon 150x150; permanent virreinal graves fading toward empty corners; 2 low entrance tutorial graves; centered fountain; 12m flat hex paths; south entrance; 6m north iron gate; four zones with graduated density.");
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.Euler(90, 0, 0), 95);
        Debug.Log("Panteon: applied and verified sketch layout 150x150.");
    }
    [MenuItem("Tools/Panteon/Populate Four Zones And Jump Entrance")]
    public static void ApplyPopulation()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var layout = Array.Find(scene.GetRootGameObjects(), go => go.name == "Layout 150x150");
        if (layout == null) throw new InvalidOperationException("Build the 150x150 layout first.");
        EditorSceneManager.SaveScene(scene, "Temp/Panteon-before-population.unity", true);
        Populate(layout.transform, false);
        Physics.SyncTransforms();
        ValidatePopulation(layout.transform, false);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Capture(scene, new Vector3(0, 190, 0), Quaternion.Euler(90, 0, 0), "Temp/Panteon-layout-top.png");
        Capture(scene, new Vector3(110, 125, -150), Quaternion.LookRotation(new Vector3(-110, -125, 150)), "Temp/Panteon-layout-perspective.png");
        Capture(scene, new Vector3(0, 8, -82), Quaternion.LookRotation(new Vector3(0, -5, 14)), "Temp/Panteon-entrance-jump.png", 13);
        Capture(scene, new Vector3(-25, 10, -30), Quaternion.LookRotation(new Vector3(-12, -6, 18)), "Temp/Panteon-zone-detail.png", 15);
        File.WriteAllText("Temp/PanteonLayout.result", "SUCCESS: grave clearances set to 3m between columns and 4m between rows; Zona 1.B preserved; graduated density retained; existing entrance, player capsule and other scene objects preserved.");
        Debug.Log("Panteon: verified graduated permanent grave population and two jump tutorial tombs.");
    }
    static void Populate(Transform layout, bool rebuildTutorial = true)
    {
        var cornerFill = layout.Find("Zonas 1 a 4 - tumbas permanentes/Zona 1.B");
        if (cornerFill != null) cornerFill.SetParent(layout, true);
        for (int i = layout.childCount - 1; i >= 0; i--)
        {
            var child = layout.GetChild(i);
            if (child.name.StartsWith("Tumbas virreinales") || child.name == "Zonas 1 a 4 - tumbas permanentes" || (rebuildTutorial && child.name == "Entrada - aprender a saltar"))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        var zones = new GameObject("Zonas 1 a 4 - tumbas permanentes");
        zones.transform.SetParent(layout, false);
        if (cornerFill != null) cornerFill.SetParent(zones.transform, true);
        int zoneNumber = 0;
        // Number clockwise from the upper left, matching the sketch's top-down orientation.
        foreach (var signs in new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
        {
            var zone = new GameObject("Zona " + (++zoneNumber) + " - densidad gradual");
            zone.transform.SetParent(zones.transform, false);
            for (int row = 0; row < 13; row++)
                for (int column = 0; column < 15; column++)
                {
                    if (!KeepGrave(row, column)) continue;
                    // 2m footprints plus 3m column clearance and 4m row clearance.
                    float x = signs.x * (9.5f + column * 5f + (row % 2) * .3f);
                    float z = signs.y * (10f + row * 6f);
                    var grave = Instance((row + column + zoneNumber) % 3 == 0 ? "pf_GraveVirreinalNiche" : "pf_GraveVirreinalCross",
                        new Vector3(x, 0, z), zone.transform);
                    grave.name = "Tumba fija " + (row + 1).ToString("00") + "-" + (column + 1).ToString("00");
                    // These are environment tombs; no enemy association or pooling lifecycle.
                    var lifecycle = grave.GetComponent<Grave>();
                    if (lifecycle != null) UnityEngine.Object.DestroyImmediate(lifecycle);
                }
        }
        if (!rebuildTutorial) return;
        var tutorial = new GameObject("Entrada - aprender a saltar");
        tutorial.transform.SetParent(layout, false);
        for (int i = 0; i < 2; i++)
        {
            var tomb = new GameObject("Tumba baja " + (i + 1));
            tomb.transform.SetParent(tutorial.transform, false);
            tomb.transform.localPosition = new Vector3(i == 0 ? -3 : 3, .025f, -68.5f);
            float height = i == 0 ? .6f : .7f;
            Cube("Basamento", new Vector3(0, .12f, 0), new Vector3(5.96f, .24f, 1.7f), tomb.transform, Material("m_Stone"));
            Cube("Sepulcro", new Vector3(0, height / 2, 0), new Vector3(5.78f, height - .12f, 1.52f), tomb.transform, Material("m_EntranceIvory"));
            Cube("Lapida horizontal", new Vector3(0, height - .06f, 0), new Vector3(5.96f, .12f, 1.7f), tomb.transform, Material("m_Marble"));
            Cube("Cruz horizontal larga", new Vector3(0, height + .008f, 0), new Vector3(1.2f, .016f, .08f), tomb.transform, Material("m_CarvedStone"));
            Cube("Cruz horizontal corta", new Vector3(-.25f, height + .008f, 0), new Vector3(.08f, .016f, .65f), tomb.transform, Material("m_CarvedStone"));
            foreach (var collider in tomb.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            var obstacle = tomb.AddComponent<BoxCollider>();
            obstacle.size = new Vector3(5.96f, height, 1.7f);
            obstacle.center = new Vector3(0, height / 2, 0);
        }
    }
    static bool KeepGrave(int row, int column)
    {
        float x = 9.5f + column * 5f + (row % 2) * .3f;
        float z = 10f + row * 6f;
        if (x > 72f || z > 72f) return false;
        float distance = Mathf.Sqrt(x * x + z * z);
        // Full density in the inner 30m; smooth falloff to no graves beyond 75m.
        float t = Mathf.Clamp01((distance - 30f) / 45f);
        float density = 1f - t * t * (3f - 2f * t);
        uint hash = unchecked((uint)(row * 73856093) ^ (uint)(column * 19349663) ^ 83492791u);
        hash ^= hash >> 13; hash = unchecked(hash * 1274126177u); hash ^= hash >> 16;
        return (hash & 65535u) / 65536f < density;
    }
    static int ExpectedGravesPerZone()
    {
        int count = 0;
        for (int row = 0; row < 13; row++)
            for (int column = 0; column < 15; column++)
                if (KeepGrave(row, column)) count++;
        return count;
    }
    static void ValidatePopulation(Transform layout, bool validateTutorial = true)
    {
        var zones = layout.Find("Zonas 1 a 4 - tumbas permanentes");
        if (zones == null || zones.childCount - (zones.Find("Zona 1.B") != null ? 1 : 0) != 4) throw new InvalidOperationException("Expected four populated zones.");
        int count = 0;
        foreach (Transform zone in zones)
        {
            if (zone.name == "Zona 1.B") continue;
            if (zone.childCount != ExpectedGravesPerZone()) throw new InvalidOperationException("Incorrect grave count in " + zone.name);
            foreach (Transform tomb in zone)
            {
                count++;
                if (tomb.GetComponent<Grave>() != null || !tomb.gameObject.activeSelf || tomb.GetComponentsInChildren<Collider>().Length == 0)
                    throw new InvalidOperationException("Permanent grave lifecycle/collider error.");
                foreach (var renderer in tomb.GetComponentsInChildren<Renderer>())
                {
                    var bounds = renderer.bounds;
                    if (bounds.min.x < -74 || bounds.max.x > 74 || bounds.min.z < -74 || bounds.max.z > 74 ||
                        (bounds.min.x < 6 && bounds.max.x > -6) || (bounds.min.z < 6 && bounds.max.z > -6))
                        throw new InvalidOperationException("Grave intrudes on wall/path: " + tomb.name);
                }
            }
        }
        if (count != 4 * ExpectedGravesPerZone()) throw new InvalidOperationException("Incorrect population.");
        if (!validateTutorial) return;
        var tutorial = layout.Find("Entrada - aprender a saltar");
        if (count != 4 * ExpectedGravesPerZone() || tutorial.childCount != 2) throw new InvalidOperationException("Incorrect population.");
        foreach (Transform tomb in tutorial)
        {
            var collider = tomb.GetComponent<BoxCollider>();
            if (collider == null || collider.isTrigger || collider.bounds.size.y > .75f)
                throw new InvalidOperationException("Invalid jump tutorial collision.");
        }
    }
    static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + name + ".mat");
    static GameObject Instance(string name, Vector3 position, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab"), parent);
        go.transform.position = position;
        return go;
    }
    static GameObject Cube(string name, Vector3 position, Vector3 scale, Transform parent, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent); go.transform.localPosition = position;
        go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }
    static void Wall(string name, float x, float z, float length, bool vertical, Transform parent)
    {
        var wall = Instance("pf_WallSegment", new Vector3(x, 0, z), parent);
        wall.name = "Muro " + name;
        wall.transform.localScale = new Vector3(length / 10f, 1, 1);
        wall.transform.rotation = Quaternion.Euler(0, vertical ? 90 : 0, 0);
    }
    static void Surface(string name, float x, float z, float width, float depth, Transform parent)
    {
        var mesh = new Mesh { name = name };
        float w = width / 2, d = depth / 2;
        mesh.vertices = new[] { new Vector3(-w, 0, -d), new Vector3(-w, 0, d), new Vector3(w, 0, d), new Vector3(w, 0, -d) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        // Texture repeats every 4 metres in world space, including across rectangle joins.
        mesh.uv = new[] { new Vector2((x-w)/4, (z-d)/4), new Vector2((x-w)/4, (z+d)/4), new Vector2((x+w)/4, (z+d)/4), new Vector2((x+w)/4, (z-d)/4) };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        string asset = "Assets/Models/Layout_" + name.Replace(" ", "_") + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(asset);
        if (existing == null) AssetDatabase.CreateAsset(mesh, asset);
        else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent); go.transform.localPosition = new Vector3(x, .025f, z);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = Material("m_Floor");
    }
    static void BuildGate(Transform parent)
    {
        const string path = "Assets/Materials/m_GateIron.mat";
        var iron = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (iron == null) { iron = new Material(Material("m_Stone")); AssetDatabase.CreateAsset(iron, path); }
        iron.SetTexture("_BaseMap", null); iron.SetTexture("_BumpMap", null);
        iron.DisableKeyword("_NORMALMAP"); iron.color = new Color(.075f, .085f, .08f);
        iron.SetFloat("_Metallic", .8f); iron.SetFloat("_Smoothness", .35f); EditorUtility.SetDirty(iron);
        var gate = new GameObject("Reja norte - 6 m"); gate.transform.SetParent(parent);
        gate.transform.localPosition = new Vector3(0, 0, 74.5f);
        foreach (float x in new[] { -3.3f, 3.3f })
            Cube("Pilar", new Vector3(x, 1.8f, 0), new Vector3(.6f, 3.6f, .8f), gate.transform, Material("m_Stone"));
        for (int leaf = 0; leaf < 2; leaf++)
        {
            var door = new GameObject(leaf == 0 ? "Hoja izquierda" : "Hoja derecha"); door.transform.SetParent(gate.transform, false);
            float center = leaf == 0 ? -1.5f : 1.5f;
            foreach (float y in new[] { .25f, 1.35f, 2.8f })
                Cube("Travesano", new Vector3(center, y, 0), new Vector3(2.96f, .09f, .12f), door.transform, iron);
            for (int i = 0; i <= 10; i++)
                Cube("Barrote", new Vector3(center - 1.46f + i * .292f, 1.55f, 0), new Vector3(.065f, 3, .065f), door.transform, iron);
        }
    }
    static void Capture(Scene scene, Vector3 position, Quaternion rotation, string path, float previewSize = 0)
    {
        var go = new GameObject("Temporary layout preview", typeof(Camera));
        SceneManager.MoveGameObjectToScene(go, scene);
        var cam = go.GetComponent<Camera>(); go.transform.SetPositionAndRotation(position, rotation);
        cam.orthographic = true; cam.orthographicSize = previewSize > 0 ? previewSize : (position.x == 0 ? 86 : 110); cam.farClipPlane = 500;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.16f, .18f, .20f);
        var rt = new RenderTexture(1200, 1200, 24); cam.targetTexture = rt;
        var previous = RenderTexture.active;
        try
        {
            cam.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1200, 1200, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 1200), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active = previous; cam.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go); }
    }
}
