using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Checks the leaf prefabs without changing the user's scene or entering Play mode.</summary>
[InitializeOnLoad]
public static class GateLeafValidation
{
    private const string Request = "Temp/GateLeafValidation.request";

    static GateLeafValidation() => EditorApplication.delayCall += ConsumeRequest;

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
            File.WriteAllText("Temp/GateLeafValidation.result", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Panteon/Validate Gate Leaf Opening")]
    public static void Validate()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (string side in new[] { "Izquierda", "Derecha" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/pf_Hoja" + side + ".prefab");
                var leaf = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var controller = leaf.GetComponent<GateLeaf>();
                Require(controller != null, "Missing controller: " + side);
                var trigger = leaf.GetComponent<BoxCollider>();
                Require(trigger.isTrigger && leaf.GetComponent<Rigidbody>().isKinematic, "Invalid trigger physics");
                var hinge = leaf.transform.Find("Bisagra");
                Require(hinge != null && hinge.childCount == 14, "Missing hinge geometry");
                int solidColliders = 0;
                foreach (var collider in hinge.GetComponentsInChildren<Collider>())
                    if (!collider.isTrigger) solidColliders++;
                Require(solidColliders == 14, "Leaf collision changed");
                controller.SendMessage("Awake");
                var actor = new GameObject("Gate validation actor");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, preview);
                var colliderObject = new GameObject("Player child collider");
                colliderObject.transform.SetParent(actor.transform);
                var actorCollider = colliderObject.AddComponent<CapsuleCollider>();
                controller.SendMessage("OnTriggerEnter", actorCollider);
                controller.SendMessage("FixedUpdate");
                Require(hinge.localRotation == Quaternion.identity, "Non-player opened gate");
                actor.tag = "Player";
                controller.SendMessage("OnTriggerEnter", actorCollider);
                for (int frame = 0; frame < 10; frame++) controller.SendMessage("FixedUpdate");
                Require(Quaternion.Angle(hinge.localRotation, Quaternion.identity) > 0f && !controller.IsOpen, "Opening is not animated");
                for (int frame = 0; frame < 200; frame++) controller.SendMessage("FixedUpdate");
                Require(controller.IsOpen, "Gate did not finish opening");
                Require(Mathf.Abs(Quaternion.Angle(hinge.localRotation, Quaternion.identity) - 90f) < 0.01f, "Incorrect opening angle");
                // Both inner edges must swing toward prefab-local +Z.
                var innerEdge = hinge.TransformPoint(new Vector3(side == "Izquierda" ? 3f : -3f, 0f, 0f));
                Require(leaf.transform.InverseTransformPoint(innerEdge).z > 2.99f, "Wrong opening direction");
                Require(leaf.transform.localRotation == Quaternion.identity && trigger.center.z == 0f, "Trigger moved with leaf");
                var rotation = hinge.localRotation;
                controller.SendMessage("OnTriggerEnter", actorCollider);
                controller.SendMessage("FixedUpdate");
                Require(hinge.localRotation == rotation, "Repeated entry restarted opening");
            }
            File.WriteAllText("Temp/GateLeafValidation.result", "PASS: both prefabs; 14 solid colliders each; stationary triggers and kinematic bodies; player filtering including nested colliders; smooth 90-degree opening around outer hinges; matching inward direction; repeated entry remains open.");
            Debug.Log("Gate leaf validation passed.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
