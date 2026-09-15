using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using Object = UnityEngine.Object;

static class ActionCalibrationProbe
{
    public static IEnumerator RunEquipmentAxes(GameObject imported, Animator source, List<string> report, string output)
    {
        CheckPreferences(output);
        foreach(float height in new[]{1.4f,2f,2.2f})
        {
            source.Rebind();source.Play("Base Layer.Movement",0,.5f);source.Update(0);
            var model=Object.Instantiate(imported,source.transform.parent);
            AvatarScale.ApplyHeight(model,height);
            AccessTools.Method(typeof(ValheimVRM.VRM),"PrepareVrm10Clone").Invoke(null,new object[]{imported,model});
            model.SetActive(true);foreach(var b in model.GetComponents<MonoBehaviour>())b.enabled=false;
            var settings=new ValheimVRM.Settings.VrmSettingsContainer{Name="EquipmentAxesProbe"};
            var sync=model.AddComponent<VRMAnimationSync>();sync.Setup(source,settings);sync.enabled=false;
            var target=model.GetComponent<Animator>();
            ItemCalibrationProbe.Run(source,target,settings);
            BackEquipmentProbe.Run(source,target,settings,sync);
            report.Add("equipment axes height="+height+": 4 held item types x 90 changing headings/wrist rotations; 9 back items x 3 draw/sheath cycles x 6 animations x 30 frames; scale/reset/no accumulation PASS");
            File.WriteAllLines(Path.Combine(output,"results.txt"),report);
            Object.Destroy(model);yield return null;
        }
        report.Add("AVATAR_EQUIPMENT_AXES_PASSED");
    }
    static readonly System.Reflection.MethodInfo Tick = AccessTools.Method(typeof(VRMAnimationSync), "LateUpdate");
    public static IEnumerator Run(GameObject imported, Animator source, List<string> report, string output)
    {
        CheckPreferences(output);
        foreach (float height in new[] { 1.4f, 2f, 2.2f })
        {
            source.Rebind(); source.Play("Base Layer.Movement", 0, .5f); source.Update(0);
            var nativeBones = source.GetComponentsInChildren<Transform>(true);
            var nativePositions = nativeBones.Select(b => b.localPosition).ToArray();
            var nativeRotations = nativeBones.Select(b => b.localRotation).ToArray();
            var model = Object.Instantiate(imported, source.transform.parent);
            AvatarScale.ApplyHeight(model, height);
            AccessTools.Method(typeof(ValheimVRM.VRM), "PrepareVrm10Clone").Invoke(null, new object[] { imported, model });
            model.SetActive(true);
            foreach (var behaviour in model.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var target = model.GetComponent<Animator>();
            var bones = model.GetComponentsInChildren<Transform>(true);
            var positions = bones.Select(b => b.localPosition).ToArray();
            var rotations = bones.Select(b => b.localRotation).ToArray();
            string name = "ActionProbe";
            var profile = AvatarCalibrationOptions.Current.Get(name);
            profile.Animations.Clear();
            var settings = new ValheimVRM.Settings.VrmSettingsContainer { Name = name };
            var sync = model.AddComponent<VRMAnimationSync>();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            sync.Setup(source, settings); sync.enabled = false; timer.Stop();
            AssertPose(nativeBones, nativePositions, nativeRotations, "native setup");
            AssertPose(bones, positions, rotations, "target setup");
            if (sync.AnimationCatalog.Entries.Count != 172 || sync.CalibratedStateCount != 172) throw new Exception("Incomplete animation catalog / calibration");
            if (sync.AnimationCatalog.Entries.Single(e => e.Path == "Base Layer.standup 0").Contact != AvatarContactKind.Ground ||
                sync.AnimationCatalog.Entries.Single(e => e.Path == "Base Layer.standup").Contact != AvatarContactKind.Reclining)
                throw new Exception("Ground-sit and bed exit states mixed up");
            int count = 0, frames = 0;
            float drift = 0, contactError = 0;
            using (var sourceSurface = new GroundingProbe.Surface(source))
            using (var targetSurface = new GroundingProbe.Surface(target))
            {
                foreach (var entry in sync.AnimationCatalog.Entries.ToArray())
                {
                    source.Rebind();
                    source.Play("Base Layer.Movement", 0, .5f);
                    source.SetLayerWeight(1, entry.LayerIndex == 1 ? 1 : 0);
                    source.Play(entry.Hash, entry.LayerIndex, .5f); source.Update(0);
                    var nativeHip = source.GetBoneTransform(HumanBodyBones.Hips);
                    var targetHip = target.GetBoneTransform(HumanBodyBones.Hips);
                    var before = nativeHip.position;
                    Tick.Invoke(sync, null);
                    var baseline = targetHip.position;
                    if (!Finite(baseline) || (baseline - before).magnitude > 1.8f) throw new Exception("Invalid calibration " + entry.Path + ": " + (baseline - before));
                    if (entry.LayerIndex == 0 &&
                        (entry.Name == "Emote_sit" || entry.Name.StartsWith("Sit") || entry.Name.StartsWith("Ride")))
                    {
                        sourceSurface.Sample(out _, out float nativeSeat, out _);
                        targetSurface.Sample(out _, out float avatarSeat, out float avatarLower);
                        float error = Mathf.Abs(entry.Name == "Emote_sit" ? avatarLower - source.transform.position.y : avatarSeat - nativeSeat);
                        contactError = Mathf.Max(contactError, error);
                        if (error > .025f) throw new Exception("Contact mismatch " + entry.Path + " error=" + error);
                    }
                    if (entry.LayerIndex == 0 &&
                        (entry.Name == "HoldMast" || entry.Name == "HoldDragon" || entry.Name == "In Water"))
                    {
                        Vector3 a = Anchor(source, entry), b = Anchor(target, entry);
                        float error = Vector3.Distance(a, b);
                        contactError = Mathf.Max(contactError, error);
                        if (error > .01f) throw new Exception("Anchor mismatch " + entry.Path + " error=" + error);
                    }
                    var offset = new Vector3(.07f, -.03f, .09f);
                    profile.Set(entry.Path, offset);
                    Tick.Invoke(sync, null);
                    if (!source.IsInTransition(entry.LayerIndex))
                    {
                        var delta = targetHip.position - baseline;
                        if (Vector3.Distance(delta, source.transform.rotation * offset) > .0005f)
                            throw new Exception("Manual axes mismatch " + entry.Path + " " + delta);
                    }
                    Vector3 first = targetHip.position;
                    for (int i = 0; i < 30; i++)
                    {
                        Tick.Invoke(sync, null); frames++;
                        drift = Mathf.Max(drift, Vector3.Distance(first, targetHip.position));
                        if (nativeHip.position != before) throw new Exception("Native hip written by calibration");
                    }
                    profile.Set(entry.Path, Vector3.zero);
                    Tick.Invoke(sync, null);
                    if (Vector3.Distance(baseline, targetHip.position) > .0005f) throw new Exception("Reset retained a correction");
                    count++;
                }
            }
            if (drift > .00001f) throw new Exception("Accumulated drift: " + drift);
            CheckBlending(source, target, sync, profile);
            EquipmentProbe.Run(source, target, sync, output);
            ItemCalibrationProbe.Run(source, target, settings);
            BackEquipmentProbe.Run(source, target, settings, sync);
            report.Add("height=" + height + " native back equipment: 9 item types, 3 draw/sheath cycles, 6 animations including equip_hip/equip_head, 4860 stepped refresh checks; hammer geometry/XYZ/scale/reset PASS");
            report.Add("height=" + height + " states=" + count + " repeatFrames=" + frames + " drift=" + drift +
                " contactError=" + contactError + " calibrationMs=" + timer.ElapsedMilliseconds + " XYZ/reset/equipment PASS");
            // Actual locomotion over successive frames must retain one baseline.
            source.Rebind(); source.Play("Base Layer.Movement"); source.SetLayerWeight(1, 0);
            Vector3? reference = null; float gaitDrift = 0;
            for (int frame = 0; frame < 360; frame++)
            {
                source.SetFloat("forward_speed", frame < 120 ? 2f : frame < 240 ? 7f : 0);
                source.Update(1f / 60); Tick.Invoke(sync, null);
                var lift = target.GetBoneTransform(HumanBodyBones.Hips).position - source.GetBoneTransform(HumanBodyBones.Hips).position;
                if (reference.HasValue) gaitDrift = Mathf.Max(gaitDrift, Vector3.Distance(lift, reference.Value)); else reference = lift;
                yield return null;
            }
            if (gaitDrift > .0001f) throw new Exception("Gait baseline moved " + gaitDrift);
            report.Add("height=" + height + " locomotionFrames=360 baselineDrift=" + gaitDrift + " PASS");
            Object.Destroy(model); yield return null;
        }
    }
    static Vector3 Anchor(Animator a, AvatarAnimationEntry entry) => entry.Name == "In Water"
        ? a.GetBoneTransform(HumanBodyBones.Head).position :
        (a.GetBoneTransform(HumanBodyBones.LeftHand).position + a.GetBoneTransform(HumanBodyBones.RightHand).position) * .5f;
    internal static void CheckBlending(Animator source, Animator target, VRMAnimationSync sync, AvatarCalibrationOptions.Profile profile)
    {
        var movement = sync.AnimationCatalog.Entries.Single(e => e.Path == "Base Layer.Movement");
        source.Rebind(); source.SetBool("onGround", true); source.Play(movement.Hash, 0, .5f);
        source.SetFloat("forward_speed", 3.25f); source.Update(0);
        Tick.Invoke(sync, null);
        var baseline = target.GetBoneTransform(HumanBodyBones.Hips).position;
        var common = new Vector3(.02f, .03f, -.04f);
        foreach (string clip in movement.Clips) profile.Set(movement.ClipKey(clip), common);
        Tick.Invoke(sync, null);
        var actual = target.GetBoneTransform(HumanBodyBones.Hips).position - baseline;
        if (Vector3.Distance(actual, source.transform.rotation * common) > .0001f) throw new Exception("Blend clip offsets applied with incorrect weights");
        foreach (string clip in movement.Clips) profile.Set(movement.ClipKey(clip), Vector3.zero);
        var chair = sync.AnimationCatalog.Entries.Single(e => e.Path == "Base Layer.SitChair");
        source.CrossFadeInFixedTime(chair.Hash, .5f, 0, .5f);
        int transitions = 0;
        for (int frame = 0; frame < 20; frame++)
        {
            source.Update(.01f); Tick.Invoke(sync, null);
            baseline = target.GetBoneTransform(HumanBodyBones.Hips).position;
            // Equal offsets on every state must survive transition/layer blending
            // without doubling or disappearing, independently of transition phase.
            foreach (var entry in sync.AnimationCatalog.Entries.Where(e => e.LayerIndex == 0)) profile.Set(entry.Path, common);
            Tick.Invoke(sync, null);
            actual = target.GetBoneTransform(HumanBodyBones.Hips).position - baseline;
            if (Vector3.Distance(actual, source.transform.rotation * common) > .0001f) throw new Exception("State transition doubled/lost manual offset");
            foreach (var entry in sync.AnimationCatalog.Entries.Where(e => e.LayerIndex == 0)) profile.Set(entry.Path, Vector3.zero);
            if (source.IsInTransition(0)) transitions++;
        }
        if (transitions == 0) throw new Exception("Transition test did not exercise a transition");
    }
    static bool Finite(Vector3 value) => !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
    static void AssertPose(Transform[] bones, Vector3[] positions, Quaternion[] rotations, string context)
    {
        for (int i = 0; i < bones.Length; i++)
            if (Vector3.Distance(bones[i].localPosition, positions[i]) > .00001f || Quaternion.Angle(bones[i].localRotation, rotations[i]) > .05f)
                throw new Exception(context + " changed " + bones[i].name);
    }
    static void CheckPreferences(string output)
    {
        var options = new AvatarCalibrationOptions(Path.Combine(output, "preferences"));
        var a = options.Get("A");
        options.Get("B");
        a.Set("Base Layer.In Water", new Vector3(.11f, -.19f, .23f));
        a.Left.Scale = .4f; a.Right.Scale = 1.6f; a.TwoHanded.Scale = 1.2f;
        a.Left.Position.Value = new Vector3(.1f, .2f, .3f);
        a.Back.Scale = 1.43f; a.Back.Position.Value = new Vector3(-.2f,.15f,.3f);
        options.Changed(); options.Save(); options.Load();
        if (options.Get("A").Get("Base Layer.In Water") != new Vector3(.11f, -.19f, .23f) ||
            options.Get("B").Get("Base Layer.In Water") != Vector3.zero ||
            options.Get("A").Right.Multiplier != 1.6f || options.Get("A").Back.Multiplier != 1.43f ||
            options.Get("A").Back.Position.Value != new Vector3(-.2f,.15f,.3f)) throw new Exception("Calibration persistence/isolation failed");
        if (AvatarCalibrationOptions.ClampScale(float.NaN) != 1 || AvatarCalibrationOptions.ClampScale(99) != 2) throw new Exception("Unsafe scale bounds");
    }
}
