using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Articulates the supplied floating-part character without modifying its source FBX.</summary>
public static class CharacterVFAnimationBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Player/Character_VF_Animated.prefab";
    public const float PlayerScale = 2f / 2.3017f;
    private const string ModelPath = "Assets/Models/Character_VF.fbx";
    private const string MeshFolder = "Assets/Animations/Player/CharacterVFMeshes";
    private static readonly Vector3 GroundOffset = new Vector3(0, .1533f, 0);
    private static readonly string[] PartNames = { "Body", "Head", "LeftHand", "RightHand", "LeftFoot", "RightFoot" };

    public static GameObject BuildRig()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (source == null) throw new InvalidOperationException("Character_VF.fbx is missing.");
        if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(PlayerAnimationSetup.Folder, "CharacterVFMeshes");
        var character = new GameObject("Character_VF");
        var pose = Pivot("Pose", character.transform, Vector3.zero);
        var body = Pivot("Body", pose, new Vector3(0, .85f, 0));
        var head = Pivot("Head", body, new Vector3(0, .45f, 0));
        var leftHand = Pivot("LeftHand", body, new Vector3(-.72f, -.084f, .018f));
        var rightHand = Pivot("RightHand", body, new Vector3(.72f, -.084f, .018f));
        var leftFoot = Pivot("LeftFoot", pose, new Vector3(-.2f, .14f, -.091f));
        var rightFoot = Pivot("RightFoot", pose, new Vector3(.2f, .14f, -.091f));
        foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || filter.sharedMesh == null) continue;
            if (filter.name == "Hands_GEO")
            {
                Part(filter, renderer, source.transform, leftHand, -1);
                Part(filter, renderer, source.transform, rightHand, 1);
            }
            else if (filter.name == "Shoes_GEO")
            {
                Part(filter, renderer, source.transform, leftFoot, -1);
                Part(filter, renderer, source.transform, rightFoot, 1);
            }
            else
            {
                bool isHead = filter.name.StartsWith("Head") || filter.name == "Ears_GEO" || filter.name == "Hair_GEO";
                Part(filter, renderer, source.transform, isHead ? head : body, 0);
            }
        }
        return character;
    }

    private static Transform Pivot(string name, Transform parent, Vector3 localPosition)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    private static void Part(MeshFilter source, MeshRenderer original, Transform model, Transform pivot, int side)
    {
        var go = new GameObject(source.name + (side < 0 ? "_Left" : side > 0 ? "_Right" : ""));
        go.transform.SetParent(pivot, false);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = original.sharedMaterials;
        var filter = go.AddComponent<MeshFilter>();
        if (side == 0)
        {
            // These pieces retain the original imported meshes and materials.
            go.transform.position = model.InverseTransformPoint(source.transform.position) + GroundOffset;
            go.transform.rotation = Quaternion.Inverse(model.rotation) * source.transform.rotation;
            go.transform.localScale = source.transform.lossyScale;
            filter.sharedMesh = source.sharedMesh;
            return;
        }

        // Hands/shoes each share a source mesh. Split triangles into disjoint halves,
        // retaining the original vertices, normals, UVs and material assignments.
        var mesh = source.sharedMesh;
        Matrix4x4 toModel = model.worldToLocalMatrix * source.transform.localToWorldMatrix;
        Vector3[] originalVertices = mesh.vertices;
        var triangles = new List<int>[mesh.subMeshCount];
        var selected = new SortedSet<int>();
        for (int s = 0; s < triangles.Length; s++)
        {
            triangles[s] = new List<int>();
            int[] indices = mesh.GetTriangles(s);
            for (int i = 0; i < indices.Length; i += 3)
            {
                float x = 0;
                for (int j = 0; j < 3; j++) x += toModel.MultiplyPoint3x4(originalVertices[indices[i + j]]).x;
                if ((x < 0 ? -1 : 1) != side) continue;
                for (int j = 0; j < 3; j++) { triangles[s].Add(indices[i + j]); selected.Add(indices[i + j]); }
            }
        }
        if (selected.Count == 0) throw new InvalidOperationException("Cannot split " + source.name);
        int[] vertices = selected.ToArray();
        var mapping = vertices.Select((index, i) => new { index, i }).ToDictionary(v => v.index, v => v.i);
        var part = new Mesh { name = go.name };
        part.vertices = vertices.Select(i => toModel.MultiplyPoint3x4(originalVertices[i]) + GroundOffset - pivot.position).ToArray();
        var normals = mesh.normals;
        Matrix4x4 normalMatrix = toModel.inverse.transpose;
        if (normals.Length == originalVertices.Length) part.normals = vertices.Select(i => normalMatrix.MultiplyVector(normals[i]).normalized).ToArray();
        for (int channel = 0; channel < 8; channel++)
        {
            var uvs = new List<Vector4>();
            mesh.GetUVs(channel, uvs);
            if (uvs.Count == originalVertices.Length) part.SetUVs(channel, vertices.Select(i => uvs[i]).ToList());
        }
        var colors = mesh.colors;
        if (colors.Length == originalVertices.Length) part.colors = vertices.Select(i => colors[i]).ToArray();
        part.subMeshCount = triangles.Length;
        for (int s = 0; s < triangles.Length; s++) part.SetTriangles(triangles[s].Select(i => mapping[i]).ToArray(), s);
        part.RecalculateBounds();
        part.RecalculateTangents();
        string path = MeshFolder + "/" + part.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(part, path); filter.sharedMesh = part; }
        else { EditorUtility.CopySerialized(part, existing); Object.DestroyImmediate(part); filter.sharedMesh = existing; }
    }

    public static AnimationClip[] BuildClips(GameObject character)
    {
        var animated = character.GetComponentsInChildren<Transform>().Where(t => t.name == "Pose" || PartNames.Contains(t.name)).ToArray();
        var clips = new AnimationClip[3];
        string[] names = { "Player_Idle", "Player_Walk", "Player_Hurt" };
        for (int mode = 0; mode < clips.Length; mode++)
        {
            string path = PlayerAnimationSetup.Folder + "/" + names[mode] + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null && AnimationUtility.GetCurveBindings(clip).Any(b => b.path == "Pose/LeftFoot"))
            {
                clips[mode] = clip; // Preserve manual edits after the capsule-to-character migration.
                continue;
            }
            if (clip == null) { clip = new AnimationClip { name = names[mode] }; AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves();
            clip.frameRate = 60;
            float[] times = mode == 0 ? new[] { 0f, .2f } : mode == 1 ? new[] { 0f, .08f, .16f, .24f, .32f, .40f, .48f, .56f, .64f } : new[] { 0f, .055f, .12f, .22f, .38f };
            foreach (var joint in animated)
            {
                var positions = new Vector3[times.Length];
                var rotations = new Vector3[times.Length];
                var scales = new Vector3[times.Length];
                for (int i = 0; i < times.Length; i++)
                {
                    positions[i] = joint.localPosition;
                    scales[i] = Vector3.one;
                    if (mode == 1) WalkPose(joint.name, i, ref positions[i], ref rotations[i]);
                    else if (mode == 2) HurtPose(joint.name, i, ref positions[i], ref rotations[i]);
                }
                string binding = AnimationUtility.CalculateTransformPath(joint, character.transform);
                Curves(clip, binding, "m_LocalPosition", times, positions);
                Curves(clip, binding, "localEulerAnglesRaw", times, rotations);
                Curves(clip, binding, "m_LocalScale", times, scales);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = mode != 2;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            clips[mode] = clip;
        }
        return clips;
    }

    private static void WalkPose(string joint, int i, ref Vector3 p, ref Vector3 r)
    {
        float[] sway = { -1f, -.7f, 0f, .7f, 1f, .7f, 0f, -.7f, -1f };
        float[] bounce = { 0f, .018f, .035f, .018f, 0f, .018f, .035f, .018f, 0f };
        float[] stride = { -.22f, -.11f, 0f, .11f, .22f, .11f, 0f, -.11f, -.22f };
        float[] liftLeft = { 0f, 0f, 0f, 0f, 0f, .065f, .11f, .065f, 0f };
        float[] liftRight = { 0f, .065f, .11f, .065f, 0f, 0f, 0f, 0f, 0f };
        if (joint == "Pose") { p.y += bounce[i]; }
        if (joint == "Body") { r = new Vector3(-3, sway[i] * 4, sway[i] * 2); }
        if (joint == "Head") { r = new Vector3(2, -sway[i] * 2, -sway[i] * 1.5f); }
        if (joint == "LeftHand" || joint == "RightHand")
        {
            float sign = joint == "LeftHand" ? 1 : -1;
            p.z -= stride[i] * .7f * sign;
            p.y += .015f + bounce[i] * .5f;
            r.x = sway[i] * 10 * sign;
            r.z = -sign * 5;
        }
        if (joint == "LeftFoot" || joint == "RightFoot")
        {
            bool left = joint == "LeftFoot";
            p.z += stride[i] * (left ? 1 : -1);
            p.y += (left ? liftLeft[i] : liftRight[i]) - bounce[i];
            float[] pitch = { -8f, 0f, 0f, 5f, 12f, 8f, 0f, -12f, -8f };
            r.x = pitch[left ? i : (i + 4) % 8];
        }
    }

    private static void HurtPose(string joint, int i, ref Vector3 p, ref Vector3 r)
    {
        float[] recoil = { 0f, 1f, .65f, -.18f, 0f };
        float a = recoil[i];
        if (joint == "Pose") { p.z += .12f * a; p.y -= .025f * a; }
        if (joint == "Body") { r.x = 18 * a; r.z = -7 * a; p.y -= .045f * a; }
        if (joint == "Head") { r.x = 10 * a; r.z = 6 * a; }
        if (joint == "LeftHand" || joint == "RightHand")
        {
            float sign = joint == "LeftHand" ? -1 : 1;
            p.x += sign * .09f * a;
            p.y += .16f * a;
            p.z += .1f * a;
            r.x = -25 * a;
            r.z = sign * 20 * a;
        }
        if (joint == "LeftFoot" || joint == "RightFoot") { p.y += .025f * a; p.z -= .12f * a; }
    }

    private static void Curves(AnimationClip clip, string path, string property, float[] times, Vector3[] values)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < times.Length; i++) curve.AddKey(new Keyframe(times[i], values[i][axis]));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property + "." + "xyz"[axis]), curve);
        }
    }
}
