# Valheim VRM 管理器开发

`manager/` 是 ValheimVRM 仓库下的 WPF 管理器项目。日常开发在 `main`，发布时用 `codex/public-release` 保存对应的公开源码。使用方法见[中文指南](../docs/MANAGER.zh-CN.md)。

## 构建

在装有 .NET SDK 的 Windows 上，从仓库根目录运行以下命令。

```powershell
.\manager\Build.ps1 -ValheimPath "D:\SteamLibrary\steamapps\common\Valheim"
```

`-ValheimPath` 指向有 BepInEx 5 的 Valheim 客户端，提供编译引用。可用 `-BepInExPackage` 指定本地原版 BepInEx 5.4.23.3 Windows x64 ZIP。构建输出到 `release/`，也可传 `-OutputDirectory` 指定其他目录。

## 打包

克隆仓库并准备上述构建环境后，即可在干净的 Git 检出中生成并校验发布包。版本号取自 [ValheimVRM.csproj](../ValheimVRM.csproj)。

```powershell
.\manager\Pack.ps1 -ValheimPath "D:\SteamLibrary\steamapps\common\Valheim"
```

命令使用本地源码和公开的依赖包，在 `artifacts/releases/` 下生成管理器 EXE、客户端和服务端 ZIP、本次提交的源码 ZIP、随附组件源码及 `SHA256SUMS.txt`。`build.log` 记录源码提交和构建结果。

## GitHub 发布

仓库维护者单独执行 GitHub 发布。以 `build.log` 中记录的源码提交为准，同步 `codex/public-release` 并打标签。根据上个发布标签以来的变更撰写 Release 正文，再上传 EXE、ZIP 和 `SHA256SUMS.txt`。仓库中的 `main` 推送工作流负责构建。
