using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;

// Observe both sides of the production LateUpdate; never force animator evaluation
// between frames. Manual Rebind/Update tests can hide writes back to native bones.
[DefaultExecutionOrder(9000)]
sealed class NativePoseSnapshot : MonoBehaviour
{
    internal readonly List<Transform> Bones = new List<Transform>();
    internal Vector3[] Positions;
    internal Quaternion[] Rotations;
    internal void Setup(Animator source)
    {
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        { var bone = source.GetBoneTransform((HumanBodyBones)i); if (bone != null) Bones.Add(bone); }
        Positions = new Vector3[Bones.Count]; Rotations = new Quaternion[Bones.Count];
    }
    void LateUpdate()
    {
        for (int i = 0; i < Bones.Count; i++)
        { Positions[i] = Bones[i].localPosition; Rotations[i] = Bones[i].localRotation; }
    }
}

[DefaultExecutionOrder(13000)]
sealed class LiveContactObserver : MonoBehaviour
{
    internal Animator Source, Target;
    internal NativePoseSnapshot Snapshot;
    internal Exception Failure;
    internal int Frames, Samples;
    internal float MaximumError, MaximumHip;
    internal GroundingProbe.Surface Original, Rendered;
    void LateUpdate()
    {
        if (Failure != null) return;
        try
        {
            for (int i = 0; i < Snapshot.Bones.Count; i++)
                if ((Snapshot.Bones[i].localPosition - Snapshot.Positions[i]).sqrMagnitude > 1e-10f ||
                    Quaternion.Angle(Snapshot.Bones[i].localRotation, Snapshot.Rotations[i]) > .05f)
                    throw new Exception("Native skeleton was modified: " + Snapshot.Bones[i].name);
            float hip = Target.GetBoneTransform(HumanBodyBones.Hips).position.y - Source.transform.position.y;
            MaximumHip = Mathf.Max(MaximumHip, Mathf.Abs(hip));
            if (float.IsNaN(hip) || Mathf.Abs(hip) > 4) throw new Exception("Avatar escaped character: " + hip);
            Frames++;
            if (Frames % 15 != 0 || Source.IsInTransition(0)) return;
            Original.Sample(out float sourceFoot, out float sourceSeat, out _);
            Rendered.Sample(out float foot, out float seat, out float lower);
            int state = Source.GetCurrentAnimatorStateInfo(0).shortNameHash;
            float error;
            if (state == 890925016 || state == -1544306596 || state == -805461806)
                error = Mathf.Abs(lower - Source.transform.position.y);
            else if (state == -1829310159)
                error = Mathf.Abs(seat - sourceSeat);
            else
                error = Mathf.Abs(foot - Mathf.Max(sourceFoot, Source.transform.position.y));
            MaximumError = Mathf.Max(MaximumError, error); Samples++;
            if (error > .012f) throw new Exception($"Live surface mismatch: frame={Frames}, state={state}, error={error}, foot={foot}, lower={lower}");
        }
        catch (Exception error) { Failure = error; }
    }
}

static class LivePoseProbe
{
    public static IEnumerator Run(GameObject model, Animator source, VRMAnimationSync sync, List<string> report, string output)
    {
        source.Rebind(); source.Play(229373857, 0, .5f); source.Update(0);
        var target = model.GetComponent<Animator>();
        var snapshot = source.gameObject.AddComponent<NativePoseSnapshot>(); snapshot.Setup(source);
        var observer = model.AddComponent<LiveContactObserver>();
        observer.Source = source; observer.Target = target; observer.Snapshot = snapshot;
        observer.Original = new GroundingProbe.Surface(source); observer.Rendered = new GroundingProbe.Surface(target);
        var initialScale = model.transform.localScale;
        sync.enabled = true;
        foreach (float scale in new[] { 1f, .7f, 1.4f })
        {
            model.transform.localScale = initialScale * scale;
            // Moving the parent tests world-space support without accumulating it.
            model.transform.parent.position = new Vector3(3, scale == 1f ? 0 : 1200, -4);
            foreach (int state in new[] { 229373857, 890925016, -1544306596, -805461806, -1829310159 })
            {
                source.Play(state, 0, state == 229373857 ? .5f : 0);
                int count = state == 229373857 || state == -1544306596 ? 120 : 40;
                for (int frame = 0; frame < count; frame++)
                {
                    yield return null;
                    if (observer.Failure != null) throw observer.Failure;
                }
                report.Add($"live scale={scale} requestedState={state} frames={observer.Frames} maxError={observer.MaximumError:F6}m maxHip={observer.MaximumHip:F4}m");
                File.WriteAllLines(Path.Combine(output, "results.txt"), report);
            }
        }
        sync.enabled = false; observer.enabled = false; snapshot.enabled = false;
        observer.Original.Dispose(); observer.Rendered.Dispose();
        report.Add($"LIVE PASS: {observer.Frames} automatic Update/animation/LateUpdate frames, {observer.Samples} independently baked contacts; native bones unchanged; maxError={observer.MaximumError:F6}m");
        model.transform.localScale = initialScale; model.transform.parent.position = Vector3.zero;
        UnityEngine.Object.Destroy(observer); UnityEngine.Object.Destroy(snapshot);
    }
}
