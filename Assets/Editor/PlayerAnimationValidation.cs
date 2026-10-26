using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercises the saved player prefab in an isolated Play scene, then restores the editor.</summary>
[InitializeOnLoad]
public static class PlayerAnimationValidation
{
    private const string Request = "Temp/PlayerAnimationValidation.request";
    private const string Result = "Temp/PlayerAnimationValidation.result";
    private const string SnapshotKey = "PlayerAnimationValidation.Snapshot";
    private const string ErrorsKey = "PlayerAnimationValidation.Errors";
    [Serializable] private sealed class Snapshot { public SceneSetup[] scenes; }
    private static IEnumerator checks;
    private static float originalTimeScale;

    static PlayerAnimationValidation()
    {
        EditorApplication.delayCall += ConsumeRequest;
        EditorApplication.playModeStateChanged += OnPlayMode;
        Application.logMessageReceived += OnLog;
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
        catch (Exception exception) { File.WriteAllText(Result, "FAIL: " + exception); }
    }

    [MenuItem("Tools/Player Animation/Validate Walk and Damage")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Require(!SceneManager.GetSceneAt(i).isDirty, "Save scene changes before validation.");
        Require(EditorSceneManager.playModeStartScene == null, "Clear the custom Play mode start scene before validation.");
        SessionState.SetString(SnapshotKey, JsonUtility.ToJson(new Snapshot { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        SessionState.SetString(ErrorsKey, "");
        File.WriteAllText(Result, "RUNNING: saved prefab movement and damage tests.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        string snapshot = SessionState.GetString(SnapshotKey, "");
        if (string.IsNullOrEmpty(snapshot)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            checks = Run();
            EditorApplication.update += Step;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Step;
            checks = null;
            SessionState.EraseString(SnapshotKey);
            EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Snapshot>(snapshot).scenes);
        }
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (string.IsNullOrEmpty(SessionState.GetString(SnapshotKey, "")) ||
            (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        SessionState.SetString(ErrorsKey, SessionState.GetString(ErrorsKey, "") + message + "\n" + trace + "\n");
    }

    private static void Step()
    {
        try
        {
            Require(string.IsNullOrEmpty(SessionState.GetString(ErrorsKey, "")), SessionState.GetString(ErrorsKey, ""));
            if (checks != null && checks.MoveNext()) return;
        }
        catch (Exception exception) { File.WriteAllText(Result, "FAIL: " + exception); }
        EditorApplication.update -= Step;
        (checks as IDisposable)?.Dispose();
        checks = null;
        Time.timeScale = originalTimeScale;
        EditorApplication.ExitPlaymode();
    }

    private static IEnumerator Run()
    {
        GameObject first = Spawn(new Vector3(0, 1, 0));
        GameObject second = Spawn(new Vector3(0, 1, 10));
        var body = first.GetComponent<Rigidbody>();
        var stats = first.GetComponent<p_PlayerStats>();
        var animator = first.GetComponentInChildren<Animator>();
        var otherAnimator = second.GetComponentInChildren<Animator>();
        Transform visual = animator.transform;
        Transform pose = visual.Find("Pose");
        Transform aim = visual.parent;
        Require(visual.name == "Character_VF", "The animated object is not Character_VF.");
        Require(pose.Find("Body/Head/Head_GEO") != null && pose.Find("LeftFoot/Shoes_GEO_Left") != null &&
            pose.Find("RightFoot/Shoes_GEO_Right") != null && pose.Find("Body/LeftHand/Hands_GEO_Left") != null,
            "Character_VF's articulated parts are missing.");
        Require(visual.GetComponentsInChildren<MeshFilter>().All(m => m.sharedMesh.name != "Capsule"),
            "A capsule is still used as the animated character.");
        Transform leftFoot = pose.Find("LeftFoot");
        Transform rightFoot = pose.Find("RightFoot");
        Vector3 visualPosition = visual.localPosition;
        Quaternion aimRotation = aim.localRotation;
        Vector3 colliderSize = first.GetComponent<BoxCollider>().size;
        double until = EditorApplication.timeSinceStartup + .25;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(State(animator, "Idle"), "The player did not start in Idle.");
        Require(Mathf.Approximately(stats.Health, stats.maxHealth), "Health did not initialize.");

        body.linearVelocity = Vector3.right * 5f;
        until = EditorApplication.timeSinceStartup + .45;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(State(animator, "Walk"), "Planar physics movement did not select Walk.");
        float y = pose.localPosition.y;
        float footZ = leftFoot.localPosition.z;
        bool footMoved = false;
        bool changed = false;
        until = EditorApplication.timeSinceStartup + .25;
        while (EditorApplication.timeSinceStartup < until)
        {
            changed |= Mathf.Abs(pose.localPosition.y - y) > .015f;
            footMoved |= Mathf.Abs(leftFoot.localPosition.z - footZ) > .08f;
            yield return null;
        }
        Require(changed && footMoved, "The Walk keyframes did not animate Character_VF and its feet.");
        Require(body.position.x > 1f, "The physics body did not actually move.");
        Require(Mathf.Abs(body.position.y - 1f) < .001f && Mathf.Abs(body.position.z) < .001f,
            "Visual animation displaced the physics body.");
        Require(visual.localPosition == visualPosition && aim.localRotation == aimRotation && first.GetComponent<BoxCollider>().size == colliderSize,
            "Animation changed the visual mount, aim or collider.");

        int hits = 0;
        stats.Damaged += amount => { Require(amount > 0, "Invalid damage amount."); hits++; };
        stats.TakeDamage(10f);
        until = EditorApplication.timeSinceStartup + .12;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(State(animator, "Hurt"), "Damage did not interrupt Walk.");
        Require(Mathf.Abs(Mathf.DeltaAngle(0, pose.Find("Body").localEulerAngles.x)) > 8f,
            "The damage clip did not produce a visible recoil.");
        Require(State(otherAnimator, "Idle"), "One player's damage affected another player's animation.");
        until = EditorApplication.timeSinceStartup + .55;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(State(animator, "Walk"), "The player did not return to Walk after damage.");

        stats.TakeDamage(5f);
        until = EditorApplication.timeSinceStartup + .17;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        stats.TakeDamage(5f);
        body.linearVelocity = Vector3.zero;
        until = EditorApplication.timeSinceStartup + .10;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(State(animator, "Hurt") && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < .4f,
            "A repeated hit did not restart the damage reaction.");
        until = EditorApplication.timeSinceStartup + .55;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(State(animator, "Idle"), "Stopping during damage did not recover to Idle.");
        Require(pose.localPosition.sqrMagnitude < .000001f && Quaternion.Angle(pose.localRotation, Quaternion.identity) < .01f &&
            Vector3.Distance(pose.localScale, Vector3.one) < .001f, "The visual did not recover its resting pose.");
        stats.TakeDamage(0f);
        stats.Heal(5f);
        until = EditorApplication.timeSinceStartup + .15;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(hits == 3 && State(animator, "Idle"), "Zero damage or healing triggered a damage animation.");

        stats.TakeDamage(5f);
        until = EditorApplication.timeSinceStartup + .12;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(State(animator, "Hurt"), "Damage from Idle did not animate.");
        until = EditorApplication.timeSinceStartup + .55;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(State(animator, "Idle"), "Damage from Idle did not finish.");

        // Saved clips must only bind the visual child, with a seamless walking loop.
        foreach (string name in new[] { "Player_Idle", "Player_Walk", "Player_Hurt" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerAnimationSetup.Folder + "/" + name + ".anim");
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Require(bindings.Length == 63 && bindings.All(binding => binding.path == "Pose" || binding.path.StartsWith("Pose/")), "A clip animates outside the visual pose.");
            Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime == (name != "Player_Hurt"), "Incorrect clip loop setting.");
            if (name == "Player_Walk")
                foreach (var binding in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    Require(Mathf.Approximately(curve.Evaluate(0), curve.Evaluate(clip.length)), "Walking loop has an endpoint discontinuity.");
                }
        }
        RenderPoses(first);
        File.WriteAllText(Result, "PASS: Character_VF (original meshes/materials, separate head/body/hands/feet), no capsule renderer; saved prefab in Play mode; Idle -> Walk from real Rigidbody movement; visible walking keyframes; damage interrupts Idle/Walk; repeated hits restart; recovery to moving/stopped state; healing/zero damage ignored; player-local event; root/aim/colliders unchanged; clip bindings and loop endpoints; preview PNGs rendered.");
    }

    private static GameObject Spawn(Vector3 position)
    {
        var container = new GameObject("Animation validation");
        container.SetActive(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerAnimationSetup.PrefabPath);
        var player = Object.Instantiate(prefab, container.transform);
        player.transform.SetPositionAndRotation(position, Quaternion.identity);
        foreach (var behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null && !(behaviour is p_PlayerStats) && !(behaviour is p_PlayerAnimation)) behaviour.enabled = false;
        foreach (var camera in player.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var listener in player.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        foreach (var light in player.GetComponentsInChildren<Light>(true)) light.enabled = false;
        player.transform.Find("EmptyModel").localRotation = Quaternion.identity;
        var body = player.GetComponent<Rigidbody>();
        body.useGravity = false;
        body.linearDamping = 0;
        container.SetActive(true);
        return player;
    }

    private static bool State(Animator animator, string name) => animator.GetCurrentAnimatorStateInfo(0).IsName(name);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private static void RenderPoses(GameObject player)
    {
        var animator = player.GetComponentInChildren<Animator>();
        animator.enabled = false;
        var visual = animator.gameObject;
        foreach (var renderer in player.GetComponentsInChildren<Renderer>())
            if (!renderer.transform.IsChildOf(visual.transform)) renderer.enabled = false;
        var body = player.GetComponent<Rigidbody>();
        body.linearVelocity = Vector3.zero;
        player.transform.SetPositionAndRotation(new Vector3(0, 1, 0), Quaternion.identity);
        var cameraObject = new GameObject("Animation preview camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(3, 2.8f, 5);
        camera.transform.LookAt(new Vector3(0, 1, 0));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f, .1f, .14f);
        camera.orthographic = true;
        camera.orthographicSize = 1.5f;
        var light = new GameObject("Animation preview light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        var texture = new RenderTexture(640, 640, 24);
        camera.targetTexture = texture;
        RenderTexture previous = RenderTexture.active;
        try
        {
            string[] names = { "Player_Idle", "Player_Walk", "Player_Hurt", "Player_Walk", "Player_Walk" };
            float[] times = { 0f, .16f, .055f, .48f, 0f };
            for (int i = 0; i < names.Length; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerAnimationSetup.Folder + "/" + names[i] + ".anim");
                clip.SampleAnimation(visual, times[i]);
                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                image.Apply();
                string suffix = i < 3 ? "-preview" : "-phase-" + i;
                File.WriteAllBytes("Temp/" + names[i] + suffix + ".png", image.EncodeToPNG());
                Object.Destroy(image);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            texture.Release();
            Object.Destroy(texture);
        }
    }
}
