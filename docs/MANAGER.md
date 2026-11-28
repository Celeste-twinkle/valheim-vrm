# Valheim VRM Manager

**Windows 10/11 x64** · [Download the portable EXE](https://github.com/Celeste-twinkle/valheim-vrm/releases/download/v2.0.7-fork.1/ValheimVRM-Manager-2.0.7-win-x64.exe)

1. Close Valheim or stop the dedicated server. Run the manager. It detects Steam libraries and remembers the client and dedicated server paths independently. You can also type a path or choose it using **浏览**.
2. On **客户端**, click **安装** next to **客户端模组**. A player hosting through **Start server** can also install **联机同步服务端** in the same game directory. For a Windows dedicated server, switch to **专用服务器** and install the sync addon there.
3. Drag one or more `.vrm` files into the client view or select them with **添加模型**. The list shows files in the game root's `ValheimVRM` folder. Imports skip existing filenames. To remove a model, click **删除** and confirm. If the game is open, use **F8 → Refresh list** after changing the files.
4. Click **卸载** next to the component you want to remove. Models and settings stay in their folders. The manager keeps the shared BepInEx loader and any previously installed runtime libraries.

The EXE includes both mods and the [BepInEx 5 x64 loader](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3) for offline installation. It reuses an existing BepInEx 5 installation. If a shared runtime file differs from the bundled version and belongs to an existing installation, the manager reports the conflict before writing files.

## Build

From the repository root, run `./manager/Build.ps1 -ValheimPath "D:\SteamLibrary\steamapps\common\Valheim"` on Windows with the .NET SDK. The build needs the same Windows Valheim and BepInEx 5 references as the mod projects. Pass `-BepInExPackage` with the original 5.4.23.3 Windows x64 ZIP to reuse a local download. The script writes the EXE and the manual ZIPs to `release`. See the [manager project](../manager/README.md#打包) for the full packaging command.
