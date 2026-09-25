using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ValheimVRM.Sync;

namespace ValheimVRM
{
    // Imported templates own the meshes, textures and avatars used by their
    // clones. Visibility/culling is not an ownership signal.
    public sealed class AvatarResidency : MonoBehaviour
    {
        sealed class Entry
        {
            public readonly HashSet<AvatarResourceLease> Instances = new HashSet<AvatarResourceLease>();
            public int PendingAttachments;
            public bool WasBound;
            public float InitialAttachDeadline = Time.realtimeSinceStartup + 2f;
        }
        static readonly Dictionary<VRM, Entry> entries = new Dictionary<VRM, Entry>();
        static readonly Dictionary<string, string> remoteSelections = new Dictionary<string, string>(StringComparer.Ordinal);
        static string localSelection;
        static bool remoteSnapshotAvailable;
        float nextSweep;

        internal static void Track(VRM model)
        {
            if (!entries.ContainsKey(model)) entries.Add(model, new Entry());
        }

        sealed class Attachment : IDisposable
        {
            Entry entry;
            public Attachment(Entry value) { entry = value; entry.PendingAttachments++; }
            public void Dispose()
            {
                if (entry == null) return;
                entry.PendingAttachments--;
                entry = null;
            }
        }

        internal static IDisposable Acquire(VRM model)
        {
            Track(model);
            return new Attachment(entries[model]);
        }

        internal static void Bind(VRM model, GameObject instance)
        {
            Track(model);
            var lease = instance.AddComponent<AvatarResourceLease>();
            lease.Model = model;
            entries[model].WasBound = true;
            entries[model].Instances.Add(lease);
        }

        internal static void Release(AvatarResourceLease lease)
        {
            if (lease.Model != null && entries.TryGetValue(lease.Model, out var entry))
            {
                entry.Instances.Remove(lease);
            }
            lease.Model = null;
        }

        internal static void Attached(VRM model, Player player)
        {
            if (model == null || player == null) return;
            if (player == Player.m_localPlayer)
            {
                SetLocalSelection(model.Name);
                return;
            }
            if (!remoteSnapshotAvailable && TryGetCharacterKey(player, out var key) && !string.IsNullOrEmpty(model.Name))
                remoteSelections[key] = model.Name;
        }

        internal static void SetLocalSelection(string model)
        {
            localSelection = string.IsNullOrEmpty(model) || model == AvatarCatalog.OriginalModel ? null : model;
            Collect();
        }

        static string CharacterKey(long user, uint id) { return user + ":" + id; }

        static bool TryGetCharacterKey(Player player, out string key)
        {
            key = null;
            var zdo = player?.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null || zdo.m_uid.UserID == 0 || zdo.m_uid.ID == 0) return false;
            key = CharacterKey(zdo.m_uid.UserID, zdo.m_uid.ID);
            return true;
        }

        internal static void SetRemoteSelections(AvatarSelection[] selections)
        {
            remoteSelections.Clear();
            if (selections != null)
                foreach (var selection in selections)
                    if (selection != null && selection.CharacterUser != 0 && selection.CharacterId != 0 &&
                        !string.IsNullOrEmpty(selection.Model) && selection.Model != AvatarCatalog.OriginalModel)
                        remoteSelections[CharacterKey(selection.CharacterUser, selection.CharacterId)] = selection.Model;
            remoteSnapshotAvailable = true;
            Collect();
        }

        internal static void ClearRemoteSelections()
        {
            remoteSelections.Clear();
            remoteSnapshotAvailable = false;
            Collect();
        }

        internal static void LoseRemoteAuthority()
        {
            // A transient RPC loss cannot prove that a player disconnected.
            // Preserve the last known choices and validate them against the
            // game's connected-character list until a new snapshot arrives.
            remoteSnapshotAvailable = false;
        }

        static void PruneDisconnectedSelections()
        {
            if (remoteSnapshotAvailable || remoteSelections.Count == 0) return;
            var net = ZNet.instance;
            if (net == null) { remoteSelections.Clear(); return; }
            var online = new HashSet<string>(net.GetPlayerList()
                .Where(player => player.m_characterID.UserID != 0 && player.m_characterID.ID != 0)
                .Select(player => CharacterKey(player.m_characterID.UserID, player.m_characterID.ID)), StringComparer.Ordinal);
            foreach (var key in remoteSelections.Keys.Where(key => !online.Contains(key)).ToArray())
                remoteSelections.Remove(key);
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextSweep) return;
            nextSweep = now + 1f;
            Collect();
        }

        static bool IsCurrentGeneration(VRM model)
        {
            return model != null && VrmManager.VrmDic.TryGetValue(model.Name, out var current) &&
                ReferenceEquals(current, model);
        }

        static bool IsRetained(VRM model)
        {
            if (!IsCurrentGeneration(model)) return false;
            return model.Name == localSelection || remoteSelections.ContainsValue(model.Name);
        }

        internal static void Collect()
        {
            PruneDisconnectedSelections();
            foreach (var pair in new List<KeyValuePair<VRM, Entry>>(entries))
            {
                var model = pair.Key;
                var entry = pair.Value;
                entry.Instances.RemoveWhere(instance => instance == null);
                if (entry.PendingAttachments != 0 || entry.Instances.Count != 0 || IsRetained(model)) continue;
                // Registration and SetToPlayer are separate iterator steps.
                // Protect only a never-bound template long enough for its first
                // attachment to acquire a lease; formerly used, unreferenced
                // templates remain immediately collectible.
                if (!entry.WasBound && Time.realtimeSinceStartup < entry.InitialAttachDeadline) continue;
                // A same-name reload can leave the old template in use by a
                // different player. Only remove the matching cache generation.
                if (IsCurrentGeneration(model))
                {
                    VrmManager.VrmDic.Remove(model.Name);
                    VrmManager.VrmHashes.Remove(model.Name);
                }
                entries.Remove(model);
                model.Dispose();
                Debug.Log("[ValheimVRM] Released avatar resources after selection/player exit: " + model.Name);
            }
        }

        internal static void ClearAll()
        {
            var models = new HashSet<VRM>(entries.Keys);
            models.UnionWith(VrmManager.VrmDic.Values.Where(model => model != null));
            entries.Clear();
            VrmManager.VrmDic.Clear();
            VrmManager.VrmHashes.Clear();
            localSelection = null;
            remoteSelections.Clear();
            remoteSnapshotAvailable = false;
            foreach (var model in models) model.Dispose();
            if (models.Count != 0)
                Debug.Log("[ValheimVRM] Released all avatar resources while leaving the world: " + models.Count);
        }
    }

    public sealed class AvatarResourceLease : MonoBehaviour
    {
        [NonSerialized] internal VRM Model;
        void OnDestroy() { AvatarResidency.Release(this); }
    }

    [HarmonyPatch(typeof(ZNet), "Shutdown")]
    static class AvatarResidencyWorldShutdown
    {
        [HarmonyPostfix]
        static void Postfix() { AvatarResidency.ClearAll(); }
    }
}
