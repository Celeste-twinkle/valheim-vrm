# Valheim VRM 管理器开发

`manager/` 是 ValheimVRM 仓库下的 WPF 管理器项目。日常开发在 `main`，发布时用 `codex/public-release` 保存对应的公开源码。使用方法见[中文指南](../docs/MANAGER.zh-CN.md)。

## 构建

在装有 .NET SDK 的 Windows 上，从仓库根目录运行以下命令。

```powershell
.\manager\Build.ps1 -ValheimPath "D:\SteamLibrary\steamapps\common\Valheim"
```

`-ValheimPath` 指向有 BepInEx 5 的 Valheim 客户端，提供编译引用。可用 `-BepInExPackage` 指定本地原版 BepInEx 5.4.23.3 Windows x64 ZIP。构建输出到 `release/`，也可传 `-OutputDirectory` 指定其他目录。

## 准备 Release

提交本次版本的源码和[版本日志](../release-notes.md)。日志标题使用 `## 版本号 - 标题`，版本号与 [ValheimVRM.csproj](../ValheimVRM.csproj) 一致。用以下命令准备发布包。

```powershell
.\manager\Release.ps1 -ValheimPath "D:\SteamLibrary\steamapps\common\Valheim"
```

命令在 `artifacts/releases/` 下生成带提交号的目录，包含管理器 EXE、客户端和服务端 ZIP、本次提交的源码 ZIP、随附组件源码、`SHA256SUMS.txt`、当前版本的 `RELEASE-NOTES.md` 和本地构建日志 `build.log`。按命令输出的源码提交校对发布标签。

手动将发布源码同步到 `codex/public-release`，给日志中的提交打标签，再在 GitHub 创建 Release。将 `RELEASE-NOTES.md` 的内容填入 Release 正文，上传 EXE、ZIP 和 `SHA256SUMS.txt`。仓库中的 `main` 推送工作流负责构建。
