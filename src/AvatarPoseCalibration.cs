using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ValheimVRM
{
    // Measurements are made once on an isolated native skeleton at a fixed phase.
    // The cached vectors are hip-relative, in unscaled reference coordinates.
    // Playback only scales those original vectors and blends states; it never
    // samples feet, feeds corrected hips back, or integrates a previous offset.
    internal sealed class AvatarPoseCalibration
    {
        struct Reference
        {
            public Vector3 Source, Target;
            public Vector3 Offset(Animator source, Animator target) =>
                Source * Mathf.Abs(source.transform.lossyScale.y) - Target * Mathf.Abs(target.transform.lossyScale.y);
        }
        readonly Dictionary<int, Reference> references = new Dictionary<int, Reference>();
        readonly AvatarStandingReference standing;
        public int Count => references.Count;
        public AvatarPoseCalibration(Animator native, Animator avatar, AvatarAnimationCatalog catalog, HumanPoseHandler targetPose)
        {
            standing = AvatarStandingReference.Measure(native, avatar);
            var saved = new TransformSnapshot(avatar.transform);
            var probeRoot = new GameObject("VRM posture calibration") { hideFlags = HideFlags.HideAndDontSave };
            probeRoot.SetActive(false);
            HumanPoseHandler sourcePose = null;
            try
            {
                var map = new Dictionary<Transform, Transform> { [native.transform] = probeRoot.transform };
                probeRoot.transform.localScale = native.transform.lossyScale;
                CopyChildren(native.transform, probeRoot.transform, map);
                foreach (var source in native.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!source.enabled || !source.gameObject.activeInHierarchy || source.sharedMesh == null) continue;
                    var skin = map[source.transform].gameObject.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = source.sharedMesh; skin.forceRenderingOff = true;
                    var bones = source.bones;
                    var copy = new Transform[bones.Length];
                    for (int i = 0; i < bones.Length; i++) if (bones[i] != null) map.TryGetValue(bones[i], out copy[i]);
                    skin.bones = copy;
                    if (source.rootBone != null && map.TryGetValue(source.rootBone, out var root)) skin.rootBone = root;
                    for (int i = 0; i < source.sharedMesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
                }
                var probe = probeRoot.AddComponent<Animator>();
                probe.avatar = native.avatar; probe.runtimeAnimatorController = native.runtimeAnimatorController;
                probe.applyRootMotion = false; probe.fireEvents = false; probe.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                probeRoot.SetActive(true); probe.Rebind(); probe.Update(0);
                sourcePose = new HumanPoseHandler(probe.avatar, probe.transform);
                // Normalize axes only for the measurement. Every target transform is
                // restored verbatim before returning, including non-humanoid bones.
                avatar.transform.position = Vector3.zero; avatar.transform.rotation = Quaternion.identity;
                var targetContact = new AvatarGroundContact(avatar);
                var sourceContact = new AvatarGroundContact(probe);
                var pose = new HumanPose();
                foreach (var entry in catalog.Entries)
                {
                    if (entry.Contact == AvatarContactKind.Feet || entry.Contact == AvatarContactKind.Overlay)
                    { references[entry.Hash] = default(Reference); continue; }
                    probe.Rebind();
                    int sample = entry.Hash;
                    // Entry/exit clips inherit the stable support of their resting
                    // posture instead of measuring a half-completed sit or lie down.
                    string reference = ReferenceState(entry.Name);
                    if (reference != null && probe.HasState(0, Animator.StringToHash(reference))) sample = Animator.StringToHash(reference);
                    probe.Play(sample, entry.LayerIndex, .5f);
                    probe.Update(0);
                    sourcePose.GetHumanPose(ref pose);
                    targetPose.SetHumanPose(ref pose);
                    var sourceHip = probe.GetBoneTransform(HumanBodyBones.Hips);
                    var targetHip = avatar.GetBoneTransform(HumanBodyBones.Hips);
                    targetHip.position = sourceHip.position;
                    Vector3 sourcePoint, targetPoint;
                    if (entry.Contact == AvatarContactKind.Hands)
                    {
                        sourcePoint = Hands(probe); targetPoint = Hands(avatar);
                    }
                    else if (entry.Contact == AvatarContactKind.Head)
                    {
                        sourcePoint = Bone(probe, HumanBodyBones.Head); targetPoint = Bone(avatar, HumanBodyBones.Head);
                    }
                    else
                    {
                        sourceContact.Sample(out _, out float sourceSeat, out float sourceLower);
                        targetContact.Sample(out _, out float targetSeat, out float targetLower);
                        float support = entry.Contact == AvatarContactKind.Ground ? probe.transform.position.y :
                            entry.Contact == AvatarContactKind.Seat ? sourceSeat : sourceLower;
                        float contact = entry.Contact == AvatarContactKind.Seat ? targetSeat : targetLower;
                        sourcePoint = new Vector3(sourceHip.position.x, support, sourceHip.position.z);
                        targetPoint = new Vector3(targetHip.position.x, contact, targetHip.position.z);
                    }
                    references[entry.Hash] = new Reference {
                        Source = (sourcePoint - sourceHip.position) / Mathf.Max(Mathf.Abs(probe.transform.lossyScale.y), .0001f),
                        Target = (targetPoint - targetHip.position) / Mathf.Max(Mathf.Abs(avatar.transform.lossyScale.y), .0001f)
                    };
                }
            }
            finally
            {
                saved.Restore();
                sourcePose?.Dispose();
                probeRoot.SetActive(false); Object.Destroy(probeRoot);
            }
        }
        static string ReferenceState(string name)
        {
            switch (name)
            {
                case "sit down": case "standup 0": return "Base Layer.Emote_sit";
                case "Emote_kneel": return "Base Layer.Emotes.Kneel Loop";
                case "Laydown": case "standup": return "Base Layer.Sleeping";
                case "Laydown Relax": return "Base Layer.Emotes.Relax Loop";
                case "Laydown Rest": return "Base Layer.Emotes.Rest Loop";
                default: return null;
            }
        }
        public Vector3 Offset(AvatarAnimationEntry entry, Animator source, Animator target)
        {
            if (entry != null && entry.Contact == AvatarContactKind.Overlay) return Vector3.zero;
            if (entry == null || entry.Contact == AvatarContactKind.Feet || !references.TryGetValue(entry.Hash, out var reference))
                return Vector3.up * standing.Offset(source, target);
            var value = reference.Offset(source, target);
            return Finite(value.x) && Finite(value.y) && Finite(value.z) ? value : Vector3.zero;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static Vector3 Bone(Animator animator, HumanBodyBones bone) =>
            (animator.GetBoneTransform(bone) ?? animator.GetBoneTransform(HumanBodyBones.Hips)).position;
        static Vector3 Hands(Animator animator) => (Bone(animator, HumanBodyBones.LeftHand) + Bone(animator, HumanBodyBones.RightHand)) * .5f;
        static void CopyChildren(Transform source, Transform target, Dictionary<Transform, Transform> map)
        {
            foreach (Transform child in source)
            {
                var copy = new GameObject(child.name).transform; copy.SetParent(target, false);
                copy.localPosition = child.localPosition; copy.localRotation = child.localRotation; copy.localScale = child.localScale;
                copy.gameObject.SetActive(child.gameObject.activeSelf); map[child] = copy;
                CopyChildren(child, copy, map);
            }
        }
        sealed class TransformSnapshot
        {
            readonly Transform[] bones;
            readonly Vector3[] positions, scales;
            readonly Quaternion[] rotations;
            public TransformSnapshot(Transform root)
            {
                bones = root.GetComponentsInChildren<Transform>(true);
                positions = new Vector3[bones.Length]; scales = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++) { positions[i] = bones[i].localPosition; scales[i] = bones[i].localScale; rotations[i] = bones[i].localRotation; }
            }
            public void Restore()
            {
                for (int i = 0; i < bones.Length; i++) if (bones[i] != null)
                { bones[i].localPosition = positions[i]; bones[i].localRotation = rotations[i]; bones[i].localScale = scales[i]; }
            }
        }
    }
}
