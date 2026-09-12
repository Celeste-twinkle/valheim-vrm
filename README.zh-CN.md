# ValheimVRM — Celeste-twinkle fork

[English](README.md) | **简体中文**

[下载编译版](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) · [发布版源码](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/public-release) · [详细安装说明](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.zh-CN.md)

适用于 Windows x64 客户端，已在英灵神殿 1.0.12 上验证。本 fork 将
[上游 PR #53](https://github.com/nyaarium/valheim-vrm/pull/53) 中的兼容性修复与游戏内模型选择菜单、
可选渲染控制整合为独立发布版。请从本仓库的 Release 页面下载 `ValheimVRM-1.8.1.zip`。

## Fork 继承链

[yoship1639/ValheimVRM](https://github.com/yoship1639/ValheimVRM)
→ [aMidnightNova/ValheimVRM](https://github.com/aMidnightNova/ValheimVRM)
→ [nyaarium/valheim-vrm](https://github.com/nyaarium/valheim-vrm)
→ **[Celeste-twinkle/valheim-vrm](https://github.com/Celeste-twinkle/valheim-vrm)**

感谢原作者和各 fork 维护者的贡献。本仓库提供独立编译版本，遇到此版本的问题，请提交至
[本 fork 的 Issues](https://github.com/Celeste-twinkle/valheim-vrm/issues)。

## 安装

先单独安装 BepInEx 5，再将**完整的发布版 ZIP** 解压到 `valheim.exe` 所在文件夹。
安装包包含配套的 UniVRM 依赖，仅复制 `ValheimVRM.dll` 不足以完成安装。
安装或升级前请退出游戏，并确保只保留一份插件。

安装包不含角色模型。请将自己的 `.vrm` 文件直接放入游戏根目录的 `ValheimVRM` 文件夹：

```text
Valheim/
  valheim.exe
  BepInEx/plugins/ValheimVRM/ValheimVRM.dll
  BepInEx/plugins/ValheimVRM/UniVRM.shaders
  valheim_Data/Managed/             （ZIP 中的配套依赖）
  ValheimVRM/
    我的模型.vrm
    另一个模型.vrm
```

前置依赖、升级步骤和模型专用配置见
[中文安装说明](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.zh-CN.md)
或 [English installation guide](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.md)。

## 服务器端包使用

服务器同步为可选功能。需要联机同步时，从本仓库的
[Release](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) 下载对应包：

| 安装位置 | 安装包 | 使用方法 |
| --- | --- | --- |
| 每位玩家的客户端 | `ValheimVRM-1.8.1.zip` | 按上文安装完整客户端，并准备相同的 `ValheimVRM` 模型文件夹。 |
| 专用服务器 | `ValheimVRM-Server-1.8.1.zip` | 先安装 BepInEx 5，再解压到服务器程序所在目录，重启服务器。 |
| 通过游戏“启动服务器”的房主 | 客户端包 + 服务器端包 | 两个包都安装到房主的游戏目录；其他玩家只安装客户端包。 |

服务器插件的最终路径为
`BepInEx/plugins/ValheimVRM.Server/ValheimVRM.Server.dll`。
服务器不需要模型、UniVRM 依赖或客户端着色器；服务器端包不包含 BepInEx。
**未安装本 Mod 的玩家也可正常加入服务器**，他们看到原版角色且不参与外观同步。
服务器不强制客户端安装，不因缺少 Mod 拒绝连接或踢人。

各客户端的模型文件名（含大小写）和文件内容必须一致，建议统一分发模型及其
`settings_模型名.txt` 配置。进入世界后按 **F8**，保持
**服务器外观同步（服务器支持时）**勾选，确认显示**服务器同步已连接**，再点击模型。
例如 A 选模型 1、B 选模型 2，其他玩家看到的就是 A = 模型 1、B = 模型 2；
A 再切换只影响 A，同名玩家或使用相同模型也各自独立。

未安装服务器插件时自动使用本地模式，模型菜单仍可正常使用。
取消 F8 同步勾选会保留自己的本地模型、撤回公开选择，并恢复本机其他玩家的原版外观。
缺少模型或文件内容不一致时会提示并保留已有可用外观；统一文件后重启客户端。
服务器仅同步模型名称和文件指纹，不传输 VRM 文件。

首次运行后，服务器配置位于
`BepInEx/config/com.celestetwinkle.valheimvrm.server.cfg`，默认 `[Sync] Enabled = true`。
客户端 F8 同步开关保存在
`BepInEx/config/com.yoship1639.plugins.valheimvrm.cfg` 的 `[AvatarSync] Enabled`。
更多安装、重生和故障处理说明见[服务器同步说明](docs/SERVER-SYNC.md)。

## F8 人物外观菜单

1. 使用角色进入世界，关闭聊天、物品栏和其他菜单，然后按 **F8** 打开人物外观面板。
2. 滚动列表，点击模型即可切换。列表**没有固定的模型数量限制**，支持中文和带空格的文件名。
   不扫描子文件夹，包括 `Shared` 联机缓存目录。
3. 增删模型文件后点击**刷新列表**。如果替换了已有模型文件的内容，请重启游戏以清除模型缓存。
4. 按 **F8**、**Esc** 或点击**关闭**退出面板。

游戏语言为中文时，面板显示中文；其他语言使用英文。

模型选择按游戏角色保存在 `ValheimVRM/avatar_selections.json`，重启或死亡复活后自动恢复。
无效的模型文件会显示导入错误，并保留先前外观。切换外观会保留已装备物品及其属性；
原有模型配置仍可控制装备是否显示、武器位置、碰撞体尺寸和交互距离。

支持可选的服务器外观同步。服务器安装独立 **ValheimVRM.Server** 插件、各客户端
安装相同模型文件夹后，A 切换只会改变其他玩家眼中的 A，不影响 B 的选择。
没有服务器插件时保持本地模式。安装步骤见[服务器同步说明](docs/SERVER-SYNC.md)。

### 物理摆动权重

**物理摆动权重**滑块控制已导出的头发、衣服和身体弹簧物理：**0%** 不摆动，
**100%** 保持原始物理摆幅，默认 **50%**。兼容 VRM 1.0 和 VRM 0.x，拖动即时
生效，松开后保存到 `ValheimVRM/physics_options.json`，切换模型及重启后保留。
该设置不改写模型内的刚度、重力、阻尼或碰撞参数，也不会补造未导出的物理。

### 渲染开关

面板提供三个开关，作用于 **VRM 1.0 MToon 材质**：

| 开关 | 默认值 | 效果 |
| --- | --- | --- |
| 场景光照 | 开 | 跟随日光和局部灯光受光。关闭后显示基础色与自发光，不计算场景光照明暗。 |
| 接收阴影 | 开 | 接收阴影贴图产生的阴影。关闭后仍保留光照方向和点光源距离衰减，模型也仍可向场景投射阴影。 |
| 模型泛光 | 关 | 开启后允许模型表面产生泛光。关闭时保留场景、火焰和武器的泛光。 |

**本插件没有亮度上限、亮部压缩或亮度封顶选项。**

修改立即生效，并保存在 `ValheimVRM/rendering_options.json`，切换模型及死亡生成布娃娃后同样有效。
关闭场景光照时，接收阴影开关暂时不可操作，但会保留所选状态。
重新开启光照和接收阴影时，会恢复原始着色器及导出材质参数。

这些开关不改写 VRM 文件，也不修改游戏的全局图像设置。
VRM 0.x 仍可导入，但旧版 MToon／游戏材质、Standard 和第三方材质不受这三个开关控制。
其他物体产生的泛光仍可能覆盖到模型，游戏自身的其他屏幕空间后处理仍会生效。

如果 F8 无法打开菜单，请确认角色存活且已进入世界，关闭其他菜单，并检查 `BepInEx/LogOutput.log`。
在 `ValheimVRM/global_settings.txt` 中设置 `EnableAvatarPicker=false` 会禁用此菜单。

## 开发

发布版的渲染控制与模型菜单实现在
[`codex/public-release`](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/public-release) 分支。
默认 `main` 分支不包含这些实现，编译前请切换到发布分支。
[`codex/valheim-1.0-compatibility`](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/valheim-1.0-compatibility)
分支仅保留兼容性修复，作为上游 PR 的来源。

准备 .NET SDK、已安装的英灵神殿和 BepInEx，然后在仓库目录执行：

```powershell
git switch codex/public-release
$env:VALHEIM_INSTALL_PATH = 'C:\Games\Valheim'
powershell -NoProfile -File tools/Test-RuntimeDependencies.ps1 -ValheimPath $env:VALHEIM_INSTALL_PATH
dotnet build -c Release
dotnet run --project tests/AvatarCatalogTests
dotnet run --project tests/AvatarSyncTests -c Release
powershell -NoProfile -File tools/Build-ServerPackage.ps1 -ValheimPath $env:VALHEIM_INSTALL_PATH
```

编译输出为 `release/ValheimVRM-1.8.1.zip`。
服务器打包命令输出 `release/ValheimVRM-Server-1.8.1.zip`，应在客户端构建之后执行。
除非显式传入 `-p:InstallToGame=true`，否则编译不会自动将插件安装到游戏中。
模型目录测试使用 .NET 7 和临时文件，插件目标框架为 .NET Framework 4.7.1。
着色器源码及重建说明见
[shaders/README.md](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/shaders/README.md)。
分发插件必须使用完整构建以嵌入渲染资源，仅执行 `-t:Compile` 不足以生成可分发的 DLL。

验证范围见[运行时依赖来源](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/Libs/README.md)、
[兼容性验证](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/valheim-1.0-validation.md)和
[发布版验证记录](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.8.1-validation.md)。
此版本已在 Windows／D3D11 下完成受控引擎验证，包括实际 ZRpc 序列化、服务器处理逻辑
及两名角色的独立模型绑定。尚未完成真实 Steam／PlayFab 专用服务器联机验收，
Linux、macOS 和 Vulkan 未验证；完整范围见上述记录。
