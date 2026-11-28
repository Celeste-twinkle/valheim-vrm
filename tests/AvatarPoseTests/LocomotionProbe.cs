using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;

[DefaultExecutionOrder(13000)]
sealed class GaitObserver : MonoBehaviour
{
    internal Animator Source, Target;
    internal NativePoseSnapshot Snapshot;
    internal float MinOffset = float.PositiveInfinity, MaxOffset = float.NegativeInfinity;
    internal float MinHip = float.PositiveInfinity, MaxHip = float.NegativeInfinity;
    internal Exception Failure;
    internal int Frames;
    void LateUpdate()
    {
        float source = Source.GetBoneTransform(HumanBodyBones.Hips).position.y;
        float target = Target.GetBoneTransform(HumanBodyBones.Hips).position.y;
        float offset = target - source;
        MinOffset = Mathf.Min(MinOffset, offset); MaxOffset = Mathf.Max(MaxOffset, offset);
        MinHip = Mathf.Min(MinHip, source); MaxHip = Mathf.Max(MaxHip, source); Frames++;
        if (float.IsNaN(offset) || Mathf.Abs(target - Source.transform.position.y) > 4) Failure = new Exception("Non-finite/escaping gait pose");
        for (int i = 0; i < Snapshot.Bones.Count; i++)
            if ((Snapshot.Bones[i].localPosition - Snapshot.Positions[i]).sqrMagnitude > 1e-10f)
                Failure = new Exception("Native skeleton changed during gait");
    }
    internal void ResetRange() { MinOffset = MinHip = float.PositiveInfinity; MaxOffset = MaxHip = float.NegativeInfinity; }
}

static class LocomotionProbe
{
    static int contactSamples;
    static void CountContactSample() { contactSamples++; }
    public static IEnumerator Run(GameObject model, Animator source, VRMAnimationSync sync, List<string> report, string output)
    {
        var snapshot = source.gameObject.AddComponent<NativePoseSnapshot>(); snapshot.Setup(source);
        var watch = model.AddComponent<GaitObserver>(); watch.Source = source; watch.Target = model.GetComponent<Animator>(); watch.Snapshot = snapshot;
        source.Rebind(); source.SetBool("onGround", true); source.Play(229373857, 0, .5f); source.Update(0);
        if (Environment.GetEnvironmentVariable("VRM_GAIT_DIAGNOSE") != "1")
        {
            var type = typeof(VRMAnimationSync).Assembly.GetType("ValheimVRM.AvatarStandingReference");
            var measure = AccessTools.Method(type, "Measure"); var offset = AccessTools.Method(type, "Offset");
            float? reference = null;
            foreach (int state in new[] {229373857, -1544306596, -1829310159})
            {
                source.Play(state, 0, .5f); source.Update(0);
                var before = source.GetBoneTransform(HumanBodyBones.Hips).position;
                var calibration = measure.Invoke(null, new object[] {source, watch.Target});
                float height = (float)offset.Invoke(calibration, new object[] {source, watch.Target});
                if (reference.HasValue && Mathf.Abs(height-reference.Value) > .0001f) throw new Exception("Calibration depends on loading pose");
                if (Vector3.Distance(before, source.GetBoneTransform(HumanBodyBones.Hips).position) > .00001f) throw new Exception("Calibration modified native rig");
                reference = height;
            }
            report.Add("bind-skeleton calibration is identical when loaded standing, ground-sitting or chair-sitting; native hips unchanged");
            source.Rebind(); source.SetBool("onGround", true); source.Play(229373857, 0, .5f); source.Update(0);
        }
        var clipsSeen = new HashSet<string>(); var scale = model.transform.localScale;
        bool diagnose = Environment.GetEnvironmentVariable("VRM_GAIT_DIAGNOSE") == "1";
        var patch = new Harmony("valheimvrm.tests.gait.contacts");
        patch.Patch(AccessTools.Method(typeof(VRMAnimationSync).Assembly.GetType("ValheimVRM.AvatarGroundContact"), "Sample"),
            prefix: new HarmonyMethod(typeof(LocomotionProbe), nameof(CountContactSample)));
        contactSamples = 0;
        sync.enabled = true;
        foreach (float ratio in new[] { 1f, .7f, 1.4f })
        {
            model.transform.localScale = scale * ratio;
            model.transform.parent.position = new Vector3(4, ratio == 1f ? 0 : 1200, -2);
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            foreach (float speed in new[] { 0f, 1.5f, 3f, 6f, 8f, 0f })
            {
                source.SetFloat("forward_speed", speed); watch.ResetRange();
                for (int frame = 0; frame < (Environment.GetEnvironmentVariable("VRM_GAIT_SHORT") == "1" ? 60 : 120); frame++)
                {
                    yield return null;
                    if (watch.Failure != null) throw watch.Failure;
                }
                var clips = source.GetCurrentAnimatorClipInfo(0).Where(c => c.weight > .01f).Select(c => c.clip.name).Distinct().ToArray();
                foreach (var clip in clips) clipsSeen.Add(clip);
                minimum = Mathf.Min(minimum, watch.MinOffset); maximum = Mathf.Max(maximum, watch.MaxOffset);
                report.Add($"gait ratio={ratio} speed={speed} extraLiftRange={watch.MaxOffset-watch.MinOffset:F6}m authoredHipRange={watch.MaxHip-watch.MinHip:F6}m offset={watch.MinOffset:F6}..{watch.MaxOffset:F6} clips={string.Join(",", clips)}");
                File.WriteAllLines(Path.Combine(output, "results.txt"), report);
            }
            if (!diagnose && maximum-minimum > .001f) throw new Exception("Gait correction varies with animation: " + (maximum-minimum));
            report.Add($"gait scale={ratio}: constant-offset range across start/walk/run/stop={maximum-minimum:F6}m");
        }
        patch.UnpatchSelf();
        report.Add("production contact samples during locomotion=" + contactSamples);
        if (!diagnose && contactSamples != 0) throw new Exception("Locomotion is still dynamically sampling contacts");
        if (!clipsSeen.Any(c => c.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0) || !clipsSeen.Any(c => c.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0))
            throw new Exception("Fixture did not enter both walking and running clips");
        watch.enabled = sync.enabled = snapshot.enabled = false;
        model.transform.localScale = scale; model.transform.parent.position = Vector3.zero;
        source.SetFloat("forward_speed", 0); source.Rebind(); source.Play(229373857, 0, .5f); source.Update(0);
        AccessTools.Method(typeof(VRMAnimationSync), "LateUpdate").Invoke(sync, null);
        using (var rendered = new GroundingProbe.Surface(watch.Target))
        {
            rendered.Sample(out float soles, out _, out _);
            report.Add($"reference standing sole height={soles:F6}m; {watch.Frames} continuous locomotion frames");
            if (!diagnose && Mathf.Abs(soles) > .05f) throw new Exception("Standing sole reference is more than 5 cm off: " + soles);
        }
        UnityEngine.Object.Destroy(watch); UnityEngine.Object.Destroy(snapshot);
    }
}
