using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRM
{
    // Cache visible skin contact vertices once. Evaluate their original blend weights
    // against the current bones: a dominant-bone envelope can over-lift bent feet.
    internal sealed class AvatarGroundContact
    {
        struct Point
        {
            public BoneWeight Weight;
            public Vector3 A, B, C, D;
            public bool Foot, Seat;
        }
        readonly Transform[] sampledBones;
        readonly Matrix4x4[] matrices;
        readonly Point[] points;
        readonly Transform leftFoot, rightFoot, hips;
        readonly Dictionary<Transform, HumanBodyBones> humanoid = new Dictionary<Transform, HumanBodyBones>();

        public AvatarGroundContact(Animator animator)
        {
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null) humanoid[bone] = (HumanBodyBones)i;
            }
            var baked = new Mesh();
            var contacts = new List<Point>();
            var boneIndices = new Dictionary<Transform, int>();
            var boneList = new List<Transform>();
            try
            {
                foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    var mesh = renderer.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var weights = mesh.boneWeights;
                    if (weights.Length != mesh.vertexCount) continue;
                    var bones = renderer.bones;
                    var inverse = new Matrix4x4[bones.Length];
                    var regions = new int[bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                    {
                        if (bones[i] == null) continue;
                        inverse[i] = bones[i].worldToLocalMatrix;
                        regions[i] = Classify(bones[i]);
                    }
                    // Unity 6 needs scale compensation before TransformPoint. The
                    // default overload applies renderer scale a second time here.
                    renderer.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    for (int i = 0; i < weights.Length; i++)
                    {
                        var w = weights[i];
                        if (!Valid(w.boneIndex0, w.weight0, bones) || !Valid(w.boneIndex1, w.weight1, bones) ||
                            !Valid(w.boneIndex2, w.weight2, bones) || !Valid(w.boneIndex3, w.weight3, bones)) continue;
                        float foot = Influence(w, regions, 1), seat = Influence(w, regions, 2);
                        float lower = foot + seat + Influence(w, regions, 3);
                        if (lower < .5f) continue;
                        var p = renderer.transform.TransformPoint(vertices[i]);
                        var mapped = w;
                        mapped.boneIndex0 = Index(w.boneIndex0, w.weight0, bones, boneIndices, boneList);
                        mapped.boneIndex1 = Index(w.boneIndex1, w.weight1, bones, boneIndices, boneList);
                        mapped.boneIndex2 = Index(w.boneIndex2, w.weight2, bones, boneIndices, boneList);
                        mapped.boneIndex3 = Index(w.boneIndex3, w.weight3, bones, boneIndices, boneList);
                        contacts.Add(new Point
                        {
                            Weight = mapped,
                            Foot = foot >= .5f,
                            Seat = seat >= .5f,
                            A = Local(p, w.boneIndex0, w.weight0, inverse),
                            B = Local(p, w.boneIndex1, w.weight1, inverse),
                            C = Local(p, w.boneIndex2, w.weight2, inverse),
                            D = Local(p, w.boneIndex3, w.weight3, inverse)
                        });
                    }
                }
            }
            finally { Object.Destroy(baked); }
            points = contacts.ToArray();
            sampledBones = boneList.ToArray();
            matrices = new Matrix4x4[sampledBones.Length];
            humanoid.Clear();
        }

        static int Index(int i, float weight, Transform[] bones, Dictionary<Transform, int> indices, List<Transform> list)
        {
            if (weight <= 0) return 0;
            if (!indices.TryGetValue(bones[i], out int mapped)) { mapped = list.Count; indices.Add(bones[i], mapped); list.Add(bones[i]); }
            return mapped;
        }

        static bool Valid(int index, float weight, Transform[] bones) => weight == 0 || (index >= 0 && index < bones.Length && bones[index] != null);
        static Vector3 Local(Vector3 point, int index, float weight, Matrix4x4[] inverse) => weight > 0 ? inverse[index].MultiplyPoint3x4(point) : Vector3.zero;
        static float Influence(BoneWeight w, int[] regions, int region)
        {
            float result = 0;
            if (w.weight0 > 0 && regions[w.boneIndex0] == region) result += w.weight0;
            if (w.weight1 > 0 && regions[w.boneIndex1] == region) result += w.weight1;
            if (w.weight2 > 0 && regions[w.boneIndex2] == region) result += w.weight2;
            if (w.weight3 > 0 && regions[w.boneIndex3] == region) result += w.weight3;
            return result;
        }
        int Classify(Transform bone)
        {
            while (bone != null)
            {
                if (humanoid.TryGetValue(bone, out var human))
                {
                    switch (human)
                    {
                        case HumanBodyBones.LeftFoot:
                        case HumanBodyBones.RightFoot:
                        case HumanBodyBones.LeftToes:
                        case HumanBodyBones.RightToes: return 1;
                        case HumanBodyBones.Hips: case HumanBodyBones.LeftUpperLeg: case HumanBodyBones.RightUpperLeg: return 2;
                        case HumanBodyBones.LeftLowerLeg: case HumanBodyBones.RightLowerLeg: return 3;
                        default: return 0;
                    }
                }
                bone = bone.parent;
            }
            return 0;
        }

        public void Sample(out float feet, out float seat, out float lowerBody)
        {
            feet = seat = lowerBody = float.PositiveInfinity;
            for (int i = 0; i < sampledBones.Length; i++)
                if (sampledBones[i] != null) matrices[i] = sampledBones[i].localToWorldMatrix;
            foreach (var p in points)
            {
                var w = p.Weight;
                float y = WorldY(p.A, w.boneIndex0, w.weight0, matrices)
                    + WorldY(p.B, w.boneIndex1, w.weight1, matrices)
                    + WorldY(p.C, w.boneIndex2, w.weight2, matrices)
                    + WorldY(p.D, w.boneIndex3, w.weight3, matrices);
                lowerBody = Mathf.Min(lowerBody, y);
                if (p.Foot) feet = Mathf.Min(feet, y);
                if (p.Seat) seat = Mathf.Min(seat, y);
            }
            if (float.IsPositiveInfinity(feet)) feet = Mathf.Min(leftFoot != null ? leftFoot.position.y : hips.position.y, rightFoot != null ? rightFoot.position.y : hips.position.y);
            if (float.IsPositiveInfinity(seat)) seat = hips.position.y;
            if (float.IsPositiveInfinity(lowerBody)) lowerBody = Mathf.Min(feet, seat);
        }
        static float WorldY(Vector3 p, int i, float weight, Matrix4x4[] matrices)
        {
            if (weight <= 0) return 0;
            var m = matrices[i];
            return (m.m10 * p.x + m.m11 * p.y + m.m12 * p.z + m.m13) * weight;
        }
    }
}
