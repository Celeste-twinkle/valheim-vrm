using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ValheimVRM.Manager
{
    internal static class ModInstaller
    {
        private const string RuntimeLedger = "BepInEx/config/ValheimVRM.Manager/runtime-files.txt";

        internal static void Install(GameInstallation game, ModComponent component)
        {
            if (component == ModComponent.Client && game.Kind != TargetKind.Client)
                throw new IOException("请选择 Valheim 客户端的安装目录。");

            using (var changes = new FileChanges(game))
            using (var package = BundledPackages.Open(component.ToString()))
            {
                game.RequireStopped();
                StageLoader(game, changes);
                var files = BundledPackages.ModFiles(package, component);
                var plugin = GameInstallation.PluginFile(component);
                var previous = component == ModComponent.Client ? ReadRuntimeLedger(game) : null;
                var owned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    if (file.Key.Equals(plugin, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (BundledPackages.IsRuntime(file.Key))
                        StageRuntime(game, changes, file, previous, owned);
                    else
                        Stage(changes, file.Key, file.Value);
                }

                if (previous != null)
                {
                    foreach (var runtime in previous)
                    {
                        if (!owned.ContainsKey(runtime.Key) && !files.ContainsKey(runtime.Key) &&
                            HashMatches(game.Resolve(runtime.Key), runtime.Value))
                            changes.Remove(runtime.Key);
                    }
                    changes.Stage(RuntimeLedger, stream =>
                    {
                        using (var writer = new StreamWriter(stream))
                            foreach (var runtime in owned)
                                writer.WriteLine(runtime.Value + " " + runtime.Key);
                    });
                }

                foreach (var legacy in game.FindPlugins(component))
                {
                    if (!legacy.Equals(plugin, StringComparison.OrdinalIgnoreCase))
                        changes.Remove(legacy);
                }
                Stage(changes, plugin, files[plugin]);

                game.RequireStopped();
                changes.Commit();
            }
        }

        internal static void Uninstall(GameInstallation game, ModComponent component)
        {
            using (var changes = new FileChanges(game))
            {
                game.RequireStopped();
                foreach (var plugin in game.FindPlugins(component))
                    changes.Remove(plugin);

                if (component == ModComponent.Client)
                {
                    changes.Remove(BundledPackages.Shader);
                    foreach (var runtime in ReadRuntimeLedger(game))
                    {
                        if (HashMatches(game.Resolve(runtime.Key), runtime.Value))
                            changes.Remove(runtime.Key);
                    }

                    if (BundledPackages.Contains(component))
                    {
                        using (var package = BundledPackages.Open("Client"))
                        {
                            foreach (var file in BundledPackages.ModFiles(package, component))
                            {
                                if (!file.Key.StartsWith(BundledPackages.ClientPluginDirectory, StringComparison.OrdinalIgnoreCase) ||
                                    file.Key.Equals(BundledPackages.Shader, StringComparison.OrdinalIgnoreCase) ||
                                    file.Key.Equals(GameInstallation.PluginFile(component), StringComparison.OrdinalIgnoreCase))
                                    continue;

                                var path = game.Resolve(file.Key);
                                if (!File.Exists(path))
                                    continue;
                                using (var source = file.Value.Open())
                                    if (HashMatches(path, BundledPackages.Hash(source)))
                                        changes.Remove(file.Key);
                            }
                        }
                    }
                    changes.Remove(RuntimeLedger);
                }
                game.RequireStopped();
                changes.Commit();
            }
        }

        private static void StageLoader(GameInstallation game, FileChanges changes)
        {
            var core = game.Resolve(GameInstallation.Loader);
            if (File.Exists(core))
            {
                if (GameInstallation.ReadVersion(core)?.Major != 5)
                    throw new IOException("这个目录需要兼容的 BepInEx 5 加载器。");
                if (!File.Exists(game.Resolve(BundledPackages.LoaderPreloader)) ||
                    !File.Exists(game.Resolve(BundledPackages.LoaderConfig)) ||
                    !File.Exists(game.Resolve(BundledPackages.LoaderProxy)))
                    throw new IOException("现有 BepInEx 安装不完整，请先修复加载器。");
                return;
            }

            using (var package = BundledPackages.Open("Loader"))
            {
                foreach (var file in BundledPackages.LoaderFiles(package))
                {
                    var path = game.Resolve(file.Key);
                    if (!File.Exists(path))
                    {
                        Stage(changes, file.Key, file.Value, newOnly: true);
                        continue;
                    }
                    using (var source = file.Value.Open())
                        if (!HashMatches(path, BundledPackages.Hash(source)))
                            throw new IOException("已有加载器文件与内置版本冲突：" + file.Key);
                }
            }

            var assembly = typeof(ModInstaller).Assembly;
            foreach (var license in assembly.GetManifestResourceNames().Where(name => name.StartsWith("License.", StringComparison.Ordinal)))
            {
                var path = "BepInEx/core/ValheimVRM-Licenses/" + license.Substring("License.".Length);
                if (!File.Exists(game.Resolve(path)))
                {
                    changes.Stage(path, output =>
                    {
                        using (var source = assembly.GetManifestResourceStream(license))
                            source.CopyTo(output);
                    }, newOnly: true);
                }
            }
        }

        private static void StageRuntime(GameInstallation game, FileChanges changes,
            KeyValuePair<string, ZipArchiveEntry> file, Dictionary<string, string> previous, Dictionary<string, string> owned)
        {
            string expected;
            using (var source = file.Value.Open())
                expected = BundledPackages.Hash(source);

            var path = game.Resolve(file.Key);
            bool exists = File.Exists(path);
            bool wasInstalledByManager = previous.TryGetValue(file.Key, out var prior) && HashMatches(path, prior);
            if (!exists || wasInstalledByManager)
            {
                Stage(changes, file.Key, file.Value, newOnly: !exists);
                owned.Add(file.Key, expected);
            }
            else if (!HashMatches(path, expected))
            {
                throw new IOException("已有运行库与内置版本冲突：" + file.Key);
            }
        }

        private static Dictionary<string, string> ReadRuntimeLedger(GameInstallation game)
        {
            var owned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var path = game.Resolve(RuntimeLedger);
            if (!File.Exists(path))
                return owned;

            foreach (var line in File.ReadLines(path))
            {
                if (line.Length < 66 || line[64] != ' ')
                    continue;
                string relative = line.Substring(65);
                string hash = line.Substring(0, 64);
                if (BundledPackages.IsRuntime(relative) && hash.All(Uri.IsHexDigit))
                    owned[relative] = hash;
            }
            return owned;
        }

        private static bool HashMatches(string path, string hash)
        {
            return File.Exists(path) && BundledPackages.Hash(path).Equals(hash, StringComparison.OrdinalIgnoreCase);
        }

        private static void Stage(FileChanges changes, string path, ZipArchiveEntry entry, bool newOnly = false)
        {
            changes.Stage(path, output =>
            {
                using (var input = entry.Open())
                    input.CopyTo(output);
            }, newOnly);
        }
    }
}
