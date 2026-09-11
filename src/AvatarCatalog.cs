using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ValheimVRM
{
    // Only top-level, user-supplied files belong in the picker. Shared downloads
    // and paths from a selection file must never expand this directory boundary.
    public sealed class AvatarCatalog
    {
        readonly string directory;
        readonly Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> selections = new Dictionary<string, string>(StringComparer.Ordinal);

        public string[] Names { get; private set; } = new string[0];
        string SelectionPath => Path.Combine(directory, "avatar_selections.json");

        public AvatarCatalog(string directory)
        {
            this.directory = directory;
        }

        public void Refresh()
        {
            var files = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => string.Equals(Path.GetExtension(path), ".vrm", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray()
                : new string[0];
            paths.Clear();
            foreach (var path in files) paths[Path.GetFileNameWithoutExtension(path)] = path;
            Names = paths.Keys.ToArray();
        }

        public bool TryGetPath(string name, out string path)
        {
            path = null;
            return name != null && paths.TryGetValue(name, out path) && File.Exists(path);
        }

        public void LoadSelections()
        {
            if (File.Exists(SelectionPath))
            {
                selections = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(SelectionPath))
                    ?? new Dictionary<string, string>(StringComparer.Ordinal);
                return;
            }
            // Earlier local builds saved short outfit identifiers. Resolve only a
            // unique filename suffix so existing selections survive the upgrade.
            var legacyPath = Path.Combine(directory, "selected_models.json");
            if (!File.Exists(legacyPath)) return;
            var legacy = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(legacyPath));
            if (legacy == null) return;
            foreach (var pair in legacy)
            {
                if (string.IsNullOrEmpty(pair.Value)) continue;
                var matches = Names.Where(name => name == pair.Value || name.EndsWith("_" + pair.Value, StringComparison.Ordinal)).ToArray();
                if (matches.Length == 1) selections[pair.Key] = matches[0];
            }
        }

        public string Resolve(string characterName)
        {
            if (characterName != null && selections.TryGetValue(characterName, out var name) && TryGetPath(name, out _))
                return name;
            return characterName;
        }

        public void Select(string characterName, string name)
        {
            if (string.IsNullOrEmpty(characterName)) throw new ArgumentException("Character name is missing.");
            if (!TryGetPath(name, out _)) throw new FileNotFoundException("The selected VRM is no longer available.");
            var updated = new Dictionary<string, string>(selections, StringComparer.Ordinal) { [characterName] = name };
            Directory.CreateDirectory(directory);
            var temporary = SelectionPath + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(updated, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(SelectionPath)) File.Replace(temporary, SelectionPath, null);
                else File.Move(temporary, SelectionPath);
                selections = updated;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
