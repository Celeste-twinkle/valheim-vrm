# Valheim VRM 管理器

**Windows 10/11 x64** · [下载便携 EXE](https://github.com/Celeste-twinkle/valheim-vrm/releases/download/v2.0.7-fork.1/ValheimVRM-Manager-2.0.7-win-x64.exe)

1. 退出游戏或停止专用服务器，双击打开管理器。它会搜索 Steam 库并分别记住客户端和专用服务器目录。SteamCMD 等自定义目录可以直接填写，也可以点 **浏览**。
2. 在 **客户端** 中点击 **客户端模组** 旁边的 **安装**。通过游戏内“启动服务器”开房的玩家，可在同一客户端目录再装 **联机同步服务端**。Windows 专用服务器切换到 **专用服务器** 后安装同步组件。
3. 把一个或多个 `.vrm` 拖进客户端页面，或点击 **添加模型**。列表对应游戏根目录的 `ValheimVRM` 文件夹。同名文件会跳过。点击模型旁的 **删除** 并确认即可移除它。游戏已开启时，在 **F8 → 刷新列表** 中查看变更。
4. 在对应组件旁点击 **卸载**。管理器保留模型、个人配置、BepInEx 和原有共享运行库。

EXE 随附两个模组及 [BepInEx 5 x64 加载器](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3)，支持离线安装。目录已有 BepInEx 5 时会沿用。如果游戏目录中已有来源不明且版本不一致的共享运行库，管理器会在写入前报告具体冲突路径。

## 构建

在装有 .NET SDK 的 Windows 上，从仓库根目录运行 `./manager/Build.ps1 -ValheimPath "D:\SteamLibrary\steamapps\common\Valheim"`。构建使用现有 Valheim 客户端和 BepInEx 5 的引用。可用 `-BepInExPackage` 指定本地原版 5.4.23.3 Windows x64 ZIP。EXE 和手动安装包输出到 `release`。本地发版命令见[管理器工程](../manager/README.md#准备-release)。
