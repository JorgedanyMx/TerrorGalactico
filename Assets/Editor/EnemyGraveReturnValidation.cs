using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Runs isolated Play mode checks, then restores the previously open, unmodified scenes.</summary>
[InitializeOnLoad]
public static class EnemyGraveReturnValidation
{
    private const string Request = "Temp/EnemyGraveReturnValidation.request";
    private const string Result = "Temp/EnemyGraveReturnValidation.result";

    private const string SceneSetupKey = "EnemyGraveReturnValidation.SceneSetup";
    private static IEnumerator checks;

    [Serializable]
    private sealed class SceneSnapshot { public SceneSetup[] scenes; }

    static EnemyGraveReturnValidation()
    {
        EditorApplication.delayCall += ConsumeRequest;
        EditorApplication.playModeStateChanged += HandlePlayMode;
    }

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
        try { Validate(); }
        catch (Exception exception)
        {
            File.WriteAllText(Result, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Gameplay/Validate Enemy Grave Return")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene changes before running the isolated Play mode validation.");
        if (EditorSceneManager.playModeStartScene != null)
            throw new InvalidOperationException("Clear the custom Play mode start scene before running this validation.");

        SessionState.SetString(SceneSetupKey, JsonUtility.ToJson(new SceneSnapshot
            { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        string snapshot = SessionState.GetString(SceneSetupKey, "");
        if (string.IsNullOrEmpty(snapshot)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks = RunChecks();
            EditorApplication.update += Step;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Step;
            checks = null;
            SessionState.EraseString(SceneSetupKey);
            EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SceneSnapshot>(snapshot).scenes);
        }
    }

    private static void Step()
    {
        try { if (checks.MoveNext()) return; }
        catch (Exception exception)
        {
            File.WriteAllText(Result, exception.ToString());
            Debug.LogException(exception);
        }
        EditorApplication.update -= Step;
        (checks as IDisposable)?.Dispose();
        checks = null;
        EditorApplication.ExitPlaymode();
    }

    private static IEnumerator RunChecks()
    {
        Scene scene = SceneManager.GetActiveScene();
        NavMeshData data = null;
        NavMeshDataInstance navMesh = default;
        GameEvent offeringEvent = ScriptableObject.CreateInstance<GameEvent>();
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        try
        {
            // Keep the test NavMesh far from any open scene's navigation data.
            Vector3 origin = new Vector3(10000f, 0f, 10000f);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>();
            foreach (float offset in new[] { 0f, 40f })
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(origin + new Vector3(offset, -0.1f, 0f), Quaternion.identity, Vector3.one),
                    size = new Vector3(20f, 0.2f, 20f),
                    area = 0
                });
            data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(origin + Vector3.right * 20f, new Vector3(70f, 10f, 30f)),
                Vector3.zero, Quaternion.identity);
            Require(data != null, "Could not build the test NavMesh.");
            navMesh = NavMesh.AddNavMeshData(data);
            Require(NavMesh.SamplePosition(origin, out _, 2f, NavMesh.AllAreas), "Test NavMesh has no ground.");

            GameObject actor = Create(scene, "Offering target", origin);
            actor.tag = "Ghost";
            actor.SetActive(false);
            NavMeshAgent agent = actor.AddComponent<NavMeshAgent>();
            agent.agentTypeID = settings.agentTypeID;
            Enemy_FSM enemy = actor.AddComponent<Enemy_FSM>();
            SetField(enemy, "m_agent", agent);
            GameObject attack = Create(scene, "Attack hitbox", origin);
            attack.transform.SetParent(actor.transform);
            SetField(enemy, "m_attackTrigger", attack);
            actor.SetActive(true);
            Require(agent.Warp(origin) && agent.isOnNavMesh, "Test agent is not on the NavMesh.");
            SetField(enemy, "m_enemyState", Enemy_FSM.EnemyState.ATTACK);
            attack.SetActive(true);

            GameObject colliderChild = Create(scene, "Enemy child collider", origin);
            colliderChild.transform.SetParent(actor.transform);
            Transform reachable = Create(scene, "Reachable grave", origin + Vector3.right * 5f).transform;
            Transform unreachable = Create(scene, "Disconnected grave", origin + Vector3.right * 40f).transform;
            Transform inactive = Create(scene, "Inactive grave", origin + Vector3.forward * 5f).transform;
            inactive.gameObject.SetActive(false);
            GameObject manager = Create(scene, "Return manager", origin);
            manager.SetActive(false);
            EnemyGraveReturn controller = manager.AddComponent<EnemyGraveReturn>();
            SetField(controller, "gravePoints", new[] { null, inactive, unreachable, reachable, reachable });

            GameEventListener listener = manager.AddComponent<GameEventListener>();
            listener.Event = offeringEvent;
            listener.Response = new UnityEvent();
            int legacyCalls = 0;
            listener.Response.AddListener(() => legacyCalls++);
            listener.ResponseWithTarget.AddListener(controller.SendEnemyToRandomGrave);
            manager.SetActive(true);
            offeringEvent.RegisterListener(listener);

            offeringEvent.Raise();
            Require(legacyCalls == 1 && !enemy.IsReturningToGrave, "Legacy Raise changed behavior.");
            offeringEvent.Raise((GameObject)null);
            offeringEvent.Raise(manager);
            Require(!enemy.IsReturningToGrave, "Invalid target changed enemy state.");

            offeringEvent.Raise(colliderChild);
            Require(enemy.IsReturningToGrave, "Targeted event did not start the return.");
            Require(!attack.activeSelf, "Attack hitbox stayed active.");
            Require(agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete,
                "Return did not use a complete path.");
            Require(Vector3.Distance(agent.destination, reachable.position) < 0.2f,
                "Selected an inactive, duplicate-weighted, or unreachable destination.");
            Require(legacyCalls == 1, "Targeted event invoked a parameterless response.");
            Vector3 destination = agent.destination;
            offeringEvent.Raise(actor);
            Require(Vector3.Distance(agent.destination, destination) < 0.01f, "Repeated offering changed the route.");

            // This isolated check covers the FSM's arrival/rest contract. The scene validator
            // covers the manager's subsequent sinking animation and deactivation.
            controller.enabled = false;

            float timeout = Time.realtimeSinceStartup + 8f;
            while (!agent.isStopped && Time.realtimeSinceStartup < timeout)
            {
                Tick(enemy);
                yield return null;
            }
            Require(Vector3.Distance(actor.transform.position, reachable.position) < 0.3f,
                "Enemy did not actually walk to the grave.");
            Require(agent.isStopped && !agent.hasPath && enemy.IsReturningToGrave, $"Enemy did not rest at its grave: stopped={agent.isStopped}, hasPath={agent.hasPath}, returning={enemy.IsReturningToGrave}, remaining={agent.remainingDistance}.");
            Tick(enemy);
            Require(enemy.IsReturningToGrave && agent.isStopped, "Enemy resumed chasing after arrival.");
            enemy.ResumePatrol();
            Require(!enemy.IsReturningToGrave && !agent.isStopped, "ResumePatrol did not release the enemy.");
            controller.enabled = true;

            Require(agent.Warp(origin), "Could not reset the test agent.");
            offeringEvent.Raise(actor);
            agent.ResetPath();
            Tick(enemy);
            Require(!enemy.IsReturningToGrave, "A lost route left the enemy stuck returning.");

            SetField(controller, "gravePoints", new[] { null, inactive, unreachable });
            offeringEvent.Raise(actor);
            Require(!enemy.IsReturningToGrave, "Unreachable graves changed the AI state.");
            SetField(controller, "gravePoints", new[] { reachable });
            agent.enabled = false;
            offeringEvent.Raise(actor);
            Require(!enemy.IsReturningToGrave, "Disabled agent accepted an offering.");
            agent.enabled = true;

            // One thrown offering can hit both an enemy root and its child collider in one frame.
            GameObject projectile = Create(scene, "Offering projectile", origin);
            GhostHitTrigger trigger = projectile.AddComponent<GhostHitTrigger>();
            var hitEvent = new UnityEvent<GameObject>();
            int hits = 0;
            hitEvent.AddListener(target => { Require(target == actor, "Hit event lost the enemy root."); hits++; });
            SetField(trigger, "onGhostHit", hitEvent);
            projectile.SendMessage("TryHit", colliderChild);
            Require(hits == 1, "Nested collider did not deliver an offering.");
            projectile.SendMessage("TryHit", actor);
            Require(hits == 1, "Consumed offering fired again.");

            offeringEvent.UnregisterListener(listener);
            offeringEvent.Raise();
            Require(legacyCalls == 1, "Unregistered listener still received events.");
            File.WriteAllText(Result, "PASS: legacy and targeted events; invalid targets; child colliders; " +
                "complete NavMesh route; inactive/disconnected graves; attack hitbox disabled; repeated offerings; " +
                "actual walking, arrival and continued rest; ResumePatrol; lost route recovery; " +
                "disabled agent; consumed hit guard; listener removal.");
            Debug.Log("Enemy grave return validation passed.");
        }
        finally
        {
            navMesh.Remove();
            if (data != null) Object.DestroyImmediate(data);
            Object.DestroyImmediate(offeringEvent);
            UnityEngine.Random.state = randomState;
        }
    }

    private static GameObject Create(Scene scene, string name, Vector3 position)
    {
        var instance = new GameObject(name);
        SceneManager.MoveGameObjectToScene(instance, scene);
        instance.transform.position = position;
        return instance;
    }

    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);

    private static void Tick(Enemy_FSM enemy) =>
        typeof(Enemy_FSM).GetMethod("EnemyFSM_Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(enemy, null);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
