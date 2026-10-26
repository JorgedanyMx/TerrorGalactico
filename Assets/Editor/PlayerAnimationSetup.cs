using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates editable clips and installs them on the player prefab and open scene.</summary>
[InitializeOnLoad]
public static class PlayerAnimationSetup
{
    public const string Folder = "Assets/Animations/Player";
    public const string PrefabPath = "Assets/Prefabs/Player/temp_player.prefab";
    public const string ControllerPath = Folder + "/Player.controller";
    private const string Request = "Temp/PlayerAnimationSetup.request";
    private const string Result = "Temp/PlayerAnimationSetup.result";

    static PlayerAnimationSetup() { EditorApplication.delayCall += ConsumeRequest; }

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
            File.WriteAllText(Result, "FAIL: " + exception);
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Player Animation/Set Up Walk and Damage")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before configuring player animations.");
        var scene = SceneManager.GetActiveScene();
        if (scene.isDirty)
            throw new InvalidOperationException("Save the open scene before configuring player animations.");
        Directory.CreateDirectory("Temp/PlayerAnimationBackup");
        Backup(PrefabPath);
        if (!string.IsNullOrEmpty(scene.path)) Backup(scene.path);
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations", "Player");

        GameObject character = CharacterVFAnimationBuilder.BuildRig();
        try
        {
            var clips = CharacterVFAnimationBuilder.BuildClips(character);
            AnimatorController controller = GetOrCreateController(clips[0], clips[1], clips[2]);
            character.AddComponent<Animator>().runtimeAnimatorController = controller;
            if (PrefabUtility.SaveAsPrefabAsset(character, CharacterVFAnimationBuilder.PrefabPath) == null)
                throw new InvalidOperationException("Could not save Character_VF animated prefab.");
        }
        finally { Object.DestroyImmediate(character); }
        var playerController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Install(prefab, playerController);
            if (PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath) == null)
                throw new InvalidOperationException("Could not save the player prefab.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        int count = 0;
        foreach (var player in Object.FindObjectsByType<p_PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (player.gameObject.scene != scene) continue;
            Install(player.gameObject, playerController);
            count++;
        }
        AssetDatabase.SaveAssets();
        if (count > 0 && !string.IsNullOrEmpty(scene.path))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the player scene.");
        }
        File.WriteAllText(Result, "PASS: Character_VF with independent hands/feet and head/body keyframes; Walk/Hurt/Idle; player prefab; " + count + " scene player(s) configured in " + scene.path);
        Debug.Log("Character_VF walk and damage animations configured.");
    }

    private static void Backup(string path)
    {
        string destination = "Temp/PlayerAnimationBackup/" + Path.GetFileName(path);
        if (!File.Exists(destination)) File.Copy(path, destination);
    }

    [MenuItem("Tools/Player Animation/Test Damage in Play Mode")]
    private static void TestDamage()
    {
        if (!EditorApplication.isPlaying) return;
        var stats = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<p_PlayerStats>() : null;
        if (stats == null) stats = Object.FindFirstObjectByType<p_PlayerStats>();
        if (stats != null) stats.TakeDamage(10f);
    }

    [MenuItem("Tools/Player Animation/Test Damage in Play Mode", true)]
    private static bool CanTestDamage() => EditorApplication.isPlaying;

    private static void Install(GameObject player, RuntimeAnimatorController controller)
    {
        Transform aim = player.transform.Find("EmptyModel");
        if (aim == null) throw new InvalidOperationException(player.name + " has no EmptyModel aim transform.");
        Transform root = aim.Find("Character_VF");
        Transform oldCapsule = aim.Find("PlayerVisual");
        if (oldCapsule != null) Object.DestroyImmediate(oldCapsule.gameObject);
        if (root == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterVFAnimationBuilder.PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, aim);
            instance.name = "Character_VF";
            root = instance.transform;
            root.localPosition = Vector3.down;
            // The supplied model faces -Z; gameplay aiming faces +Z.
            root.localRotation = Quaternion.Euler(0, 180, 0);
            root.localScale = Vector3.one * CharacterVFAnimationBuilder.PlayerScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        }
        var animator = root.GetComponent<Animator>();
        if (animator == null) animator = root.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var driver = player.GetComponent<p_PlayerAnimation>();
        if (driver == null) driver = player.AddComponent<p_PlayerAnimation>();
        var serialized = new SerializedObject(driver);
        serialized.FindProperty("visualAnimator").objectReferenceValue = animator;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // Preserve the scene's old fallback, but avoid drawing a second capsule over the animation.
        var placeholder = player.transform.Find("PlayerPlaceholder");
        if (placeholder != null) placeholder.gameObject.SetActive(false);
        if (PrefabUtility.IsPartOfPrefabInstance(player))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }
    }

    private static AnimatorController GetOrCreateController(AnimationClip idle, AnimationClip walk, AnimationClip hurt)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null) return controller;
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter(new AnimatorControllerParameter { name = "WalkSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        controller.AddParameter("Damage", AnimatorControllerParameterType.Trigger);
        var machine = controller.layers[0].stateMachine;
        var idleState = machine.AddState("Idle", new Vector3(250, 60));
        idleState.motion = idle;
        var walkState = machine.AddState("Walk", new Vector3(500, 60));
        walkState.motion = walk;
        walkState.speedParameter = "WalkSpeed";
        walkState.speedParameterActive = true;
        var hurtState = machine.AddState("Hurt", new Vector3(375, 220));
        hurtState.motion = hurt;
        machine.defaultState = idleState;
        Transition(idleState.AddTransition(walkState), false, .12f).AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        Transition(walkState.AddTransition(idleState), false, .12f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        var damage = Transition(machine.AddAnyStateTransition(hurtState), false, .035f);
        damage.canTransitionToSelf = true;
        damage.AddCondition(AnimatorConditionMode.If, 0f, "Damage");
        Transition(hurtState.AddTransition(walkState), true, .08f).AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        Transition(hurtState.AddTransition(idleState), true, .08f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorStateTransition Transition(AnimatorStateTransition transition, bool exit, float duration)
    {
        transition.hasExitTime = exit;
        transition.exitTime = 1f;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.interruptionSource = TransitionInterruptionSource.None;
        return transition;
    }
}
