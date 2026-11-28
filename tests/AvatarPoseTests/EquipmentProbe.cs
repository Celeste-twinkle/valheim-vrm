using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;

static class EquipmentProbe
{
    public static void Run(Animator source, Animator target, VRMAnimationSync animation, string output)
    {
        var fixture = new GameObject("EquipmentProbe"); fixture.SetActive(false);
        var equipment = fixture.AddComponent<VisEquipment>();
        equipment.m_leftHand = source.GetBoneTransform(HumanBodyBones.LeftHand).Find("LeftHand_Attach");
        equipment.m_rightHand = source.GetBoneTransform(HumanBodyBones.RightHand).Find("RightHand_Attach");
        var mountBones = new[] { HumanBodyBones.Head, HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.UpperChest, HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest };
        var mountFields = new[] { "m_helmet", "m_backShield", "m_backMelee", "m_backTwohandedMelee", "m_backBow", "m_backTool", "m_backAtgeir" };
        var bodyMounts = new Transform[mountBones.Length];
        var bodySources = new Transform[mountBones.Length];
        var bodyTargets = new Transform[mountBones.Length];
        for (int i = 0; i < bodyMounts.Length; i++)
        {
            var bone = mountBones[i];
            if (source.GetBoneTransform(bone) == null || target.GetBoneTransform(bone) == null) bone = HumanBodyBones.Spine;
            bodySources[i] = source.GetBoneTransform(bone); bodyTargets[i] = target.GetBoneTransform(bone);
            bodyMounts[i] = new GameObject(mountFields[i] + "_test").transform;
            bodyMounts[i].SetParent(bodySources[i], false);
            bodyMounts[i].localPosition = new Vector3(.01f * i, .04f, -.03f);
            AccessTools.Field(typeof(VisEquipment), mountFields[i]).SetValue(equipment, bodyMounts[i]);
        }
        var bodyPositions = Array.ConvertAll(bodyMounts, m => m.localPosition);
        var sync = fixture.AddComponent<VRMEquipmentSync>();
        var report = new List<string>();
        var mounts = new[] { equipment.m_leftHand, equipment.m_rightHand };
        var positions = Array.ConvertAll(mounts, m => m.localPosition);
        var rotations = Array.ConvertAll(mounts, m => m.localRotation);
        sync.Setup(source, source, equipment);
        var before = Array.ConvertAll(mounts, m => m.position);
        AccessTools.Method(typeof(VRMEquipmentSync), "LateUpdate").Invoke(sync, null);
        for (int i = 0; i < mounts.Length; i++)
            if (Vector3.Distance(before[i], mounts[i].position) > .0001f) throw new Exception("Same-avatar grip changed");
        sync.ResetAttachments();
        var scaleBefore = target.transform.localScale;
        foreach (float scale in new[] { .6f, 1f, 1.4f })
        {
            target.transform.localScale = scaleBefore * scale;
            sync.Setup(source, target, equipment);
            foreach (int state in new[] { 229373857, 890925016, -1544306596, -805461806 })
            {
                source.Rebind(); source.Play(state, 0, .6f); source.Update(0);
                AccessTools.Method(typeof(VRMAnimationSync), "LateUpdate").Invoke(animation, null);
                var v10 = target.GetComponent<UniVRM10.Vrm10Instance>();
                if (v10 != null) v10.Runtime.Process();
                var nativePositions = Array.ConvertAll(bodySources, b => b.position);
                AccessTools.Method(typeof(VRMEquipmentSync), "LateUpdate").Invoke(sync, null);
                for (int j = 0; j < bodyMounts.Length; j++)
                {
                    var expected = bodyTargets[j].position + bodySources[j].TransformVector(bodyPositions[j]);
                    if (Vector3.Distance(bodyMounts[j].position, expected) > .0001f || Vector3.Distance(bodySources[j].position, nativePositions[j]) > .0001f)
                        throw new Exception("Body socket did not follow avatar or changed native bones");
                }
                for (int i = 0; i < mounts.Length; i++)
                {
                    var hand = target.GetBoneTransform(i == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    var middle = target.GetBoneTransform(i == 0 ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                    float distance = Vector3.Distance(mounts[i].position, hand.position);
                    float palmLength = Vector3.Distance(middle.position, hand.position);
                    report.Add("scale=" + scale + " state=" + state + " side=" + i + " wristToGrip=" + distance.ToString("F5") + " palmLength=" + palmLength.ToString("F5") + " rotation=" + mounts[i].rotation);
                    if (float.IsNaN(distance) || distance > palmLength * 1.6f || distance < palmLength * .3f) throw new Exception("Grip is outside the palm");
                }
            }
            sync.ResetAttachments();
            for (int j = 0; j < bodyMounts.Length; j++)
                if (Vector3.Distance(bodyMounts[j].localPosition, bodyPositions[j]) > .0001f) throw new Exception("Body socket was not restored");
            for (int i = 0; i < mounts.Length; i++)
                if (Vector3.Distance(mounts[i].localPosition, positions[i]) > 1e-7f || Quaternion.Angle(mounts[i].localRotation, rotations[i]) > .01f)
                    throw new Exception("Grip did not restore after unbinding");
        }
        // A misconfigured socket referencing a real bone must be ignored.
        equipment.m_helmet = source.GetBoneTransform(HumanBodyBones.Head);
        sync.Setup(source, target, equipment);
        var headPosition = equipment.m_helmet.position;
        AccessTools.Method(typeof(VRMEquipmentSync), "LateUpdate").Invoke(sync, null);
        if (Vector3.Distance(headPosition, equipment.m_helmet.position) > .0001f) throw new Exception("Humanoid bone accepted as a socket");
        sync.ResetAttachments();
        foreach (var mount in bodyMounts) UnityEngine.Object.Destroy(mount.gameObject);
        target.transform.localScale = scaleBefore;
        File.WriteAllLines(Path.Combine(output, "equipment-checks.txt"), report);
        UnityEngine.Object.Destroy(fixture);
    }
}
