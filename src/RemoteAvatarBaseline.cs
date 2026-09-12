using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ValheimVRM
{
    public sealed class RemoteAvatarBaseline : MonoBehaviour
    {
        struct RenderState { public bool Enabled, Off; }
        struct ItemState { public Vector3 Position, Scale; public Quaternion Rotation; }
        readonly Dictionary<Renderer, RenderState> renderers = new Dictionary<Renderer, RenderState>();
        readonly Dictionary<Transform, ItemState> items = new Dictionary<Transform, ItemState>();

        public void Capture()
        {
            var player = GetComponent<Player>();
            var visual = player.GetVisual();
            if (visual != null)
                foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                    if (!renderers.ContainsKey(r)) renderers.Add(r, new RenderState { Enabled = r.enabled, Off = r.forceRenderingOff });
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
                        if (!renderers.ContainsKey(r)) renderers.Add(r, new RenderState { Enabled = r.enabled, Off = r.forceRenderingOff });
                }
            }
        }
        public static void Restore(Player player)
        {
            if (player == null || player == Player.m_localPlayer || player.IsDead()) return;
            player.GetComponent<VRMEquipmentSync>()?.ResetAttachments();
            if (VrmManager.PlayerToVrmInstance.TryGetValue(player, out var visual) && visual != null)
            { visual.SetActive(false); Destroy(visual); }
            VrmManager.PlayerToVrmInstance.Remove(player); VrmManager.PlayerToName.Remove(player);
            var controller = player.GetComponent<VrmController>(); if (controller != null) controller.visual = null;
            var baseline = player.GetComponent<RemoteAvatarBaseline>();
            if (baseline == null) return;
            foreach (var pair in baseline.renderers)
                if (pair.Key != null) { pair.Key.enabled = pair.Value.Enabled; pair.Key.forceRenderingOff = pair.Value.Off; }
            foreach (var pair in baseline.items)
                if (pair.Key != null)
                { pair.Key.localPosition = pair.Value.Position; pair.Key.localRotation = pair.Value.Rotation; pair.Key.localScale = pair.Value.Scale; }
            Destroy(baseline);
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "UpdateLodgroup")]
    static class CaptureRemoteEquipment
    {
        static void Prefix(VisEquipment __instance) { __instance.GetComponent<Player>()?.GetComponent<RemoteAvatarBaseline>()?.Capture(); }
    }
}
