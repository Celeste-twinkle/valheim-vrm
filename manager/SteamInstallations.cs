using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ValheimVRM.Manager
{
    internal static class SteamInstallations
    {
        private static readonly Regex Pair = new Regex(
            @"^\s*""(?<key>[^""\r\n]+)""[ \t]+""(?<value>(?:\\.|[^""\\\r\n])*)""",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        internal static GameInstallation Find(TargetKind target)
        {
            var id = target == TargetKind.Client ? "892970" : "896660";
            var defaultName = target == TargetKind.Client ? "Valheim" : "Valheim dedicated server";
            var installed = GameInstallation.FindAt(target,
                ReadRegistry(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App " + id,
                    "InstallLocation"));
            if (installed != null)
                return installed;

            var roots = new[]
            {
                ReadRegistry(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                ReadRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                ReadRegistry(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
            };
            foreach (var root in roots.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var library in Libraries(root).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var apps = Path.Combine(library, "steamapps");
                    string name = Entries(Path.Combine(apps, "appmanifest_" + id + ".acf"))
                        .FirstOrDefault(entry => entry.Key.Equals("installdir", StringComparison.OrdinalIgnoreCase)).Value;
                    if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." ||
                        name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        name = defaultName;

                    var installation = GameInstallation.FindAt(target, Path.Combine(apps, "common", name))
                        ?? GameInstallation.FindAt(target, Path.Combine(apps, "common", defaultName));
                    if (installation != null)
                        return installation;
                }
            }
            return null;
        }

        internal static IEnumerable<string> Libraries(string root)
        {
            yield return root;
            foreach (var entry in Entries(Path.Combine(root, "steamapps", "libraryfolders.vdf")))
            {
                // Older Steam versions stored library paths directly under numbered keys.
                if ((entry.Key.Equals("path", StringComparison.OrdinalIgnoreCase) || int.TryParse(entry.Key, out _)) &&
                    !string.IsNullOrWhiteSpace(entry.Value) && Path.IsPathRooted(entry.Value))
                    yield return entry.Value;
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> Entries(string path)
        {
            string content;
            try { content = File.ReadAllText(path); }
            catch (IOException) { yield break; }
            catch (UnauthorizedAccessException) { yield break; }

            foreach (Match match in Pair.Matches(content))
            {
                var value = match.Groups["value"].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                if (value.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                    yield return new KeyValuePair<string, string>(match.Groups["key"].Value, value);
            }
        }

        private static string ReadRegistry(RegistryKey root, string key, string name)
        {
            try
            {
                using (var entry = root.OpenSubKey(key))
                    return entry?.GetValue(name) as string;
            }
            catch (SecurityException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }
}
