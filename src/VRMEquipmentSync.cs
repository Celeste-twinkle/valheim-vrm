using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
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
            internal Transform Mount, SourceBone, TargetBone;
            internal Vector3 OriginalPosition, TargetPosition;
            internal Quaternion OriginalRotation, TargetRotation;
            internal bool HasPalm;
            internal float FallbackScale;

            internal void Apply()
            {
                if (Mount == null || SourceBone == null || TargetBone == null) return;
                Mount.position = HasPalm ? TargetBone.TransformPoint(TargetPosition) :
                    TargetBone.position + SourceBone.TransformVector(TargetPosition) * FallbackScale;
                Mount.rotation = (HasPalm ? TargetBone.rotation : SourceBone.rotation) * TargetRotation;
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

        readonly List<Grip> grips = new List<Grip>(10);
        Animator target;
        Transform characterFrame;
        VisEquipment equipment;
        Settings.VrmSettingsContainer settings;
        AvatarCalibrationOptions.Profile profile;
        HeldItem leftItem, rightItem, leftBackItem, rightBackItem;
        float heightScale = 1;
        static readonly FieldInfo LeftInstance = AccessTools.Field(typeof(VisEquipment), "m_leftItemInstance");
        static readonly FieldInfo RightInstance = AccessTools.Field(typeof(VisEquipment), "m_rightItemInstance");
        static readonly FieldInfo LeftHash = AccessTools.Field(typeof(VisEquipment), "m_leftItem");
        static readonly FieldInfo RightHash = AccessTools.Field(typeof(VisEquipment), "m_rightItem");
        static readonly FieldInfo LeftBackInstance = AccessTools.Field(typeof(VisEquipment), "m_leftBackItemInstance");
        static readonly FieldInfo RightBackInstance = AccessTools.Field(typeof(VisEquipment), "m_rightBackItemInstance");

        public void Setup(Animator original, Animator avatar, VisEquipment equipment, Settings.VrmSettingsContainer settings = null)
        {
            ResetAttachments();
            if (original == null || avatar == null || equipment == null) return;
            target = avatar;
            // Match VRMAnimationSync's pose offsets, including VRM 0.x imports
            // with an axis-correction rotation on their visual root.
            characterFrame = original.transform;
            this.equipment = equipment;
            this.settings = settings ?? new Settings.VrmSettingsContainer();
            profile = avatar.GetComponent<AvatarCalibrationBinding>()?.Profile ?? AvatarCalibrationOptions.Current.Get(this.settings.Name);
            heightScale = (avatar.GetComponent<AvatarScale>()?.TargetHeight ?? AvatarScale.DefaultHeight) / AvatarScale.DefaultHeight;
            var sourcePose = GetBindPose(original);
            var targetPose = GetBindPose(avatar);
            AddGrip(original, avatar, equipment.m_leftHand, true, sourcePose, targetPose);
            AddGrip(original, avatar, equipment.m_rightHand, false, sourcePose, targetPose);
            foreach (var mount in new[] { equipment.m_helmet, equipment.m_backShield,
                equipment.m_backMelee, equipment.m_backTwohandedMelee, equipment.m_backBow,
                equipment.m_backTool, equipment.m_backAtgeir })
                AddBodyMount(original, avatar, mount);
        }

        void AddBodyMount(Animator original, Animator avatar, Transform mount)
        {
            if (!SafeMount(original, mount)) return;
            foreach (var grip in grips) if (grip.Mount == mount) return;
            // Move only the equipment socket. Native humanoid bones must retain
            // their animated positions for grounding, gameplay and other mods.
            for (var parent = mount.parent; parent != null; parent = parent.parent)
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    if (original.GetBoneTransform((HumanBodyBones)i) != parent) continue;
                    var destination = avatar.GetBoneTransform((HumanBodyBones)i);
                    if (destination == null) continue;
                    grips.Add(new Grip {
                        Mount = mount, SourceBone = parent, TargetBone = destination,
                        OriginalPosition = mount.localPosition, OriginalRotation = mount.localRotation,
                        TargetPosition = parent.InverseTransformPoint(mount.position),
                        TargetRotation = Quaternion.Inverse(parent.rotation) * mount.rotation,
                        FallbackScale = 1
                    });
                    return;
                }
        }

        static bool SafeMount(Animator original, Transform mount)
        {
            if (mount == null || !mount.IsChildOf(original.transform)) return false;
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = original.GetBoneTransform((HumanBodyBones)i);
                if (bone != null && (bone == mount || bone.IsChildOf(mount))) return false;
            }
            return true;
        }

        void AddGrip(Animator original, Animator avatar, Transform mount, bool left,
            Dictionary<Transform, Matrix4x4> sourcePose, Dictionary<Transform, Matrix4x4> targetPose)
        {
            var bone = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var sourceHand = original.GetBoneTransform(bone);
            var targetHand = avatar.GetBoneTransform(bone);
            if (!SafeMount(original, mount) || sourceHand == null || targetHand == null || !mount.IsChildOf(sourceHand)) return;
            var point = sourceHand.InverseTransformPoint(mount.position);
            var rotation = Quaternion.Inverse(sourceHand.rotation) * mount.rotation;
            var grip = new Grip {
                Mount = mount, SourceBone = sourceHand, TargetBone = targetHand,
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
            if (target == null || characterFrame == null || equipment == null || !target.gameObject.activeInHierarchy) { ResetAttachments(); return; }
            foreach (var grip in grips) grip.Apply();
            UpdateItem(ref leftItem, true);
            UpdateItem(ref rightItem, false);
            UpdateBackItem(ref leftBackItem, LeftBackInstance.GetValue(equipment) as GameObject);
            UpdateBackItem(ref rightBackItem, RightBackInstance.GetValue(equipment) as GameObject);
        }

        void UpdateBackItem(ref HeldItem held, GameObject instance)
        {
            var mount = instance != null ? instance.transform.parent : null;
            if (held != null && (instance == null || held.Item != instance.transform || held.Mount != mount))
            { held.Restore(); held = null; }
            if (mount == null || !(mount == equipment.m_backShield || mount == equipment.m_backMelee ||
                mount == equipment.m_backTwohandedMelee || mount == equipment.m_backBow ||
                mount == equipment.m_backTool || mount == equipment.m_backAtgeir)) return;
            if (held == null) held = new HeldItem(instance.transform, mount, profile.Back);
            held.Apply(heightScale, settings.EquipmentScale, Vector3.zero, characterFrame.rotation);
        }

        void UpdateItem(ref HeldItem held, bool left)
        {
            var instance = (left ? LeftInstance : RightInstance).GetValue(equipment) as GameObject;
            var mount = left ? equipment.m_leftHand : equipment.m_rightHand;
            if (held != null && (instance == null || held.Item != instance.transform || held.Mount != mount))
            { held.Restore(); held = null; }
            // Skin-bound gear is not a hand socket and must not scale native bones.
            if (instance == null || mount == null || instance.transform.parent != mount) return;
            if (held == null)
            {
                int hash = (int)(left ? LeftHash : RightHash).GetValue(equipment);
                var data = ObjectDB.instance?.GetItemPrefab(hash)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                held = new HeldItem(instance.transform, mount,
                    data != null && IsTwoHanded(data.m_itemType) ? profile.TwoHanded : left ? profile.Left : profile.Right);
            }
            held.Apply(heightScale, settings.EquipmentScale, left ? settings.LeftHandItemPos : settings.RightHandItemPos, characterFrame.rotation);
        }

        public static bool IsTwoHanded(ItemDrop.ItemData.ItemType type) =>
            type == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
            type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || type == ItemDrop.ItemData.ItemType.Bow;

        sealed class HeldItem
        {
            internal readonly Transform Item, Mount;
            readonly Vector3 position, scale;
            readonly Quaternion rotation;
            readonly AvatarCalibrationOptions.Equipment options;
            internal HeldItem(Transform item, Transform mount, AvatarCalibrationOptions.Equipment options)
            {
                Item = item; Mount = mount; this.options = options;
                position = item.localPosition; rotation = item.localRotation; scale = item.localScale;
            }
            internal void Apply(float height, float legacyScale, Vector3 legacyPosition, Quaternion characterRotation)
            {
                if (float.IsNaN(legacyScale) || float.IsInfinity(legacyScale) || legacyScale <= 0) legacyScale = 1;
                float factor = height * options.Multiplier * legacyScale;
                Item.localScale = scale * factor;
                Item.localRotation = rotation;
                Item.localPosition = position * factor;
                Item.position += characterRotation * (legacyPosition + options.Position.Value);
            }
            internal void Restore()
            {
                if (Item == null || Item.parent != Mount) return;
                Item.localPosition = position; Item.localRotation = rotation; Item.localScale = scale;
            }
        }

        public void ResetAttachments()
        {
            leftItem?.Restore(); rightItem?.Restore();
            leftBackItem?.Restore(); rightBackItem?.Restore();
            leftItem = rightItem = null;
            leftBackItem = rightBackItem = null;
            foreach (var grip in grips) grip.Restore();
            grips.Clear();
            target = null; characterFrame = null; equipment = null;
        }

        void OnDisable() { ResetAttachments(); }
        void OnDestroy() { ResetAttachments(); }
    }
}
