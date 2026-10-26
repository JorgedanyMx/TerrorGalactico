using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CodexGraveNotesCheck
{
    const string Key = "CodexGraveNotesCheck";
    static CodexGraveNotesCheck() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Notes/Check recovered drop")]
    static void Run()
    {
        if (EditorApplication.isPlaying) return;
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
        EditorApplication.delayCall += Check;
    }
    static void Check()
    {
        SessionState.SetBool(Key, false);
        try
        {
            var manager = UnityEngine.Object.FindFirstObjectByType<EnemyGraveReturn>();
            if (manager == null) throw new Exception("No EnemyGraveReturn found");
            typeof(EnemyGraveReturn).GetMethod("DropNote", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(manager, new object[] { new Vector3(1000, 0, 1000) });
            var pickup = UnityEngine.Object.FindFirstObjectByType<GraveNotePickup>();
            if (pickup == null) throw new Exception("Drop did not create a pickup");
            var player = GameObject.FindGameObjectWithTag("Player");
            var collider = player.GetComponentInChildren<Collider>();
            if (collider == null) throw new Exception("Player has no collider");
            typeof(GraveNotePickup).GetMethod("TryCollect", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(pickup, new object[] { collider });
            var inspector = UnityEngine.Object.FindFirstObjectByType<ObjectInspector>();
            if (inspector == null || !inspector.ActivableElements.activeSelf ||
                string.IsNullOrEmpty(inspector.textName.text) || string.IsNullOrEmpty(inspector.textCont.text))
                throw new Exception("Inspection did not open with note text");
            File.WriteAllText("Temp/CodexGraveNotesCheck.result", "PASS: drop, pickup and inspection text: " + inspector.textName.text);
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/CodexGraveNotesCheck.result", "FAIL: " + e);
        }
        finally { EditorApplication.isPlaying = false; }
    }
}
