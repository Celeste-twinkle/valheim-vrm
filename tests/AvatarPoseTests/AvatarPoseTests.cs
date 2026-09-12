using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UniVRM10;
using UnityEngine;
using ValheimVRM;
using Object = UnityEngine.Object;

[BepInPlugin("valheimvrm.tests.poses", "Avatar pose regression tests", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public sealed class AvatarPoseTests : BaseUnityPlugin
{
    string output;
    readonly List<string> report = new List<string>();
    static readonly System.Reflection.MethodInfo Synchronize = AccessTools.Method(typeof(VRMAnimationSync), "LateUpdate");
    void Awake()
    {
        output = Environment.GetEnvironmentVariable("VRM_POSE_TEST_OUTPUT");
        if (string.IsNullOrEmpty(output)) { enabled = false; return; }
        Directory.CreateDirectory(output);
        var getter = AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupported");
        if (getter != null) new Harmony("valheimvrm.tests.poses.isolation").Patch(getter,
            prefix: new HarmonyMethod(typeof(AvatarPoseTests), nameof(NoCloud)));
    }
    static bool NoCloud(ref bool __result) { __result = false; return false; }

    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            bool more; object value = null;
            try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; }
            catch (Exception ex)
            {
                File.WriteAllLines(Path.Combine(output, "results.txt"), report);
                File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString());
                Logger.LogError(ex); Application.Quit(1); yield break;
            }
            if (!more) { stack.Pop(); continue; }
            if (value is IEnumerator nested) stack.Push(nested); else yield return value;
        }
        File.WriteAllLines(Path.Combine(output, "results.txt"), report);
        Logger.LogInfo("AVATAR_POSE_TESTS_PASSED"); Application.Quit(0);
    }

    IEnumerator Run()
    {
        PhysicsWeightProbe.CheckSettings();
        report.Add("physics settings: atomic save/reload and finite 0..1 bounds passed");
        float deadline = Time.realtimeSinceStartup + 90;
        FejdStartup menu;
        while ((menu = Object.FindFirstObjectByType<FejdStartup>()) == null)
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Menu timed out");
            yield return null;
        }
        var prefab = (GameObject)AccessTools.Field(typeof(FejdStartup), "m_playerPrefab").GetValue(menu);
        var template = prefab.GetComponentInChildren<Animator>(true);
        var models = Environment.GetEnvironmentVariable("VRM_POSE_TEST_MODELS")?.Split('|');
        if (models == null || models.Length == 0) throw new Exception("Set VRM_POSE_TEST_MODELS to fixture paths separated by |");
        foreach (var path in models)
        {
            var holder = new GameObject("AvatarPoseTest"); holder.SetActive(false);
            var sourceObject = Object.Instantiate(template.gameObject, holder.transform);
            foreach (var behaviour in sourceObject.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            holder.SetActive(true);
            var source = sourceObject.GetComponent<Animator>();
            source.cullingMode = AnimatorCullingMode.AlwaysAnimate; source.applyRootMotion = false;
            source.Rebind(); source.Update(0);
            GameObject imported = null;
            yield return ValheimVRM.VRM.ImportVisualAsync(File.ReadAllBytes(path), path, 1, root => imported = root);
            if (imported == null) throw new Exception("Import failed: " + path);
            imported.SetActive(false);
            report.Add(PhysicsWeightProbe.Run(imported));
            var model = Object.Instantiate(imported);
            model.transform.SetParent(holder.transform, false);
            AccessTools.Method(typeof(ValheimVRM.VRM), "PrepareVrm10Clone").Invoke(null, new object[] { imported, model });
            model.SetActive(true);
            foreach (var behaviour in model.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var target = model.GetComponent<Animator>();
            var sync = model.AddComponent<VRMAnimationSync>();
            sync.Setup(source, new ValheimVRM.Settings.VrmSettingsContainer { ModelScale = 1, ModelOffsetY = 0, PlayerHeight = 1.85f });
            sync.enabled = false;
            report.Add("MODEL " + Path.GetFileName(path));
            CheckGroundSitting(holder, model, source, sync);
            EquipmentProbe.Run(source, target, sync, output);
            report.Add("grips: both hands, identity mapping, three scales, four poses, switch/unbind restoration passed");
            CheckSpringClone(imported, model, source, sync);
            Object.Destroy(holder); Object.Destroy(imported);
            yield return null;
        }
    }

    void CheckGroundSitting(GameObject holder, GameObject model, Animator source, VRMAnimationSync sync)
    {
        var target = model.GetComponent<Animator>();
        var feet = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightFoot, HumanBodyBones.RightToes }
            .Select(b => target.GetBoneTransform(b)).Where(t => t != null).ToArray();
        var meshes = new List<Tuple<SkinnedMeshRenderer, int[]>>();
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = renderer.sharedMesh; var bones = renderer.bones;
            if (mesh == null || !mesh.isReadable) continue;
            var weights = mesh.boneWeights;
            var indices = new List<int>();
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                var ids = new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                var values = new[] { w.weight0, w.weight1, w.weight2, w.weight3 };
                if (Enumerable.Range(0, 4).Any(k => values[k] >= .5f && ids[k] < bones.Length && bones[ids[k]] != null &&
                    feet.Any(f => bones[ids[k]] == f || bones[ids[k]].IsChildOf(f)))) indices.Add(i);
            }
            if (indices.Count > 0) meshes.Add(Tuple.Create(renderer, indices.ToArray()));
        }
        if (meshes.Count == 0) throw new Exception("Fixture has no weighted foot vertices");
        float minimum = float.PositiveInfinity;
        var baked = new Mesh();
        foreach (float scale in new[] { .6f, 1f, 1.4f })
        {
            model.transform.localScale = Vector3.one * scale;
            holder.transform.position = new Vector3(3, scale * 2 - 1, -4);
            holder.transform.rotation = Quaternion.Euler(0, scale * 50, 0);
            foreach (int state in new[] { 890925016, -1544306596, -805461806 })
            for (int frame = 0; frame <= 10; frame++)
            {
                source.Rebind(); source.Play(state, 0, frame * .099f); source.Update(0);
                Synchronize.Invoke(sync, null);
                foreach (var item in meshes)
                {
                    item.Item1.BakeMesh(baked); var vertices = baked.vertices;
                    foreach (int index in item.Item2)
                        minimum = Mathf.Min(minimum, item.Item1.transform.TransformPoint(vertices[index]).y - source.transform.position.y);
                }
            }
            source.Rebind(); source.Play(229373857, 0, .75f); source.Update(0);
            Synchronize.Invoke(sync, null);
            float residual = Vector3.Distance(source.GetBoneTransform(HumanBodyBones.Hips).position, target.GetBoneTransform(HumanBodyBones.Hips).position);
            if (residual > .0001f) throw new Exception("Seated lift leaked into standing: " + residual);
        }
        Object.Destroy(baked);
        report.Add("ground sit: 99 samples, three scales/translated roots, lowest foot=" + minimum.ToString("F6") + " m; standing reset passed");
        if (minimum < -.02f) throw new Exception("Feet penetrated the ground: " + minimum);
        model.transform.localScale = Vector3.one; holder.transform.position = Vector3.zero; holder.transform.rotation = Quaternion.identity;
    }

    void CheckSpringClone(GameObject imported, GameObject model, Animator source, VRMAnimationSync sync)
    {
        var original = imported.GetComponent<Vrm10Instance>();
        var clone = model.GetComponent<Vrm10Instance>();
        if (original == null) { report.Add("legacy VRM: no VRM 1.0 spring runtime"); return; }
        if (original.SpringBone.Springs.Count != clone.SpringBone.Springs.Count || original.SpringBone.ColliderGroups.Count != clone.SpringBone.ColliderGroups.Count)
            throw new Exception("Spring lists were lost during cloning");
        for (int i = 0; i < original.SpringBone.Springs.Count; i++)
        {
            var a = original.SpringBone.Springs[i]; var b = clone.SpringBone.Springs[i];
            if (a.Joints.Count != b.Joints.Count || a.ColliderGroups.Count != b.ColliderGroups.Count || (a.Center != null && b.Center == null))
                throw new Exception("Spring contents were lost");
            foreach (var joint in b.Joints) if (!joint.transform.IsChildOf(model.transform)) throw new Exception("Spring still references source avatar");
            foreach (var group in b.ColliderGroups)
                foreach (var collider in group.Colliders) if (!collider.transform.IsChildOf(model.transform)) throw new Exception("Collider still references source avatar");
        }
        var joints = model.GetComponentsInChildren<VRM10SpringBoneJoint>();
        var initial = joints.Select(j => j.transform.localRotation).ToArray();
        float movement = 0;
        for (int frame = 0; frame < 120; frame++)
        {
            float t = frame / 60f;
            model.transform.parent.position = new Vector3(Mathf.Sin(t * 9) * .15f, Mathf.Sin(t * 11) * .07f, t * .4f);
            source.Rebind(); source.Play(229373857, 0, t); source.Update(0);
            Synchronize.Invoke(sync, null);
            clone.Runtime.SpringBone.Process(1f / 60);
            for (int i = 0; i < joints.Length; i++)
            {
                float angle = Quaternion.Angle(initial[i], joints[i].transform.localRotation);
                if (float.IsNaN(angle)) throw new Exception("Spring produced a non-finite pose");
                movement = Mathf.Max(movement, angle);
            }
        }
        if (original.SpringBone.Springs.Count > 0 && movement < .1f) throw new Exception("Spring simulation remained static");
        report.Add("spring clone: " + clone.SpringBone.Springs.Count + " chains, all references local, finite motion max=" + movement.ToString("F3") + " degrees");
        model.transform.parent.position = Vector3.zero;
    }
}
