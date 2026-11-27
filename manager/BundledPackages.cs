using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace ValheimVRM.Manager
{
    internal static class BundledPackages
    {
        private static readonly string[] RuntimeNames =
        {
            "AsyncImageLoader.Runtime.dll", "FastSpringBone10.dll", "FreeImage.dll", "MToon.dll",
            "SpringBoneJobs.dll", "UniGLTF.dll", "UniGLTF.UniUnlit.dll", "UniGLTF.Utils.dll",
            "UniHumanoid.dll", "VRM.dll", "VRM10.dll", "VRM10.MToon10.dll", "VrmLib.dll"
        };

        internal const string ClientPluginDirectory = GameInstallation.Plugins + "/ValheimVRM/";
        internal const string Shader = ClientPluginDirectory + "UniVRM.shaders";
        internal const string LoaderPreloader = "BepInEx/core/BepInEx.Preloader.dll";
        internal const string LoaderConfig = "doorstop_config.ini";
        internal const string LoaderProxy = "winhttp.dll";

        internal static bool Contains(ModComponent component)
        {
            return typeof(BundledPackages).Assembly.GetManifestResourceInfo(ResourceName(component.ToString())) != null;
        }

        private static string ResourceName(string name) => "Payload." + name + ".zip";

        internal static ZipArchive Open(string name)
        {
            var stream = typeof(BundledPackages).Assembly.GetManifestResourceStream(ResourceName(name))
                ?? throw new IOException("管理器缺少内置安装包，请使用 Release 中的完整管理器。");
            try { return new ZipArchive(stream); }
            catch { stream.Dispose(); throw; }
        }

        internal static Dictionary<string, ZipArchiveEntry> ModFiles(ZipArchive package, ModComponent component)
        {
            var plugin = GameInstallation.PluginFile(component);
            var required = new[] { plugin };
            if (component == ModComponent.Server)
                return ReadFiles(package, path => path.Equals(plugin, StringComparison.OrdinalIgnoreCase), required);

            return ReadFiles(package, IsClientFile, required.Append(Shader)
                .Concat(RuntimeNames.Select(name => GameInstallation.ClientManaged + name)));
        }

        internal static Dictionary<string, ZipArchiveEntry> LoaderFiles(ZipArchive package)
        {
            return ReadFiles(package, path =>
                path == LoaderConfig || path == LoaderProxy || path == ".doorstop_version" ||
                (path.StartsWith("BepInEx/core/", StringComparison.OrdinalIgnoreCase) &&
                    Path.GetDirectoryName(path).Equals(@"BepInEx\core", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)),
                new[] { LoaderConfig, LoaderProxy, GameInstallation.Loader, LoaderPreloader });
        }

        private static bool IsClientFile(string path)
        {
            if (IsRuntime(path))
                return true;
            if (!path.StartsWith(ClientPluginDirectory, StringComparison.OrdinalIgnoreCase))
                return false;

            var item = path.Substring(ClientPluginDirectory.Length);
            if (item.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
                return item.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                    item.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase);

            if (item.Contains("/"))
                return false;
            return item == "ValheimVRM.dll" || item == "UniVRM.shaders" || item == "LICENSE" ||
                item.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                item.EndsWith("-LICENSE.txt", StringComparison.OrdinalIgnoreCase) ||
                item.EndsWith(".txt.example", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRuntime(string path)
        {
            return path.StartsWith(GameInstallation.ClientManaged, StringComparison.OrdinalIgnoreCase) &&
                RuntimeNames.Contains(path.Substring(GameInstallation.ClientManaged.Length), StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, ZipArchiveEntry> ReadFiles(ZipArchive package, Func<string, bool> allowed,
            IEnumerable<string> required)
        {
            var files = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in package.Entries)
            {
                var path = entry.FullName.Replace('\\', '/');
                if (string.IsNullOrEmpty(entry.Name) || !allowed(path))
                    continue;
                if (files.ContainsKey(path))
                    throw new InvalidDataException("安装包文件重复：" + path);
                files.Add(path, entry);
            }

            foreach (var path in required)
            {
                if (!files.TryGetValue(path, out var entry) || entry.Length == 0)
                    throw new InvalidDataException("安装包缺少文件：" + path);
            }
            return files;
        }

        internal static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
                return Hash(stream);
        }

        internal static string Hash(Stream stream)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
