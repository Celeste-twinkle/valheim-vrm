using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRM
{
    // Imported templates own the meshes, textures and avatars used by their
    // clones. Visibility/culling is not an ownership signal.
    public sealed class AvatarResidency : MonoBehaviour
    {
        internal const float IdleSeconds = 15f;
        sealed class Entry
        {
            public readonly HashSet<AvatarResourceLease> Instances = new HashSet<AvatarResourceLease>();
            public int PendingAttachments;
            public float IdleSince = Time.realtimeSinceStartup;
        }
        static readonly Dictionary<VRM, Entry> entries = new Dictionary<VRM, Entry>();
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
                entry.IdleSince = Time.realtimeSinceStartup;
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
            entries[model].Instances.Add(lease);
        }

        internal static void Release(AvatarResourceLease lease)
        {
            if (lease.Model != null && entries.TryGetValue(lease.Model, out var entry))
            {
                entry.Instances.Remove(lease);
                entry.IdleSince = Time.realtimeSinceStartup;
            }
            lease.Model = null;
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextSweep) return;
            nextSweep = now + 1f;
            Collect(now);
        }

        internal static void Collect(float now)
        {
            foreach (var pair in new List<KeyValuePair<VRM, Entry>>(entries))
            {
                var model = pair.Key;
                var entry = pair.Value;
                entry.Instances.RemoveWhere(instance => instance == null);
                if (entry.PendingAttachments != 0 || entry.Instances.Count != 0)
                {
                    entry.IdleSince = now;
                    continue;
                }
                if (now - entry.IdleSince < IdleSeconds) continue;
                // A same-name reload can leave the old template in use by a
                // different player. Only remove the matching cache generation.
                if (VrmManager.VrmDic.TryGetValue(model.Name, out var current) && ReferenceEquals(current, model))
                {
                    VrmManager.VrmDic.Remove(model.Name);
                    VrmManager.VrmHashes.Remove(model.Name);
                }
                entries.Remove(model);
                model.Dispose();
                Debug.Log("[ValheimVRM] Released unused avatar resources: " + model.Name);
            }
        }
    }

    public sealed class AvatarResourceLease : MonoBehaviour
    {
        [NonSerialized] internal VRM Model;
        void OnDestroy() { AvatarResidency.Release(this); }
    }
}
