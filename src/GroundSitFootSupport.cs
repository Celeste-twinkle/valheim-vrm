using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRM
{
    // Cache extremal foot/sole vertices once while the avatar is in its rest pose.
    // Runtime grounding needs at most 104 point transforms, not mesh baking.
    internal sealed class GroundSitFootSupport
    {
        readonly Transform[] bones;
        readonly Vector3[][] contacts;

        public GroundSitFootSupport(Animator animator)
        {
            bones = new[] {
                animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                animator.GetBoneTransform(HumanBodyBones.LeftToes),
                animator.GetBoneTransform(HumanBodyBones.RightFoot),
                animator.GetBoneTransform(HumanBodyBones.RightToes)
            };
            contacts = new Vector3[bones.Length][];
            var directions = new List<Vector3>(26);
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        if (x != 0 || y != 0 || z != 0) directions.Add(new Vector3(x, y, z));
            var extremes = new Vector3[bones.Length, directions.Count];
            var distances = new float[bones.Length, directions.Count];
            var populated = new bool[bones.Length];
            var baked = new Mesh();
            try
            {
                foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    var mesh = renderer.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var weights = mesh.boneWeights;
                    var skinBones = renderer.bones;
                    if (weights.Length != mesh.vertexCount) continue;
                    renderer.BakeMesh(baked);
                    var vertices = baked.vertices;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        var weight = weights[v];
                        int index = weight.boneIndex0;
                        float influence = weight.weight0;
                        if (weight.weight1 > influence) { index = weight.boneIndex1; influence = weight.weight1; }
                        if (weight.weight2 > influence) { index = weight.boneIndex2; influence = weight.weight2; }
                        if (weight.weight3 > influence) { index = weight.boneIndex3; influence = weight.weight3; }
                        if (influence < .5f || index < 0 || index >= skinBones.Length || skinBones[index] == null) continue;
                        int foot = FindFoot(skinBones[index]);
                        if (foot < 0) continue;
                        var point = bones[foot].InverseTransformPoint(renderer.transform.TransformPoint(vertices[v]));
                        for (int d = 0; d < directions.Count; d++)
                        {
                            float distance = Vector3.Dot(point, directions[d]);
                            if (!populated[foot] || distance > distances[foot, d])
                            {
                                extremes[foot, d] = point;
                                distances[foot, d] = distance;
                            }
                        }
                        populated[foot] = true;
                    }
                }
            }
            finally { Object.Destroy(baked); }

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                if (!populated[i]) { contacts[i] = new[] { Vector3.zero }; continue; }
                var points = new List<Vector3>(26);
                for (int d = 0; d < directions.Count; d++)
                    if (!points.Contains(extremes[i, d])) points.Add(extremes[i, d]);
                contacts[i] = points.ToArray();
            }
        }

        int FindFoot(Transform bone)
        {
            // Prefer toes to their foot ancestor, so toe animation moves its own envelope.
            for (int i = bones.Length - 1; i >= 0; i--)
                if (bones[i] != null && (bone == bones[i] || bone.IsChildOf(bones[i]))) return i;
            return -1;
        }

        public float GetLift(float floorHeight)
        {
            float lowest = floorHeight;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null || contacts[i] == null) continue;
                foreach (var point in contacts[i]) lowest = Mathf.Min(lowest, bones[i].TransformPoint(point).y - .005f);
            }
            return floorHeight - lowest;
        }
    }
}
