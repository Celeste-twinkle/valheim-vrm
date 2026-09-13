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
        GroundingProbe.CheckSettings();
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
            if (Environment.GetEnvironmentVariable("VRM_GROUNDING_DIAGNOSTIC") != "1") report.Add(PhysicsWeightProbe.Run(imported));
            var model = Object.Instantiate(imported);
            model.transform.SetParent(holder.transform, false);
            AccessTools.Method(typeof(ValheimVRM.VRM), "PrepareVrm10Clone").Invoke(null, new object[] { imported, model });
            model.SetActive(true);
            foreach (var behaviour in model.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var target = model.GetComponent<Animator>();
            if (Environment.GetEnvironmentVariable("VRM_ATTACH_SITTING") == "1")
            { source.Play(-1544306596, 0, .5f); source.Update(0); }
            var sync = model.AddComponent<VRMAnimationSync>();
            sync.Setup(source, new ValheimVRM.Settings.VrmSettingsContainer { ModelScale = 1, ModelOffsetY = 0, PlayerHeight = 1.85f });
            sync.enabled = false;
            report.Add("MODEL " + Path.GetFileName(path));
            if (Environment.GetEnvironmentVariable("VRM_LIVE_GROUNDING") == "1")
            {
                yield return LivePoseProbe.Run(model, source, sync, report, output);
                Object.Destroy(holder); Object.Destroy(imported);
                yield return null;
                continue;
            }
            if (Environment.GetEnvironmentVariable("VRM_GROUNDING_DIAGNOSTIC") == "1")
            {
                GroundingProbe.Run(holder, model, source, sync, report);
                Object.Destroy(holder); Object.Destroy(imported);
                yield return null;
                continue;
            }
            GroundingProbe.Run(holder, model, source, sync, report);
            EquipmentProbe.Run(source, target, sync, output);
            report.Add("equipment: both hands and seven body sockets, three scales/four poses, native skeleton protection and unbind restoration passed");
            CheckSpringClone(imported, model, source, sync);
            Object.Destroy(holder); Object.Destroy(imported);
            yield return null;
        }
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
