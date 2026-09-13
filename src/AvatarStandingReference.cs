using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRM
{
    // Bind-pose hips, ankles and sole geometry give one stable reference even
    // when the avatar is attached while its character is running or sitting.
    internal struct AvatarStandingReference
    {
        float nativeClearance, avatarClearance;

        public float Offset(Animator native, Animator avatar)
        {
            return avatarClearance * Mathf.Abs(avatar.transform.lossyScale.y)
                - nativeClearance * Mathf.Abs(native.transform.lossyScale.y);
        }

        public static AvatarStandingReference Measure(Animator native, Animator avatar)
        {
            return new AvatarStandingReference {
                nativeClearance = Clearance(native), avatarClearance = Clearance(avatar)
            };
        }

        static float Clearance(Animator animator)
        {
            var skeleton = new Dictionary<string, SkeletonBone>();
            foreach (var bone in animator.avatar.humanDescription.skeleton)
                if (!skeleton.ContainsKey(bone.name)) skeleton.Add(bone.name, bone);
            var pose = new Dictionary<Transform, Matrix4x4>();
            pose.Add(animator.transform, animator.transform.localToWorldMatrix);
            var skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            var rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
            float sole = float.PositiveInfinity;
            foreach (var skin in skins)
            {
                var mesh = skin.sharedMesh;
                if (!skin.enabled || !skin.gameObject.activeInHierarchy || mesh == null || !mesh.isReadable) continue;
                var weights = mesh.boneWeights;
                if (weights.Length != mesh.vertexCount) continue;
                var bones = skin.bones;
                var footBones = new bool[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    for (var bone = bones[i]; bone != null && bone != animator.transform; bone = bone.parent)
                        if (bone == left || bone == right || bone == leftToes || bone == rightToes) { footBones[i] = true; break; }
                var selected = new List<int>();
                for (int i = 0; i < weights.Length; i++)
                {
                    var w = weights[i];
                    float influence = Foot(w.boneIndex0, w.weight0, footBones) + Foot(w.boneIndex1, w.weight1, footBones)
                        + Foot(w.boneIndex2, w.weight2, footBones) + Foot(w.boneIndex3, w.weight3, footBones);
                    if (influence >= .5f) selected.Add(i);
                }
                if (selected.Count == 0) continue;
                var vertices = mesh.vertices;
                for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                {
                    // Imported VRM sole shapes are normally already baked. Keep
                    // a live authored single-frame sole adjustment when present.
                    float weight = skin.GetBlendShapeWeight(shape);
                    if (weight == 0 || mesh.GetBlendShapeFrameCount(shape) != 1) continue;
                    float full = mesh.GetBlendShapeFrameWeight(shape, 0);
                    if (Mathf.Abs(full) < .0001f) continue;
                    var delta = new Vector3[vertices.Length];
                    mesh.GetBlendShapeFrameVertices(shape, 0, delta, null, null);
                    foreach (int i in selected) vertices[i] += delta[i] * (weight / full);
                }
                var binds = mesh.bindposes;
                var matrices = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length && i < binds.Length; i++)
                    if (bones[i] != null) matrices[i] = RestMatrix(bones[i], pose, skeleton) * binds[i];
                foreach (int i in selected)
                {
                    var w = weights[i];
                    float y = WeightedY(vertices[i], w.boneIndex0, w.weight0, matrices)
                        + WeightedY(vertices[i], w.boneIndex1, w.weight1, matrices)
                        + WeightedY(vertices[i], w.boneIndex2, w.weight2, matrices)
                        + WeightedY(vertices[i], w.boneIndex3, w.weight3, matrices);
                    sole = Mathf.Min(sole, y);
                }
            }
            if (float.IsPositiveInfinity(sole))
                sole = Mathf.Min(RestMatrix(left != null ? left : hips, pose, skeleton).m13, RestMatrix(right != null ? right : hips, pose, skeleton).m13);
            float clearance = RestMatrix(hips, pose, skeleton).m13 - sole;
            return clearance / Mathf.Max(Mathf.Abs(animator.transform.lossyScale.y), .0001f);
        }

        static float Foot(int index, float weight, bool[] bones)
        {
            return weight > 0 && index >= 0 && index < bones.Length && bones[index] ? weight : 0;
        }

        static float WeightedY(Vector3 vertex, int index, float weight, Matrix4x4[] matrices)
        {
            return weight > 0 && index >= 0 && index < matrices.Length ? matrices[index].MultiplyPoint3x4(vertex).y * weight : 0;
        }

        static Matrix4x4 RestMatrix(Transform bone, Dictionary<Transform, Matrix4x4> pose, Dictionary<string, SkeletonBone> skeleton)
        {
            if (pose.TryGetValue(bone, out var matrix)) return matrix;
            var local = skeleton.TryGetValue(bone.name, out var rest)
                ? Matrix4x4.TRS(rest.position, rest.rotation, rest.scale)
                : Matrix4x4.TRS(bone.localPosition, bone.localRotation, bone.localScale);
            matrix = bone.parent == null ? bone.localToWorldMatrix : RestMatrix(bone.parent, pose, skeleton) * local;
            pose.Add(bone, matrix);
            return matrix;
        }
    }
}
