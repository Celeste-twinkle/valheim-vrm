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
                AccessTools.Method(typeof(VRMEquipmentSync), "LateUpdate").Invoke(sync, null);
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
            for (int i = 0; i < mounts.Length; i++)
                if (Vector3.Distance(mounts[i].localPosition, positions[i]) > 1e-7f || Quaternion.Angle(mounts[i].localRotation, rotations[i]) > .01f)
                    throw new Exception("Grip did not restore after unbinding");
        }
        target.transform.localScale = scaleBefore;
        File.WriteAllLines(Path.Combine(output, "equipment-checks.txt"), report);
        UnityEngine.Object.Destroy(fixture);
    }
}
