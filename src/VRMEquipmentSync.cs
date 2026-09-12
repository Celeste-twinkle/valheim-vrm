using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRM
{
    // Equipment follows the final pose, after humanoid retargeting (10000) and
    // UniVRM constraints/springs (11000). Keep the game's attachment hierarchy.
    [DefaultExecutionOrder(12000)]
    public sealed class VRMEquipmentSync : MonoBehaviour
    {
        sealed class Grip
        {
            internal Transform Mount, SourceHand, TargetHand;
            internal Vector3 OriginalPosition, TargetPosition;
            internal Quaternion OriginalRotation, TargetRotation;
            internal bool HasPalm;
            internal float FallbackScale;

            internal void Apply()
            {
                if (Mount == null || SourceHand == null || TargetHand == null) return;
                Mount.position = HasPalm ? TargetHand.TransformPoint(TargetPosition) :
                    TargetHand.position + SourceHand.TransformVector(TargetPosition) * FallbackScale;
                Mount.rotation = (HasPalm ? TargetHand.rotation : SourceHand.rotation) * TargetRotation;
            }

            internal void Restore()
            {
                if (Mount == null) return;
                Mount.localPosition = OriginalPosition;
                Mount.localRotation = OriginalRotation;
            }
        }

        struct Palm
        {
            internal Quaternion Rotation;
            internal Vector3 Size;
        }

        readonly List<Grip> grips = new List<Grip>(2);
        Animator target;

        public void Setup(Animator original, Animator avatar, VisEquipment equipment)
        {
            ResetAttachments();
            if (original == null || avatar == null || equipment == null) return;
            target = avatar;
            var sourcePose = GetBindPose(original);
            var targetPose = GetBindPose(avatar);
            AddGrip(original, avatar, equipment.m_leftHand, true, sourcePose, targetPose);
            AddGrip(original, avatar, equipment.m_rightHand, false, sourcePose, targetPose);
        }

        void AddGrip(Animator original, Animator avatar, Transform mount, bool left,
            Dictionary<Transform, Matrix4x4> sourcePose, Dictionary<Transform, Matrix4x4> targetPose)
        {
            var bone = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var sourceHand = original.GetBoneTransform(bone);
            var targetHand = avatar.GetBoneTransform(bone);
            if (mount == null || sourceHand == null || targetHand == null || !mount.IsChildOf(sourceHand)) return;
            var point = sourceHand.InverseTransformPoint(mount.position);
            var rotation = Quaternion.Inverse(sourceHand.rotation) * mount.rotation;
            var grip = new Grip {
                Mount = mount, SourceHand = sourceHand, TargetHand = targetHand,
                OriginalPosition = mount.localPosition, OriginalRotation = mount.localRotation,
                TargetPosition = point, TargetRotation = rotation,
                FallbackScale = avatar.humanScale / Mathf.Max(original.humanScale, .0001f)
            };
            if (TryGetPalm(original, left, sourcePose, out var sourcePalm) &&
                TryGetPalm(avatar, left, targetPose, out var targetPalm))
            {
                // Transfer the authored grip in dimensionless palm coordinates.
                // Width/thickness follow the finger spread; length follows the
                // wrist-to-finger-base distance. No avatar names or fixed offsets.
                var local = Quaternion.Inverse(sourcePalm.Rotation) * point;
                local = new Vector3(local.x / sourcePalm.Size.x, local.y / sourcePalm.Size.y, local.z / sourcePalm.Size.z);
                grip.TargetPosition = targetPalm.Rotation * Vector3.Scale(local, targetPalm.Size);
                grip.TargetRotation = targetPalm.Rotation * Quaternion.Inverse(sourcePalm.Rotation) * rotation;
                grip.HasPalm = true;
            }
            grips.Add(grip);
        }

        static Dictionary<Transform, Matrix4x4> GetBindPose(Animator animator)
        {
            var pose = new Dictionary<Transform, Matrix4x4>();
            foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                var matrices = renderer.sharedMesh.bindposes;
                var bones = renderer.bones;
                for (int i = 0; i < bones.Length && i < matrices.Length; i++)
                    if (bones[i] != null && !pose.ContainsKey(bones[i]))
                        pose.Add(bones[i], renderer.transform.localToWorldMatrix * matrices[i].inverse);
            }
            return pose;
        }

        static Matrix4x4 RestMatrix(Transform bone, Dictionary<Transform, Matrix4x4> pose)
        {
            if (pose.TryGetValue(bone, out var matrix)) return matrix;
            // Optional finger bases may be unweighted. Their local position is
            // still useful when a parent is present in the skin's bind pose.
            if (bone.parent != null)
                return RestMatrix(bone.parent, pose) * Matrix4x4.TRS(bone.localPosition, bone.localRotation, bone.localScale);
            return bone.localToWorldMatrix;
        }

        static bool TryGetPalm(Animator animator, bool left, Dictionary<Transform, Matrix4x4> pose, out Palm palm)
        {
            palm = default(Palm);
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            if (little == null) little = animator.GetBoneTransform(left ? HumanBodyBones.LeftRingProximal : HumanBodyBones.RightRingProximal);
            if (hand == null || index == null || little == null) return false;
            var inverse = RestMatrix(hand, pose).inverse;
            var indexPoint = inverse.MultiplyPoint3x4(RestMatrix(index, pose).MultiplyPoint3x4(Vector3.zero));
            var littlePoint = inverse.MultiplyPoint3x4(RestMatrix(little, pose).MultiplyPoint3x4(Vector3.zero));
            var forward = middle != null ? inverse.MultiplyPoint3x4(RestMatrix(middle, pose).MultiplyPoint3x4(Vector3.zero)) : (indexPoint + littlePoint) * .5f;
            var across = indexPoint - littlePoint;
            float length = forward.magnitude;
            if (length < 1e-7f) return false;
            across = Vector3.ProjectOnPlane(across, forward / length);
            float width = across.magnitude;
            if (width < 1e-7f) return false;
            palm.Rotation = Quaternion.LookRotation(forward, Vector3.Cross(forward / length, across / width));
            palm.Size = new Vector3(width, width, length);
            return true;
        }

        void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy) { ResetAttachments(); return; }
            foreach (var grip in grips) grip.Apply();
        }

        public void ResetAttachments()
        {
            foreach (var grip in grips) grip.Restore();
            grips.Clear();
            target = null;
        }

        void OnDisable() { ResetAttachments(); }
        void OnDestroy() { ResetAttachments(); }
    }
}
