using System;
using System.Collections.Generic;
using System.Linq;
using UniGLTF;
using UniGLTF.Utils;
using UniVRM10;
using UnityEngine;
using VRM;

namespace ValheimVRM
{
    // Springs run at 11000; equipment follows the final pose at 12000.
    [DefaultExecutionOrder(11500)]
    public sealed class AvatarPhysicsWeight : MonoBehaviour
    {
        sealed class Joint
        {
            public Transform Bone;
            public Quaternion Rest;
            public Quaternion Simulated;
        }
        Joint[] joints = new Joint[0];
        bool blended;
        public float SynchronizedWeight = -1;
        Vrm10Runtime runtime;
        delegate void ConstraintProcessor(in TransformState target, in TransformState source);
        IVrm10Constraint[] followers = new IVrm10Constraint[0];
        ConstraintProcessor[] processors = new ConstraintProcessor[0];
        Quaternion[] followerRotations = new Quaternion[0];

        public void Setup()
        {
            RestoreSimulation();
            var bones = new HashSet<Transform>();
            var instance = GetComponent<Vrm10Instance>();
            runtime = instance != null ? instance.Runtime : null;
            if (instance != null)
            {
                foreach (var spring in instance.SpringBone.Springs)
                    foreach (var joint in spring.Joints)
                        if (joint != null) bones.Add(joint.transform);
            }
            else
            {
                foreach (var spring in GetComponentsInChildren<VRMSpringBone>(true))
                    foreach (var root in spring.RootBones)
                        if (root != null)
                            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
                                if (bone.childCount > 0) bones.Add(bone);
            }

            var imported = GetComponent<RuntimeGltfInstance>();
            var pose = runtime != null ? runtime.InitPose : imported?.InitialTransformStates;
            joints = bones.Select(bone => new Joint { Bone = bone,
                Rest = pose != null && pose.TryGetValue(bone, out var state) ? state.LocalRotation : bone.localRotation }).ToArray();

            // Followers outside the simulated chains (for example garment roll constraints)
            // must see the weighted pose. Do not overwrite a spring or its ancestors.
            var affected = new HashSet<Transform>(bones);
            var selected = new List<IVrm10Constraint>();
            if (runtime != null)
                foreach (var constraint in runtime.Constraints)
                {
                    var source = constraint.ConstraintSource; var target = constraint.ConstraintTarget;
                    if (source == null || target == null || bones.Any(b => b == target || b.IsChildOf(target))) continue;
                    if (!affected.Any(b => source == b || source.IsChildOf(b))) continue;
                    selected.Add(constraint); affected.Add(target);
                }
            followers = selected.ToArray();
            var process = HarmonyLib.AccessTools.Method(typeof(IVrm10Constraint), "Process");
            processors = followers.Select(c => (ConstraintProcessor)Delegate.CreateDelegate(typeof(ConstraintProcessor), c, process)).ToArray();
            followerRotations = new Quaternion[followers.Length];
        }

        // The reduced display pose must never be fed back into the next spring step.
        // Restore before animation/constraints run, including while the weight is zero.
        void Update() { RestoreSimulation(); }
        void OnDisable() { RestoreSimulation(); }
        void RestoreSimulation()
        {
            if (!blended) return;
            foreach (var joint in joints) if (joint.Bone != null) joint.Bone.localRotation = joint.Simulated;
            for (int i = 0; i < followers.Length; i++)
                if (followers[i].ConstraintTarget != null) followers[i].ConstraintTarget.localRotation = followerRotations[i];
            blended = false;
        }

        void LateUpdate() { ApplyWeight(SynchronizedWeight >= 0 ? SynchronizedWeight : AvatarPhysics.Weight); }
        internal void ApplyWeight(float weight)
        {
            weight = AvatarPhysics.ClampWeight(weight);
            if (weight < 1)
                for (int i = 0; i < followers.Length; i++)
                    if (followers[i].ConstraintTarget != null) followerRotations[i] = followers[i].ConstraintTarget.localRotation;
            foreach (var joint in joints)
            {
                if (joint.Bone == null) continue;
                joint.Simulated = joint.Bone.localRotation;
                if (weight < 1) joint.Bone.localRotation = Quaternion.Slerp(joint.Rest, joint.Simulated, weight);
            }
            blended = weight < 1;
            if (weight >= 1 || runtime == null) return;
            for (int i = 0; i < followers.Length; i++)
            {
                var constraint = followers[i];
                if (constraint.ConstraintTarget == null || constraint.ConstraintSource == null) continue;
                if (!runtime.InitPose.TryGetValue(constraint.ConstraintTarget, out var target) ||
                    !runtime.InitPose.TryGetValue(constraint.ConstraintSource, out var source)) continue;
                processors[i](in target, in source);
            }
        }
    }
}
