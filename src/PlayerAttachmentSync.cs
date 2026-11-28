using HarmonyLib;
using UnityEngine;

namespace ValheimVRM
{
    [HarmonyPatch(typeof(ZSyncTransform), "OwnerSync")]
    internal static class Patch_PlayerAttachmentJoint
    {
        [HarmonyPostfix]
        static void Postfix(ZSyncTransform __instance, ZNetView ___m_nview, Character ___m_character,
            ZDOID ___m_tempParent, string ___m_tempAttachJoint)
        {
            if (!__instance.m_syncPosition || !__instance.m_characterParentSync ||
                !(___m_character is Player) || ___m_nview == null) return;
            var zdo = ___m_nview.GetZDO();
            if (zdo == null || !zdo.IsOwner() ||
                zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.SyncTransform) != ___m_tempParent) return;

            // The game only sends attachJoint when the parent ZDO changes. Standing
            // on a creature and then riding it keeps the same parent but changes
            // the joint from empty to the saddle; dismounting reverses that change.
            string joint = ___m_tempParent.IsNone() ? "" : ___m_tempAttachJoint ?? "";
            if (zdo.GetString(ZDOVars.s_attachJointHash) != joint)
                zdo.Set(ZDOVars.s_attachJointHash, joint);
        }
    }

    [HarmonyPatch(typeof(ZSyncTransform), "SyncPosition")]
    internal static class Patch_RemoteSaddlePosition
    {
        static readonly AccessTools.FieldRef<Character, Animator> NativeAnimator =
            AccessTools.FieldRefAccess<Character, Animator>("m_animator");

        [HarmonyPostfix]
        static void Postfix(ZSyncTransform __instance, ZDO zdo, Character ___m_character)
        {
            if (!__instance.m_characterParentSync || !(___m_character is Player player) ||
                zdo == null || zdo.IsOwner() || !zdo.HasOwner() ||
                zdo.GetString(ZDOVars.s_attachJointHash).Length != 0 || ZNetScene.instance == null) return;
            var parent = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.SyncTransform);
            if (parent.IsNone()) return;
            var mount = ZNetScene.instance.FindInstance(parent);
            var mountZdo = mount != null ? mount.GetComponent<ZNetView>()?.GetZDO() : null;
            if (mountZdo == null) return;
            long user = mountZdo.GetLong(ZDOVars.s_user, 0L);
            if (user == 0L || user != zdo.m_uid.UserID) return;
            var saddle = mount.GetComponentInChildren<Sadle>();
            if (saddle == null || !saddle.isActiveAndEnabled || saddle.m_attachPoint == null ||
                string.IsNullOrEmpty(saddle.m_attachAnimation)) return;
            var animator = NativeAnimator(player);
            // Remote Player.IsRiding()/GetAttachPoint() are local control state.
            // Require both the saddle's network user and the synchronized animation
            // before recovering a missing joint sent by an older rider client.
            if (animator == null || !animator.GetBool(saddle.m_attachAnimation)) return;

            // Native SyncPosition has already synchronized the mount and rotation.
            // Correct only this replica's position; never change received ZDO data.
            Vector3 position = saddle.m_attachPoint.position;
            if ((__instance.transform.position - position).sqrMagnitude > .000001f)
            {
                __instance.transform.position = position;
                Physics.SyncTransforms();
            }
        }
    }
}
