using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace ValheimVRM
{
    public enum AvatarContactKind { Feet, Ground, Seat, Head, Hands, Reclining, Overlay }

    public sealed class AvatarAnimationEntry
    {
        public string Layer, Path;
        public string[] Clips = new string[0];
        [JsonIgnore] public int LayerIndex, Hash;
        [JsonIgnore] public string Name => Path.Substring(Path.LastIndexOf('.') + 1);
        [JsonIgnore] public AvatarContactKind Contact => LayerIndex > 0 ? AvatarContactKind.Overlay : Classify(Name);
        public string ClipKey(string clip) => Path + "/clip/" + clip;
        static AvatarContactKind Classify(string name)
        {
            switch (name)
            {
                case "Emote_sit": case "sit down": case "standup 0":
                case "Emote_kneel": case "Kneel Loop": return AvatarContactKind.Ground;
                case "SitChair": case "SitThrone": case "SitShip": case "SitDivan":
                case "RideLox": case "RideAsksvin": case "RideMoose": return AvatarContactKind.Seat;
                case "In Water": return AvatarContactKind.Head;
                case "HoldMast": case "HoldDragon": case "Grappling": case "Repell": return AvatarContactKind.Hands;
                case "Sleeping": case "Laydown": case "standup":
                case "Laydown Relax": case "Laydown Rest": case "Relax Loop": case "Rest Loop":
                    return AvatarContactKind.Reclining;
                default: return AvatarContactKind.Feet;
            }
        }
    }

    // Unity's player API exposes clips and HasState, but no state enumeration.
    // Extracted names are validated against the running controller. New/modded
    // states are discovered on entry and still receive a stable fallback.
    public sealed class AvatarAnimationCatalog
    {
        static readonly MethodInfo StateName = typeof(Animator).GetMethod("GetAnimatorStateName", BindingFlags.Instance | BindingFlags.NonPublic);
        static AvatarAnimationEntry[] definitions;
        readonly Dictionary<long, AvatarAnimationEntry> byHash = new Dictionary<long, AvatarAnimationEntry>();
        readonly List<AvatarAnimationEntry> entries = new List<AvatarAnimationEntry>();
        public IReadOnlyList<AvatarAnimationEntry> Entries => entries;
        static long Key(int layer, int hash) => ((long)layer << 32) | (uint)hash;

        public AvatarAnimationCatalog(Animator animator)
        {
            if (definitions == null)
                using (var stream = typeof(AvatarAnimationCatalog).Assembly.GetManifestResourceStream("ValheimVRM.player-animation-catalog.json"))
                using (var reader = new StreamReader(stream))
                    definitions = JsonConvert.DeserializeObject<AvatarAnimationEntry[]>(reader.ReadToEnd());
            var clips = new HashSet<string>((animator.runtimeAnimatorController?.animationClips ?? new AnimationClip[0]).Select(c => c.name));
            foreach (var definition in definitions)
            {
                int layer = animator.GetLayerIndex(definition.Layer);
                int hash = Animator.StringToHash(definition.Path);
                if (layer < 0 || !animator.HasState(layer, hash)) continue;
                Add(new AvatarAnimationEntry { Layer = definition.Layer, Path = definition.Path,
                    LayerIndex = layer, Hash = hash, Clips = definition.Clips.Where(clips.Contains).ToArray() });
            }
            entries.Sort((a, b) => { int layer = a.LayerIndex.CompareTo(b.LayerIndex); return layer != 0 ? layer : string.Compare(a.Path, b.Path, StringComparison.Ordinal); });
        }
        void Add(AvatarAnimationEntry entry) { byHash[Key(entry.LayerIndex, entry.Hash)] = entry; entries.Add(entry); }
        public AvatarAnimationEntry Find(int layer, int hash) => byHash.TryGetValue(Key(layer, hash), out var entry) ? entry : null;
        public AvatarAnimationEntry Resolve(Animator animator, int layer, AnimatorStateInfo state, bool next)
        {
            if (state.fullPathHash == 0) return null;
            var entry = Find(layer, state.fullPathHash);
            if (entry != null) return entry;
            string name = null;
            try { name = StateName?.Invoke(animator, new object[] { layer, !next }) as string; } catch { }
            if (string.IsNullOrEmpty(name)) name = "State " + state.fullPathHash;
            string layerName = animator.GetLayerName(layer);
            if (!name.StartsWith(layerName + ".", StringComparison.Ordinal)) name = layerName + "." + name;
            var clipInfo = next ? animator.GetNextAnimatorClipInfo(layer) : animator.GetCurrentAnimatorClipInfo(layer);
            entry = new AvatarAnimationEntry { Layer = layerName, Path = name, LayerIndex = layer, Hash = state.fullPathHash,
                Clips = clipInfo.Select(c => c.clip.name).Distinct().ToArray() };
            Add(entry);
            return entry;
        }
    }
}
