using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ValheimVRM.Manager
{
    internal sealed class AvatarFile
    {
        public string Name { get; }
        public string Size { get; }

        internal AvatarFile(FileInfo file)
        {
            Name = file.Name;
            Size = file.Length >= 1024 * 1024
                ? $"{file.Length / (1024d * 1024):0.##} MB"
                : $"{file.Length / 1024d:0.##} KB";
        }
    }

    internal static class AvatarFiles
    {
        internal static IReadOnlyList<AvatarFile> List(GameInstallation game)
        {
            var directory = game.Resolve(GameInstallation.Models);
            if (!Directory.Exists(directory))
                return Array.Empty<AvatarFile>();
            return Directory.EnumerateFiles(directory)
                .Where(path => IsModel(path) && !GameInstallation.IsLink(path))
                .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .Select(path => new AvatarFile(new FileInfo(path))).ToArray();
        }

        internal static bool IsModel(string name)
        {
            return string.Equals(Path.GetExtension(name), ".vrm", StringComparison.OrdinalIgnoreCase);
        }

        internal static string Import(GameInstallation game, IEnumerable<string> paths)
        {
            using (var changes = new FileChanges(game))
            {
                int imported = 0;
                int skipped = 0;
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var source in paths)
                {
                    var name = Path.GetFileName(source);
                    if (!IsModel(name) || !File.Exists(source))
                    {
                        skipped++;
                        continue;
                    }
                    var relative = GameInstallation.Models + "/" + name;
                    if (!names.Add(name) || File.Exists(game.Resolve(relative)))
                    {
                        skipped++;
                        continue;
                    }
                    changes.Stage(relative, output =>
                    {
                        using (var input = File.OpenRead(source))
                            input.CopyTo(output);
                    }, newOnly: true);
                    imported++;
                }
                changes.Commit();
                var result = $"已添加 {imported} 个模型";
                return skipped > 0 ? $"{result}，跳过 {skipped} 个同名或不可用文件。" : result + "。";
            }
        }

        internal static void Delete(GameInstallation game, AvatarFile model)
        {
            using (var changes = new FileChanges(game))
            {
                changes.Remove(GameInstallation.Models + "/" + model.Name);
                changes.Commit();
            }
        }
    }
}
