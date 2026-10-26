using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercises the saved ShigePrueba offering flow with real physics and normal runtime updates.</summary>
[InitializeOnLoad]
public static class ShigePruebaOfferingValidation
{
    private const string ScenePath = "Assets/Scenes/TestingScenes/ShigePrueba.unity";
    private const string Request = "Temp/ShigePruebaOfferingValidation.request";
    private const string Result = "Temp/ShigePruebaOfferingValidation.result";
    private const string ActiveKey = "ShigePruebaOfferingValidation.Active";
    private const string ErrorsKey = "ShigePruebaOfferingValidation.Errors";
    private static IEnumerator checks;

    static ShigePruebaOfferingValidation()
    {
        EditorApplication.delayCall += ConsumeRequest;
        EditorApplication.playModeStateChanged += HandlePlayMode;
        Application.logMessageReceived += RecordError;
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
        catch (Exception exception) { WriteFailure(exception); }
    }

    [MenuItem("Tools/Gameplay/Validate ShigePrueba Offering Flow")]
    public static void Validate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before validating the scene.");
        Require(SceneManager.GetActiveScene().path == ScenePath, "Open ShigePrueba before running this validation.");
        Require(EditorSceneManager.playModeStartScene == null,
            "Clear the custom Play mode start scene before validating ShigePrueba.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Require(!SceneManager.GetSceneAt(i).isDirty, "Save all open scenes before validating the saved setup.");

        SessionState.SetString(ErrorsKey, "");
        SessionState.SetBool(ActiveKey, true);
        File.WriteAllText(Result, "RUNNING: entering Play mode in saved ShigePrueba.");
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks = RunChecks();
            EditorApplication.update += Step;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Step;
            (checks as IDisposable)?.Dispose();
            checks = null;
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseString(ErrorsKey);
            if (File.Exists(Result) && File.ReadAllText(Result).StartsWith("RUNNING:"))
                File.WriteAllText(Result, "FAIL: Play mode ended before the validation completed.");
        }
    }

    private static void RecordError(string message, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false) ||
            (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        SessionState.SetString(ErrorsKey, SessionState.GetString(ErrorsKey, "") +
            type + ": " + message + "\n" + stackTrace + "\n");
    }

    private static void Step()
    {
        try
        {
            string errors = SessionState.GetString(ErrorsKey, "");
            Require(string.IsNullOrEmpty(errors), "Runtime errors occurred:\n" + errors);
            if (checks != null && checks.MoveNext()) return;
        }
        catch (Exception exception) { WriteFailure(exception); }
        EditorApplication.update -= Step;
        (checks as IDisposable)?.Dispose();
        checks = null;
        EditorApplication.ExitPlaymode();
    }

    private static IEnumerator RunChecks()
    {
        Keyboard keyboard = null;
        float originalTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            Require(SceneManager.GetActiveScene().path == ScenePath, "Play mode opened a different scene.");
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Require(player != null, "No active Player is present.");
            PlayerOffering offerings = player.GetComponent<PlayerOffering>();
            Rigidbody body = player.GetComponent<Rigidbody>();
            p_PlayerController controller = player.GetComponent<p_PlayerController>();
            PieceCounter counter = Object.FindFirstObjectByType<PieceCounter>();
            Require(offerings != null && body != null && controller != null && counter != null,
                "PlayerOffering, Rigidbody, player controller or PieceCounter is missing.");
            Require(Object.FindFirstObjectByType<EnemyGraveReturn>() != null, "No EnemyGraveReturn manager is present.");
            GameObject enemyObject = GameObject.Find("OfferingTestEnemy");
            Require(enemyObject != null, "OfferingTestEnemy is missing.");
            Practice_Basic_Enemy enemy = enemyObject.GetComponent<Practice_Basic_Enemy>();
            NavMeshAgent agent = enemyObject.GetComponent<NavMeshAgent>();
            Require(enemy != null && agent != null && agent.isOnNavMesh, "The test enemy is not ready on its NavMesh.");

            GameObject[] pieces = new GameObject[3];
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i] = GameObject.Find("OfferingPiece_" + (i + 1));
                Require(pieces[i] != null && pieces[i].GetComponent<PiecePickup>() != null,
                    "Initial OfferingPiece_" + (i + 1) + " is missing or is not a pickup.");
            }
            Require(offerings.AvailableOfferings == 0 && counter.Count == 0,
                "The scene should start with zero offerings and zero collected pieces.");

            // Only pause unrelated motion in the Play instance. The saved scene remains untouched.
            agent.isStopped = true;
            enemy.enabled = false;
            enemy.StopAllCoroutines();
            foreach (CameraAreaSpawner spawner in Object.FindObjectsByType<CameraAreaSpawner>(FindObjectsSortMode.None))
            {
                spawner.enabled = false;
                spawner.StopAllCoroutines();
            }
            foreach (p_CursorHandler cursor in Object.FindObjectsByType<p_CursorHandler>(FindObjectsSortMode.None))
                cursor.enabled = false;

            Collider playerCollider = null;
            foreach (Collider candidate in player.GetComponentsInChildren<Collider>())
                if (candidate.enabled && !candidate.isTrigger) { playerCollider = candidate; break; }
            Require(playerCollider != null, "Player has no enabled solid collider for collecting pieces.");
            File.WriteAllText(Result, "RUNNING: collecting three saved scene pickups through real physics.");
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                Collider pickupCollider = pieces[i].GetComponentInChildren<Collider>();
                Require(pickupCollider != null, "Pickup has no collider.");
                Vector3 centerOffset = body.position - playerCollider.bounds.center;
                Teleport(body, pickupCollider.bounds.center + centerOffset);
                float earliest = Time.realtimeSinceStartup + 0.2f;
                float timeout = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < earliest ||
                    (offerings.AvailableOfferings * 3 + counter.Count < i + 1 && Time.realtimeSinceStartup < timeout))
                    yield return null;
                Require(offerings.AvailableOfferings * 3 + counter.Count >= i + 1,
                    "Physics did not collect OfferingPiece_" + (i + 1) + ".");
            }
            Require(offerings.AvailableOfferings == 1 && counter.Count == 0,
                "Three collected pieces did not produce exactly one offering and reset PieceCounter.");

            Vector3 south = enemy.transform.position + Vector3.back * 3f;
            Require(NavMesh.SamplePosition(south, out NavMeshHit ground, 1.5f, agent.areaMask),
                "There is no walkable position south of the enemy for the throw.");
            float footOffset = body.position.y - playerCollider.bounds.min.y;
            Teleport(body, ground.position + Vector3.up * (footOffset + 0.05f));
            var serializedOffering = new SerializedObject(offerings);
            SerializedProperty aim = serializedOffering.FindProperty("aimTarget");
            Require(aim.objectReferenceValue != null, "The saved player has no aim target configured.");
            aim.objectReferenceValue = enemy.transform;
            serializedOffering.ApplyModifiedPropertiesWithoutUndo();
            enemy.enabled = true;
            float prepareUntil = Time.realtimeSinceStartup + 0.25f;
            while (Time.realtimeSinceStartup < prepareUntil) yield return null;
            Require(!enemy.IsReturningToGrave, "The enemy started returning before an offering was delivered.");
            float healthBeforeOffering = enemy.Health;

            File.WriteAllText(Result, "RUNNING: throwing through the Offer input and waiting for real enemy collision.");
            string inputRoute = "F keyboard binding";
            InputAction offerAction = InputSystem.actions.FindAction("p_Player/Offer", true);
            bool hasKeyboardBinding = false;
            foreach (InputBinding binding in offerAction.bindings)
                if (binding.effectivePath == "<Keyboard>/f") hasKeyboardBinding = true;
            Require(hasKeyboardBinding, "Offer no longer has the expected F keyboard binding.");
            keyboard = InputSystem.AddDevice<Keyboard>("ShigePruebaValidationKeyboard");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            float inputTimeout = Time.realtimeSinceStartup + 1f;
            while (offerings.AvailableOfferings == 1 && Time.realtimeSinceStartup < inputTimeout)
                yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            if (offerings.AvailableOfferings == 1)
            {
                // Some Editor focus/device-pairing configurations exclude synthetic keyboards.
                inputRoute = "PlayerOffer callback (synthetic keyboard was not routed by the Editor)";
                MethodInfo callback = typeof(p_PlayerController).GetMethod("PlayerOffer",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(callback != null, "PlayerOffer callback is missing.");
                callback.Invoke(controller, new object[] { default(InputAction.CallbackContext) });
            }
            Require(offerings.AvailableOfferings == 0, "PlayerOffer did not launch and consume the offering.");

            float hitTimeout = Time.realtimeSinceStartup + 3f;
            while (!enemy.IsReturningToGrave && Time.realtimeSinceStartup < hitTimeout) yield return null;
            Require(enemy.IsReturningToGrave, "The real offering collision did not start the enemy's grave return.");
            Require(Mathf.Approximately(enemy.Health, healthBeforeOffering), "The offering damaged the enemy.");
            Require(agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete && !agent.isStopped,
                "The offering did not start a complete walking route.");
            Vector3 start = enemy.transform.position;
            Vector3 destination = agent.destination;
            float maxTravel = 0f;
            File.WriteAllText(Result, "RUNNING: observing normal enemy Update walking to " + destination + ".");
            float walkTimeout = Time.realtimeSinceStartup + 35f;
            while (enemy.gameObject.activeInHierarchy && enemy.enabled && Time.realtimeSinceStartup < walkTimeout)
            {
                maxTravel = Mathf.Max(maxTravel, HorizontalDistance(start, enemy.transform.position));
                yield return null;
            }
            Require(maxTravel > 0.5f, "The enemy did not actually walk after receiving the offering.");
            Require(enemy.gameObject.activeInHierarchy && !enemy.enabled && !agent.enabled && enemy.IsReturningToGrave,
                "The enemy did not start disappearing at its grave within 35 seconds. Position=" + enemy.transform.position +
                ", destination=" + destination + ".");
            Require(HorizontalDistance(enemy.transform.position, destination) <= Mathf.Max(0.5f, agent.stoppingDistance + 0.1f),
                "The enemy stopped too far from the selected grave.");
            foreach (Collider collider in enemy.GetComponentsInChildren<Collider>())
                Require(!collider.enabled, "An enemy collider stayed active during disappearance.");
            Vector3 arrivalPosition = enemy.transform.position;
            float lowestY = arrivalPosition.y;
            var returnSettings = new SerializedObject(Object.FindFirstObjectByType<EnemyGraveReturn>());
            float sinkTimeout = Time.realtimeSinceStartup + returnSettings.FindProperty("disappearanceDuration").floatValue + 3f;
            while (enemy.gameObject.activeSelf && Time.realtimeSinceStartup < sinkTimeout)
            {
                lowestY = Mathf.Min(lowestY, enemy.transform.position.y);
                Require(HorizontalDistance(arrivalPosition, enemy.transform.position) < 0.1f,
                    "The enemy moved horizontally during disappearance.");
                yield return null;
            }
            Require(lowestY < arrivalPosition.y - 0.05f, "The enemy did not visibly descend.");
            Require(!enemy.gameObject.activeSelf, "The enemy was not deactivated after sinking.");
            Require(string.IsNullOrEmpty(SessionState.GetString(ErrorsKey, "")), "Runtime errors were recorded.");
            File.WriteAllText(Result, "PASS: saved ShigePrueba scene; 3 real pickup collisions -> 1 offering; " +
                "PieceCounter reset; " + inputRoute + "; stock consumed; real projectile collision; no enemy damage; " +
                "complete NavMesh path; normal Update walking and arrival; sinking with collisions disabled; deactivation; no runtime errors. " +
                "Destination=" + destination + ", travel=" + maxTravel.ToString("F2") + "m.");
            Debug.Log("ShigePrueba offering flow validation passed.");
        }
        finally
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            Time.timeScale = originalTimeScale;
        }
    }

    private static void Teleport(Rigidbody body, Vector3 position)
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = position;
        Physics.SyncTransforms();
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = second.y;
        return Vector3.Distance(first, second);
    }

    private static void WriteFailure(Exception exception)
    {
        File.WriteAllText(Result, "FAIL: " + exception);
        Debug.LogException(exception);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
