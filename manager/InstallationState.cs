using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ValheimVRM.Manager
{
    internal sealed class InstallationState
    {
        internal GameInstallation Game { get; }
        internal ComponentState Client { get; }
        internal ComponentState Server { get; }
        internal IReadOnlyList<AvatarFile> Models { get; }
        internal string LoaderStatus { get; }

        private InstallationState(GameInstallation game)
        {
            Game = game;
            string loader = game.Resolve(GameInstallation.Loader);
            var version = File.Exists(loader) ? GameInstallation.ReadVersion(loader) : null;
            LoaderStatus = version != null
                ? "BepInEx " + GameInstallation.FormatVersion(version)
                : File.Exists(loader) ? "BepInEx 无法识别" : "安装时添加 BepInEx 5";
            Server = new ComponentState(game, ModComponent.Server);
            if (game.Kind == TargetKind.Client)
            {
                Client = new ComponentState(game, ModComponent.Client);
                if (Client.Installed)
                    Models = AvatarFiles.List(game);
            }
        }

        internal static InstallationState Read(TargetKind target, string path)
        {
            var game = GameInstallation.FindAt(target, path);
            return game == null ? null : new InstallationState(game);
        }
    }

    internal sealed class ComponentState
    {
        internal bool Installed { get; }
        internal string Description { get; }

        internal ComponentState(GameInstallation game, ModComponent component)
        {
            var files = game.FindPlugins(component);
            Installed = files.Count > 0;
            if (!Installed)
            {
                Description = "未安装";
                return;
            }

            var standard = GameInstallation.PluginFile(component);
            var path = files.FirstOrDefault(file => string.Equals(file, standard, StringComparison.OrdinalIgnoreCase))
                ?? files[0];
            var version = GameInstallation.ReadVersion(game.Resolve(path));
            Description = version == null ? "已发现模组文件" : "已安装 " + GameInstallation.FormatVersion(version);
            if (files.Count > 1)
                Description += $" · {files.Count} 份";
        }
    }
}
