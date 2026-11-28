using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ValheimVRM.Manager
{
    internal enum TargetKind
    {
        Client,
        DedicatedServer
    }

    internal enum ModComponent
    {
        Client,
        Server
    }

    internal sealed class GameInstallation
    {
        internal const string Plugins = "BepInEx/plugins";
        internal const string Loader = "BepInEx/core/BepInEx.dll";
        internal const string ClientManaged = "valheim_Data/Managed/";
        internal const string Models = "ValheimVRM";

        internal string Root { get; }
        internal TargetKind Kind { get; }
        private string Executable => Kind == TargetKind.Client ? "valheim" : "valheim_server";

        private GameInstallation(TargetKind kind, string root)
        {
            Kind = kind;
            Root = root;
        }

        internal static GameInstallation FindAt(TargetKind kind, string input)
        {
            var text = input?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                if (!Path.IsPathRooted(text) || Path.GetPathRoot(text).Length < 3)
                    return null;

                var root = Path.GetFullPath(text);
                if (File.Exists(root))
                    root = Path.GetDirectoryName(root);
                if (!root.Equals(Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase))
                    root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var target = new GameInstallation(kind, root);
                return File.Exists(Path.Combine(root, target.Executable + ".exe")) &&
                    File.Exists(Path.Combine(root, target.Executable + "_Data", "Managed", "assembly_valheim.dll"))
                    ? target : null;
            }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException ||
                error is IOException || error is UnauthorizedAccessException)
            {
                return null;
            }
        }

        // All mutations use paths under the selected game root. Do not follow
        // links to a separate mod profile or another installation.
        internal string Resolve(string relative)
        {
            var path = Root;
            foreach (var part in relative.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == ".." ||
                    part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal) ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new IOException("无效的相对路径：" + relative);

                path = Path.Combine(path, part);
                if (IsLink(path))
                    throw new IOException("无法管理链接目录或文件：" + path);
            }
            return path;
        }

        internal static bool IsLink(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        internal static string ComponentName(ModComponent component)
        {
            return component == ModComponent.Client ? "ValheimVRM" : "ValheimVRM.Server";
        }

        internal static string PluginFile(ModComponent component)
        {
            var name = ComponentName(component);
            return Plugins + "/" + name + "/" + name + ".dll";
        }

        internal IReadOnlyList<string> FindPlugins(ModComponent component)
        {
            var paths = new List<string>();
            string name = ComponentName(component) + ".dll";
            var root = Resolve(Plugins);
            if (!Directory.Exists(root))
                return paths;

            var directories = new Stack<string>();
            directories.Push(root);
            while (directories.Count > 0)
            {
                var directory = directories.Pop();
                paths.AddRange(Directory.EnumerateFiles(directory, name)
                    .Where(path => Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase))
                    .Select(path => path.Substring(Root.TrimEnd('\\').Length + 1).Replace('\\', '/')));
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (!IsLink(child))
                        directories.Push(child);
                }
            }
            return paths;
        }

        internal static Version ReadVersion(string path)
        {
            try { return AssemblyName.GetAssemblyName(path).Version; }
            catch (BadImageFormatException) { return null; }
            catch (FileLoadException) { return null; }
        }

        internal static string FormatVersion(Version version)
        {
            return version.ToString(version.Revision > 0 ? 4 : 3);
        }

        internal void RequireStopped()
        {
            foreach (var process in Process.GetProcessesByName(Executable))
            {
                using (process)
                {
                    string runningPath = null;
                    try { runningPath = process.MainModule?.FileName; }
                    catch (System.ComponentModel.Win32Exception) { }
                    catch (InvalidOperationException) { continue; }
                    if (runningPath == null ||
                        string.Equals(Path.GetDirectoryName(runningPath), Root, StringComparison.OrdinalIgnoreCase))
                        throw new IOException(Kind == TargetKind.Client ? "请先退出 Valheim。" : "请先停止 Valheim 专用服务器。");
                }
            }
        }
    }
}
