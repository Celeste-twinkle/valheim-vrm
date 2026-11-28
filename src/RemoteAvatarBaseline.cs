using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ValheimVRM
{
    public sealed class RemoteAvatarBaseline : MonoBehaviour
    {
        struct RenderState { public bool Enabled, Off, UpdateOffscreen; }
        struct ItemState { public Vector3 Position, Scale; public Quaternion Rotation; }
        readonly Dictionary<Renderer, RenderState> renderers = new Dictionary<Renderer, RenderState>();
        readonly Dictionary<Transform, ItemState> items = new Dictionary<Transform, ItemState>();
        bool captured;
        Animator animator;
        AnimatorCullingMode culling;
        bool keepAnimatorState, automaticMass, automaticInertia;
        CapsuleCollider capsule;
        Rigidbody body;
        float capsuleHeight, capsuleRadius, interactionDistance;
        Vector3 capsuleCenter, centerOfMass, inertia;

        public void Capture()
        {
            var player = GetComponent<Player>();
            var visual = player.GetVisual();
            if (!captured)
            {
                captured = true;
                interactionDistance = player.m_maxInteractDistance;
                capsule = player.GetComponent<CapsuleCollider>();
                if (capsule != null) { capsuleHeight = capsule.height; capsuleRadius = capsule.radius; capsuleCenter = capsule.center; }
                body = player.GetComponent<Rigidbody>();
                if (body != null) { centerOfMass = body.centerOfMass; inertia = body.inertiaTensor; automaticMass = body.automaticCenterOfMass; automaticInertia = body.automaticInertiaTensor; }
                animator = visual != null ? visual.GetComponent<Animator>() : null;
                if (animator != null) { culling = animator.cullingMode; keepAnimatorState = animator.keepAnimatorStateOnDisable; }
            }
            foreach (var stale in renderers.Keys.Where(r => r == null).ToArray()) renderers.Remove(stale);
            foreach (var stale in items.Keys.Where(t => t == null).ToArray()) items.Remove(stale);
            if (visual != null)
                foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                    Remember(r);
            var equipment = player.GetComponentInChildren<VisEquipment>();
            if (equipment == null) return;
            foreach (var field in AccessTools.GetDeclaredFields(typeof(VisEquipment)).Where(f => f.Name.EndsWith("ItemInstance") || f.Name.EndsWith("ItemInstances")))
            {
                var value = field.GetValue(equipment);
                var list = value is GameObject one ? new[] { one } : value as IEnumerable<GameObject>;
                if (list == null) continue;
                foreach (var item in list)
                {
                    if (item == null) continue;
                    if (!items.ContainsKey(item.transform)) items.Add(item.transform, new ItemState {
                        Position = item.transform.localPosition, Rotation = item.transform.localRotation, Scale = item.transform.localScale });
                    foreach (var r in item.GetComponentsInChildren<Renderer>(true))
                        Remember(r);
                }
            }
        }
        void Remember(Renderer renderer)
        {
            if (!renderers.ContainsKey(renderer)) renderers.Add(renderer, new RenderState {
                Enabled = renderer.enabled, Off = renderer.forceRenderingOff,
                UpdateOffscreen = renderer is SkinnedMeshRenderer skin && skin.updateWhenOffscreen });
        }
        public static void Restore(Player player)
        {
            if (player == null || player.IsDead()) return;
            player.GetComponent<VRMEquipmentSync>()?.ResetAttachments();
            player.GetComponent<VRMEyePositionSync>()?.ResetEyePosition();
            if (VrmManager.PlayerToVrmInstance.TryGetValue(player, out var visual) && visual != null)
            { visual.SetActive(false); Destroy(visual); }
            VrmManager.PlayerToVrmInstance.Remove(player); VrmManager.PlayerToName.Remove(player);
            var controller = player.GetComponent<VrmController>();
            if (controller != null) { controller.visual = null; controller.ClearAvatarPhysics(); controller.DeactivateSizeGizmo(); }
            var baseline = player.GetComponent<RemoteAvatarBaseline>();
            if (baseline == null) return;
            foreach (var pair in baseline.renderers)
                if (pair.Key != null) {
                    pair.Key.enabled = pair.Value.Enabled; pair.Key.forceRenderingOff = pair.Value.Off;
                    if (pair.Key is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = pair.Value.UpdateOffscreen;
                }
            foreach (var pair in baseline.items)
                if (pair.Key != null)
                { pair.Key.localPosition = pair.Value.Position; pair.Key.localRotation = pair.Value.Rotation; pair.Key.localScale = pair.Value.Scale; }
            if (baseline.captured)
            {
                player.m_maxInteractDistance = baseline.interactionDistance;
                if (baseline.capsule != null) { baseline.capsule.height = baseline.capsuleHeight; baseline.capsule.radius = baseline.capsuleRadius; baseline.capsule.center = baseline.capsuleCenter; }
                if (baseline.body != null) {
                    baseline.body.centerOfMass = baseline.centerOfMass; baseline.body.inertiaTensor = baseline.inertia;
                    baseline.body.automaticCenterOfMass = baseline.automaticMass; baseline.body.automaticInertiaTensor = baseline.automaticInertia;
                }
                if (baseline.animator != null) { baseline.animator.cullingMode = baseline.culling; baseline.animator.keepAnimatorStateOnDisable = baseline.keepAnimatorState; }
            }
            // Retain the empty component so a same-frame re-selection can capture
            // fresh native state instead of reusing a component pending Destroy.
            baseline.renderers.Clear(); baseline.items.Clear(); baseline.captured = false;
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "UpdateLodgroup")]
    static class CaptureRemoteEquipment
    {
        static void Prefix(VisEquipment __instance)
        {
            var player = __instance.GetComponent<Player>();
            if (player != null && VrmManager.PlayerToVrmInstance.ContainsKey(player))
                player.GetComponent<RemoteAvatarBaseline>()?.Capture();
        }
    }
}
