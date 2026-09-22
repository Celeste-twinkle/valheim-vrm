using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ValheimVRM
{
    // Personal part choices are stored per VRM name. Received choices remain on
    // the remote instance and never enter this file or this cache.
    public sealed class AvatarPartOptions
    {
        public sealed class State
        {
            public HashSet<string> Hidden = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> Shown = new HashSet<string>(StringComparer.Ordinal);
            public State Clone() => new State {
                Hidden = new HashSet<string>(Hidden, StringComparer.Ordinal),
                Shown = new HashSet<string>(Shown, StringComparer.Ordinal)
            };
        }
        sealed class StoredState { public string[] Hidden; public string[] Shown; }
        static AvatarPartOptions current;
        public static AvatarPartOptions Current => current ?? (current = Open(Settings.ConfigDir));
        readonly string path;
        Dictionary<string, State> states = new Dictionary<string, State>(StringComparer.Ordinal);

        public AvatarPartOptions(string directory) { path = Path.Combine(directory, "avatar_parts.json"); }
        static AvatarPartOptions Open(string directory)
        {
            var result = new AvatarPartOptions(directory);
            try { result.Load(); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[ValheimVRM] Cannot load avatar part settings: " + ex.Message); }
            return result;
        }
        public void Load()
        {
            if (!File.Exists(path)) return;
            var source = JsonConvert.DeserializeObject<Dictionary<string, StoredState>>(File.ReadAllText(path));
            var loaded = new Dictionary<string, State>(StringComparer.Ordinal);
            if (source != null)
                foreach (var pair in source)
                    if (!string.IsNullOrEmpty(pair.Key))
                    {
                        var state = new State {
                            Hidden = Valid(pair.Value?.Hidden), Shown = Valid(pair.Value?.Shown)
                        };
                        state.Shown.ExceptWith(state.Hidden);
                        if (state.Hidden.Count != 0 || state.Shown.Count != 0) loaded[pair.Key] = state;
                    }
            states = loaded;
        }
        static HashSet<string> Valid(IEnumerable<string> values) => new HashSet<string>(
            (values ?? Enumerable.Empty<string>()).Where(ValheimVRM.Sync.AvatarPartSync.ValidId), StringComparer.Ordinal);
        public State Get(string model) => model != null && states.TryGetValue(model, out var value) ? value.Clone() : new State();
        public void Set(string model, string id, bool visible, bool authoredVisible)
        {
            if (string.IsNullOrEmpty(model) || !ValheimVRM.Sync.AvatarPartSync.ValidId(id)) throw new ArgumentException("Invalid avatar part.");
            var updated = Copy();
            if (!updated.TryGetValue(model, out var state)) updated[model] = state = new State();
            state.Hidden.Remove(id); state.Shown.Remove(id);
            if (visible != authoredVisible) (visible ? state.Shown : state.Hidden).Add(id);
            if (state.Hidden.Count == 0 && state.Shown.Count == 0) updated.Remove(model);
            Save(updated);
        }
        public void SetAll(string model, IEnumerable<string> hidden, IEnumerable<string> shown)
        {
            if (string.IsNullOrEmpty(model)) throw new ArgumentException("Invalid avatar name.");
            var updated = Copy();
            var state = new State { Hidden = Valid(hidden), Shown = Valid(shown) };
            state.Shown.ExceptWith(state.Hidden);
            if (state.Hidden.Count == 0 && state.Shown.Count == 0) updated.Remove(model); else updated[model] = state;
            Save(updated);
        }
        Dictionary<string, State> Copy() => states.ToDictionary(p => p.Key, p => p.Value.Clone(), StringComparer.Ordinal);
        void Save(Dictionary<string, State> updated)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var serialized = updated.ToDictionary(p => p.Key, p => new StoredState {
                Hidden = p.Value.Hidden.OrderBy(v => v, StringComparer.Ordinal).ToArray(),
                Shown = p.Value.Shown.OrderBy(v => v, StringComparer.Ordinal).ToArray()
            }, StringComparer.Ordinal);
            var temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(serialized, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                states = updated;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
