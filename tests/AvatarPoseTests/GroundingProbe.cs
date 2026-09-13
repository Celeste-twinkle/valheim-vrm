using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;

static class GroundingProbe
{
    static readonly System.Reflection.MethodInfo Synchronize = AccessTools.Method(typeof(VRMAnimationSync), "LateUpdate");
    sealed class Surface : IDisposable
    {
        sealed class Entry { public SkinnedMeshRenderer Skin; public bool[] Feet, Seat, Lower; }
        readonly List<Entry> entries = new List<Entry>();
        readonly Mesh baked = new Mesh();
        public Surface(Animator animator)
        {
            var map = new Dictionary<Transform, HumanBodyBones>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            { var bone = animator.GetBoneTransform((HumanBodyBones)i); if (bone != null) map[bone] = (HumanBodyBones)i; }
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.enabled || skin.sharedMesh == null || !skin.sharedMesh.isReadable) continue;
                var weights = skin.sharedMesh.boneWeights; var bones = skin.bones;
                if (weights.Length != skin.sharedMesh.vertexCount) continue;
                var foot = new bool[weights.Length]; var seat = new bool[weights.Length]; var lower = new bool[weights.Length];
                for (int i = 0; i < weights.Length; i++)
                {
                    var w = weights[i];
                    int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                    float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
                    float f = 0, s = 0, l = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        if (values[k] <= 0 || ids[k] >= bones.Length) continue;
                        var bone = bones[ids[k]];
                        while (bone != null)
                        {
                            if (map.TryGetValue(bone, out var human))
                            {
                                if (human == HumanBodyBones.LeftFoot || human == HumanBodyBones.RightFoot || human == HumanBodyBones.LeftToes || human == HumanBodyBones.RightToes) f += values[k];
                                if (human == HumanBodyBones.Hips || human == HumanBodyBones.LeftUpperLeg || human == HumanBodyBones.RightUpperLeg) s += values[k];
                                if (human == HumanBodyBones.LeftLowerLeg || human == HumanBodyBones.RightLowerLeg) l += values[k];
                                break;
                            }
                            bone = bone.parent;
                        }
                    }
                    foot[i] = f >= .5f; seat[i] = s >= .5f; lower[i] = f + s + l >= .5f;
                }
                entries.Add(new Entry { Skin = skin, Feet = foot, Seat = seat, Lower = lower });
            }
        }
        // Independent engine skinning, including authored blend shapes. Production
        // samples cached weighted contacts and never bakes a mesh during animation.
        public void Sample(out float foot, out float seat, out float all)
        {
            foot = seat = all = float.PositiveInfinity;
            foreach (var e in entries)
            {
                e.Skin.BakeMesh(baked, true);
                var vertices = baked.vertices; var matrix = e.Skin.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float y = matrix.MultiplyPoint3x4(vertices[i]).y;
                    if (e.Lower[i]) all = Mathf.Min(all, y);
                    if (e.Feet[i]) foot = Mathf.Min(foot, y);
                    if (e.Seat[i]) seat = Mathf.Min(seat, y);
                }
            }
        }
        public void Dispose() { UnityEngine.Object.Destroy(baked); }
    }

    public static void Run(GameObject holder, GameObject model, Animator source, VRMAnimationSync sync, List<string> report)
    {
        var target = model.GetComponent<Animator>();
        var originalScale = model.transform.localScale;
        var settings = (ValheimVRM.Settings.VrmSettingsContainer)AccessTools.Field(typeof(VRMAnimationSync), "settings").GetValue(sync);
        int samples = 0; float maximumError = 0;
        using (var original = new Surface(source)) using (var rendered = new Surface(target))
        {
            foreach (float ratio in new[] { .7f, 1f, 1.4f })
                foreach (float worldHeight in new[] { 0f, 1200f })
                {
                    model.transform.localScale = originalScale * ratio;
                    holder.transform.position = new Vector3(3, worldHeight, -4);
                    holder.transform.rotation = Quaternion.Euler(0, ratio * 60, 0);
                    foreach (int state in new[] { 229373857, 890925016, -1544306596, -805461806, -1829310159 })
                        for (int frame = 0; frame < 11; frame++)
                        {
                            source.Rebind(); source.Play(state, 0, frame * .09f); source.Update(0);
                            original.Sample(out float sourceFoot, out float sourceSeat, out _);
                            Synchronize.Invoke(sync, null);
                            rendered.Sample(out float foot, out float seat, out float bottom);
                            float expected, actual;
                            if (state == 229373857) { expected = Mathf.Max(sourceFoot, source.transform.position.y); actual = foot; }
                            else if (state == -1829310159) { expected = sourceSeat; actual = seat; }
                            else { expected = source.transform.position.y; actual = bottom; }
                            float error = Mathf.Abs(actual - expected);
                            maximumError = Mathf.Max(maximumError, error); samples++;
                            if (error > .008f) throw new Exception($"Contact mismatch: ratio={ratio}, y={worldHeight}, state={state}, frame={frame}, actual={actual}, expected={expected}, error={error}");
                        }
                }
            model.transform.localScale = originalScale;
            holder.transform.position = Vector3.zero; holder.transform.rotation = Quaternion.identity;
            var baselines = new Dictionary<int, float>();
            foreach (int state in new[] { 229373857, -1544306596, -1829310159 })
            {
                source.Rebind(); source.Play(state, 0, .5f); source.Update(0); Synchronize.Invoke(sync, null);
                baselines[state] = target.GetBoneTransform(HumanBodyBones.Hips).position.y;
            }
            settings.StandingHeightOffset = .17f; settings.SittingHeightOffset = -.08f;
            foreach (int state in new[] { 229373857, -1544306596, -1829310159 })
            {
                source.Rebind(); source.Play(state, 0, .5f); source.Update(0); Synchronize.Invoke(sync, null);
                float delta = target.GetBoneTransform(HumanBodyBones.Hips).position.y - baselines[state];
                float expected = state == 229373857 ? .17f : -.08f;
                if (Mathf.Abs(delta - expected) > .001f) throw new Exception("Standing/sitting offsets were coupled: " + delta);
            }
            settings.StandingHeightOffset = settings.SittingHeightOffset = 0;
            source.Rebind(); source.Play(229373857, 0, .5f); source.Update(0);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 120; i++) { source.Update(0); Synchronize.Invoke(sync, null); }
            timer.Stop();
            rendered.Sample(out float finalFoot, out _, out _);
            if (Mathf.Abs(finalFoot) > .02f) throw new Exception("Standing grounding accumulated: " + finalFoot);
            report.Add($"contact: {samples} posed samples; 3 scales, 2 translated roots; stand/ground sit/transitions/chair; maximum surface error={maximumError:F6} m; 120 repeated frames={timer.Elapsed.TotalMilliseconds:F1} ms; independent +17/-8 cm offsets passed");
        }
    }

    public static void CheckSettings()
    {
        string name = "__grounding_test_" + Guid.NewGuid().ToString("N");
        string path = ValheimVRM.Settings.PlayerSettingsPath(name, false);
        try
        {
            Directory.CreateDirectory(ValheimVRM.Settings.ConfigDir);
            File.WriteAllText(path, "// preserved comment\nModelScale=1.25\nStandingHeightOffset=0\n");
            ValheimVRM.Settings.AddSettingsFromFile(name, false);
            var settings = ValheimVRM.Settings.GetSettings(name);
            settings.StandingHeightOffset = .17f; settings.SittingHeightOffset = -.08f;
            AvatarHeightOffsets.Save(name);
            ValheimVRM.Settings.AddSettingsFromFile(name, false);
            if (settings.StandingHeightOffset != .17f || settings.SittingHeightOffset != -.08f || settings.ModelScale != 1.25f || !File.ReadAllText(path).Contains("// preserved comment"))
                throw new Exception("Height offset persistence changed other model settings");
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, -2f, 2f })
            { float result = AvatarHeightOffsets.Clamp(value); if (float.IsNaN(result) || result < -.5f || result > .5f) throw new Exception("Invalid height offset"); }
        }
        finally { ValheimVRM.Settings.RemoveSettings(name); if (File.Exists(path)) File.Delete(path); }
    }
}
