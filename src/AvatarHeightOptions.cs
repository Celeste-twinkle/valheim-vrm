using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using ValheimVRM.Sync;

namespace ValheimVRM
{
    // Personal character preferences are separate from shared model settings.
    // Remote heights arrive with selections and never enter this file/cache.
    public sealed class AvatarHeightOptions
    {
        readonly string path;
        Dictionary<string, float> heights = new Dictionary<string, float>(StringComparer.Ordinal);
        public AvatarHeightOptions(string directory) { path = Path.Combine(directory, "avatar_heights.json"); }
        public void Load()
        {
            if (!File.Exists(path)) return;
            var loaded = JsonConvert.DeserializeObject<Dictionary<string, float>>(File.ReadAllText(path));
            heights.Clear();
            if (loaded != null)
                foreach (var pair in loaded) heights[pair.Key] = AvatarHeightRules.Clamp(pair.Value);
        }
        public float Get(string character) => character != null && heights.TryGetValue(character, out var value)
            ? value : AvatarHeightRules.Default;
        public void Set(string character, float height)
        {
            if (string.IsNullOrEmpty(character)) throw new ArgumentException("Character name is missing.");
            var updated = new Dictionary<string, float>(heights, StringComparer.Ordinal) { [character] = AvatarHeightRules.Clamp(height) };
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(updated, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                heights = updated;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
