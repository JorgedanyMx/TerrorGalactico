using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-time integration of the offering example into the saved ShigePrueba scene.</summary>
[InitializeOnLoad]
public static class ShigePruebaOfferingSetup
{
    private const string ScenePath = "Assets/Scenes/TestingScenes/ShigePrueba.unity";
    private const string Request = "Temp/ShigePruebaOfferingSetup.request";
    private const string Result = "Temp/ShigePruebaOfferingSetup.result";

    static ShigePruebaOfferingSetup() { EditorApplication.delayCall += ConsumeRequest; }

    private static void ConsumeRequest()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ConsumeRequest;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch (Exception exception)
        {
            File.WriteAllText(Result, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Gameplay/Set Up ShigePrueba Offerings")]
    public static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open and save ShigePrueba in Edit mode before setup.");
        if (Object.FindFirstObjectByType<EnemyGraveReturn>() != null)
            throw new InvalidOperationException("This scene already has an offering setup. Adjust its existing objects instead.");
        if (!File.Exists("Temp/ShigePrueba-before-offerings.unity"))
            File.Copy(ScenePath, "Temp/ShigePrueba-before-offerings.unity");

        GameObject environment = scene.GetRootGameObjects().Single(go => go.name == "PlayableZone");
        GameObject player = GameObject.FindWithTag("Player");
        GameEvent offeringEvent = AssetDatabase.LoadAssetAtPath<GameEvent>("Assets/ScriptableObjects/OfferingReceived.asset");
        if (player == null || offeringEvent == null) throw new InvalidOperationException("Player or offering event is missing.");

        // The existing ground surface references another test scene's shared data.
        // Replace only the scene component and save a new, independent NavMesh asset.
        foreach (NavMeshSurface previous in environment.GetComponentsInChildren<NavMeshSurface>())
        {
            previous.RemoveData();
            Object.DestroyImmediate(previous);
        }
        NavMeshSurface surface = environment.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = ~0;
        surface.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
        surface.BuildNavMesh();
        if (surface.navMeshData == null) throw new InvalidOperationException("NavMesh bake failed.");
        const string navFolder = "Assets/Scenes/TestingScenes/ShigePrueba";
        if (!AssetDatabase.IsValidFolder(navFolder)) AssetDatabase.CreateFolder("Assets/Scenes/TestingScenes", "ShigePrueba");
        string navPath = AssetDatabase.GenerateUniqueAssetPath(navFolder + "/NavMesh-OfferingTest.asset");
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);

        Transform[] graves =
        {
            Find(environment, "Zona 1 - densidad gradual/Tumba fija 01-01"),
            Find(environment, "Zona 2 - densidad gradual/Tumba fija 01-01"),
            Find(environment, "Zona 4 - densidad gradual/Tumba fija 01-01")
        };
        Vector3[] desired = { new Vector3(-7, 0, 10), new Vector3(7, 0, 10), new Vector3(-7, 0, -10) };
        Transform[] returnPoints = new Transform[graves.Length];
        Vector3 enemyPosition = Sample(new Vector3(-7.45f, 0, 6));
        for (int i = 0; i < graves.Length; i++)
        {
            returnPoints[i] = new GameObject("ReturnPoint").transform;
            returnPoints[i].SetParent(graves[i]);
            returnPoints[i].position = Sample(desired[i]);
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(enemyPosition, returnPoints[i].position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Grave is unreachable: " + graves[i].parent.name);
        }

        // Only this scene instance receives a fallback for the missing Character model.
        Transform missingCharacter = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Character");
        if (missingCharacter != null) Object.DestroyImmediate(missingCharacter.gameObject);
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "PlayerPlaceholder";
        visual.transform.SetParent(player.transform, false);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);

        p_CursorHandler cursorHandler = player.GetComponentInChildren<p_CursorHandler>();
        Transform cursor = (Transform)new SerializedObject(cursorHandler).FindProperty("cursorObject").objectReferenceValue;
        cursor.localPosition = Vector3.forward * cursorHandler.cursorDistance;
        Record(cursor);
        GameObject projectile = CreateProjectile(offeringEvent);
        PlayerOffering playerOffering = player.AddComponent<PlayerOffering>();
        Set(playerOffering, "offeringPrefab", projectile);
        Set(playerOffering, "aimTarget", cursor);
        Set(playerOffering, "throwSpeed", 12f);

        // Keep the existing collection/counter flow and replace its placeholder callback.
        PieceCounter counter = Object.FindFirstObjectByType<PieceCounter>();
        var collectedSet = new UnityEvent();
        UnityEventTools.AddPersistentListener(collectedSet, playerOffering.AddOffering);
        Set(counter, "onTargetReached", collectedSet);
        Set(counter, "piecesNeeded", 3);
        Set(counter, "resetAfterReached", true);
        CameraAreaSpawner spawner = counter.GetComponent<CameraAreaSpawner>();
        Set(spawner, "appearOnStart", false);
        Set(spawner, "intervalRange", new Vector2(15, 20));
        Set(spawner, "stayRange", new Vector2(2, 3));
        Set(spawner, "useCustomArea", true);
        Set(spawner, "customCenterOffset", new Vector2(-8, 0));
        Set(spawner, "customSize", new Vector2(8, 12));
        Set(spawner, "pieceLifetime", 60f);
        Set(spawner, "blockedLayers", (LayerMask)1);

        string[] piecePaths = { "Calaverita", "Cempasuchill", "PanDeMuerto" };
        Vector3[] piecePositions = { new Vector3(-8.9f, .3f, 1.25f), new Vector3(-7.45f, .3f, -.3f), new Vector3(-6f, .3f, 1.25f) };
        for (int i = 0; i < piecePaths.Length; i++)
        {
            GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/" + piecePaths[i] + ".prefab"), scene);
            piece.name = "OfferingPiece_" + (i + 1);
            piece.transform.position = piecePositions[i];
            Record(piece);
            Record(piece.transform);
        }

        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Testing/Users/Oscar/EnemyPrefabs/Bacteria Enemy.prefab"), scene);
        enemy.name = "OfferingTestEnemy";
        enemy.tag = "Ghost";
        enemy.transform.position = enemyPosition;
        enemy.transform.rotation = Quaternion.Euler(0, 180, 0);
        foreach (Transform child in enemy.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        Rigidbody body = enemy.GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Record(body);
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        agent.radius = .4f;
        agent.height = 2.8f;
        agent.speed = 3.5f;
        agent.angularSpeed = 360;
        agent.stoppingDistance = .2f;
        Record(agent);
        Enemy_FSM brain = enemy.GetComponent<Enemy_FSM>();
        Set(brain, "m_objective", player.transform);
        Set(brain, "m_playerMask", (LayerMask)1);
        Set(brain, "m_obstacleMask", (LayerMask)0);
        Set(brain, "m_AIGroundMask", (LayerMask)(1 << 2));
        Set(brain, "m_patrolRadious", 4f);
        Set(brain, "m_sightRadious", 8f);
        Set(brain, "m_sightAngle", 70f);
        Set(brain, "m_attackRadious", 1.6f);
        Record(enemy);
        Record(enemy.transform);

        GameObject manager = new GameObject("OfferingManager");
        manager.SetActive(false);
        EnemyGraveReturn graveReturn = manager.AddComponent<EnemyGraveReturn>();
        Set(graveReturn, "gravePoints", returnPoints);
        GameEventListener listener = manager.AddComponent<GameEventListener>();
        listener.Event = offeringEvent;
        listener.Response = new UnityEvent();
        listener.ResponseWithTarget = new UnityEvent<GameObject>();
        UnityEventTools.AddPersistentListener(listener.ResponseWithTarget, graveReturn.SendEnemyToRandomGrave);
        manager.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save configured scene.");
        Selection.activeGameObject = manager;
        File.WriteAllText(Result, "PASS: ShigePrueba saved; independent NavMesh; three reachable grave points; player offering input; collection event; throwable prefab; animated enemy; targeted GameEvent listener.");
        Debug.Log("ShigePrueba offering setup complete.");
    }

    private static GameObject CreateProjectile(GameEvent offeringEvent)
    {
        GameObject projectile = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/PanDeMuerto.prefab"));
        PrefabUtility.UnpackPrefabInstance(projectile, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        projectile.name = "OfferingProjectile";
        Object.DestroyImmediate(projectile.GetComponent<PiecePickup>());
        foreach (Light light in projectile.GetComponentsInChildren<Light>()) Object.DestroyImmediate(light.gameObject);
        projectile.transform.position = Vector3.zero;
        Rigidbody body = projectile.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        foreach (Collider collider in projectile.GetComponentsInChildren<Collider>()) collider.isTrigger = true;
        GhostHitTrigger hit = projectile.AddComponent<GhostHitTrigger>();
        var onHit = new UnityEvent<GameObject>();
        UnityEventTools.AddPersistentListener(onHit, new UnityAction<GameObject>(offeringEvent.Raise));
        Set(hit, "onGhostHit", onHit);
        projectile.AddComponent<OfferingProjectile>();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(projectile, "Assets/Prefabs/Items/OfferingProjectile.prefab");
        Object.DestroyImmediate(projectile);
        return saved;
    }

    private static Transform Find(GameObject environment, string suffix)
    {
        return environment.GetComponentsInChildren<Transform>(true).Single(t => t.parent != null && t.parent.name + "/" + t.name == suffix);
    }

    private static Vector3 Sample(Vector3 point)
    {
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            throw new InvalidOperationException("No navigable ground near " + point);
        return hit.position;
    }

    private static void Set(Object target, string field, object value)
    {
        Type type = target.GetType();
        FieldInfo info = null;
        while (type != null && info == null)
        {
            info = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            type = type.BaseType;
        }
        if (info == null) throw new MissingFieldException(target.GetType().Name, field);
        info.SetValue(target, value);
        Record(target);
    }

    private static void Record(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
