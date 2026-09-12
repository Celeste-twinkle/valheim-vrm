using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UniVRM10;
using UnityEngine;
using ValheimVRM;
using VRM;
using Object = UnityEngine.Object;

static class PhysicsWeightProbe
{
    public static void CheckSettings()
    {
        var path = Path.Combine(ValheimVRM.Settings.ValheimVRMDir, "physics_options.json");
        var original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        float old = AvatarPhysics.Weight;
        try
        {
            foreach (float value in new[] {0f, .25f, .5f, 1f})
            {
                AvatarPhysics.Preview(value); AvatarPhysics.Save(); AvatarPhysics.Preview(.73f); AvatarPhysics.Initialize();
                if (AvatarPhysics.Weight != value) throw new Exception("Physics setting did not survive reload");
            }
            foreach (float value in new[] {-1f, 2f, float.NaN, float.PositiveInfinity})
            {
                AvatarPhysics.Preview(value);
                if (float.IsNaN(AvatarPhysics.Weight) || AvatarPhysics.Weight < 0 || AvatarPhysics.Weight > 1)
                    throw new Exception("Invalid physics weight escaped bounds");
            }
        }
        finally
        {
            if (original != null) File.WriteAllBytes(path, original); else if (File.Exists(path)) File.Delete(path);
            AvatarPhysics.Preview(old);
        }
    }
    // Normalize in double precision: Unity's float Angle can report a nonzero
    // angle between identical, slightly non-unit imported quaternions.
    static float Angle(Quaternion a, Quaternion b)
    {
        double dot = (double)a.x*b.x + (double)a.y*b.y + (double)a.z*b.z + (double)a.w*b.w;
        double aa = (double)a.x*a.x + (double)a.y*a.y + (double)a.z*a.z + (double)a.w*a.w;
        double bb = (double)b.x*b.x + (double)b.y*b.y + (double)b.z*b.z + (double)b.w*b.w;
        return (float)(Math.Acos(Math.Min(1, Math.Abs(dot) / Math.Sqrt(aa*bb))) * 360 / Math.PI);
    }
    public static string Run(GameObject imported)
    {
        var weighted = Clone(imported);
        try
        {
            var controller = weighted.AddComponent<AvatarPhysicsWeight>();
            controller.Setup(); controller.enabled = false;
            var bones = Joints(weighted);
            var hierarchy = weighted.GetComponentsInChildren<Transform>(true);
            var rest = bones.Select(b => b.localRotation).ToArray();
            var apply = AccessTools.Method(typeof(AvatarPhysicsWeight), "ApplyWeight");
            var restore = AccessTools.Method(typeof(AvatarPhysicsWeight), "RestoreSimulation");
            float movement = 0, maximumError = 0, maximumSimulationError = 0;
            for (int frame = 0; frame < 240; frame++)
            {
                // Exercise rapid live changes and returning from zero to full weight.
                float weight = new[] {1f, .5f, .25f, 0f, .5f, 1f}[frame / 40];
                restore.Invoke(controller, null);
                float t = frame / 60f;
                var position = new Vector3(Mathf.Sin(t * 9) * .15f, Mathf.Sin(t * 11) * .07f, t * .4f);
                weighted.transform.position = position;
                weighted.transform.rotation = Quaternion.Euler(0, Mathf.Sin(t * 5) * 25, 0);
                Step(weighted);
                var originals = bones.Select(b => b.localRotation).ToArray();
                var before = hierarchy.Select(b => b.localRotation).ToArray();
                apply.Invoke(controller, new object[] { weight });
                for (int i = 0; i < bones.Length; i++)
                {
                    float angle = Angle(rest[i], originals[i]);
                    float error = Mathf.Abs(Angle(rest[i], bones[i].localRotation) - angle * weight);
                    if (float.IsNaN(error) || float.IsNaN(angle)) throw new Exception("Non-finite physics pose");
                    maximumError = Mathf.Max(maximumError, error); movement = Mathf.Max(movement, angle);
                }
                restore.Invoke(controller, null);
                for (int i = 0; i < hierarchy.Length; i++)
                    maximumSimulationError = Mathf.Max(maximumSimulationError, Angle(before[i], hierarchy[i].localRotation));
                if (frame == 239)
                {
                    apply.Invoke(controller, new object[] { 0f });
                    controller.Setup();
                    for (int i = 0; i < bones.Length; i++)
                        if (Angle(bones[i].localRotation, originals[i]) > .15f) throw new Exception("Rebind retained weighted pose");
                }
            }
            if (bones.Length == 0 || movement < .1f || maximumError > .15f || maximumSimulationError > .15f)
                throw new Exception($"Physics weighting failed: joints={bones.Length}, movement={movement}, displayError={maximumError}, simulationError={maximumSimulationError}");
            return $"physics weight: {bones.Length} joints; 240 moving frames; weights 0/25/50/100%; max motion={movement:F3}, display error={maximumError:F5}, simulation feedback error={maximumSimulationError:F5} degrees";
        }
        finally { Object.Destroy(weighted); }
    }

    static GameObject Clone(GameObject imported)
    {
        var clone = Object.Instantiate(imported);
        AccessTools.Method(typeof(ValheimVRM.VRM), "PrepareVrm10Clone").Invoke(null, new object[] { imported, clone });
        clone.SetActive(true);
        foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        var instance = clone.GetComponent<Vrm10Instance>();
        if (instance != null) instance.UpdateType = Vrm10Instance.UpdateTypes.None;
        foreach (var spring in clone.GetComponentsInChildren<VRMSpringBone>(true)) spring.m_updateType = VRMSpringBone.SpringBoneUpdateType.Manual;
        return clone;
    }

    static Transform[] Joints(GameObject root)
    {
        var instance = root.GetComponent<Vrm10Instance>();
        if (instance != null) return instance.SpringBone.Springs.SelectMany(s => s.Joints).Where(j => j != null).Select(j => j.transform).Distinct().ToArray();
        return root.GetComponentsInChildren<VRMSpringBone>(true).SelectMany(s => s.RootBones).Where(b => b != null)
            .SelectMany(b => b.GetComponentsInChildren<Transform>(true)).Where(b => b.childCount > 0).Distinct().ToArray();
    }

    static void Step(GameObject root)
    {
        var instance = root.GetComponent<Vrm10Instance>();
        if (instance != null) instance.Runtime.SpringBone.Process(1f / 60);
        else foreach (var spring in root.GetComponentsInChildren<VRMSpringBone>(true)) spring.ManualUpdate(1f / 60);
    }
}
