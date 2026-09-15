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
        public const string OriginalModel = "";
        readonly string directory;
        readonly string configurationDirectory;
        readonly Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, string> selections = new Dictionary<string, string>(StringComparer.Ordinal);

        public string[] Names { get; private set; } = new string[0];
        string SelectionPath => Path.Combine(configurationDirectory, "avatar_selections.json");

        public AvatarCatalog(string directory, string configurationDirectory)
        {
            this.directory = directory;
            this.configurationDirectory = configurationDirectory;
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
            var path = SelectionPath;
            if (File.Exists(path))
            {
                selections = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path))
                    ?? new Dictionary<string, string>(StringComparer.Ordinal);
                return;
            }
            selections = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public string Resolve(string characterName)
        {
            if (IsOriginalSelected(characterName)) return OriginalModel;
            if (characterName != null && selections.TryGetValue(characterName, out var name) && TryGetPath(name, out _))
                return name;
            return characterName;
        }

        public bool IsOriginalSelected(string characterName) => characterName != null &&
            selections.TryGetValue(characterName, out var name) && name == OriginalModel;

        public void Select(string characterName, string name)
        {
            if (string.IsNullOrEmpty(characterName)) throw new ArgumentException("Character name is missing.");
            if (name != OriginalModel && !TryGetPath(name, out _)) throw new FileNotFoundException("The selected VRM is no longer available.");
            var updated = new Dictionary<string, string>(selections, StringComparer.Ordinal) { [characterName] = name };
            Directory.CreateDirectory(configurationDirectory);
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
