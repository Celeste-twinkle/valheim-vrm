# ValheimVRM — Celeste-twinkle fork

[English](README.md) | **简体中文**

[下载编译版](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) · [发布版源码](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/public-release) · [详细安装说明](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.zh-CN.md)

适用于 Windows x64 客户端，已在英灵神殿 1.0.12 上验证。本 fork 将
[上游 PR #53](https://github.com/nyaarium/valheim-vrm/pull/53) 中的兼容性修复与游戏内模型选择菜单、
可选渲染控制整合为独立发布版。请从本仓库的 Release 页面下载 `ValheimVRM-1.8.6.zip`。

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

**模型目录只需 `.vrm` 文件即可正常工作。** 不需要 `settings_*.txt`、JSON、
清单文件、固定的九个模型、默认模型或初始化脚本。进入世界后按 F8 选择即可。
无配置时保持模型原始缩放（1.0）、使用 MToon，并关闭旧版角色淡出效果。

选择模型、调整物理或渲染后，Mod 会在 `BepInEx/config/ValheimVRM` 中自动生成
对应的 `avatar_selections.json`、`physics_options.json`、`rendering_options.json`。
`settings_模型名.txt` 和 `global_settings.txt` 是可选手动配置，也只放在该配置目录中，
不会自动生成。整个配置目录不存在也能正常使用。
1.8.6 起不再读取或迁移模型目录中的旧配置，也不再读取 `selected_models.json`。
升级前可将需要保留的配置移到新位置，或直接使用默认值。
说明文档、许可证和配置示例放在 `BepInEx/plugins/ValheimVRM` 中。


## 服务器端包使用

服务器同步为可选功能。需要联机同步时，从本仓库的
[Release](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) 下载对应包：

| 安装位置 | 安装包 | 使用方法 |
| --- | --- | --- |
| 每位玩家的客户端 | `ValheimVRM-1.8.6.zip` | 按上文安装完整客户端，并准备相同的 `ValheimVRM` 模型文件夹。 |
| 专用服务器 | `ValheimVRM-Server-1.8.6.zip` | 先安装 BepInEx 5，再解压到服务器程序所在目录，重启服务器。 |
| 通过游戏“启动服务器”的房主 | 客户端包 + 服务器端包 | 两个包都安装到房主的游戏目录；其他玩家只安装客户端包。 |

服务器插件的最终路径为
`BepInEx/plugins/ValheimVRM.Server/ValheimVRM.Server.dll`。
服务器不需要模型、UniVRM 依赖或客户端着色器；服务器端包不包含 BepInEx。
**未安装本 Mod 的玩家也可正常加入服务器**，他们看到原版角色且不参与外观同步。
服务器不强制客户端安装，不因缺少 Mod 拒绝连接或踢人。

客户端与服务器的文件夹不同、客户端缺少或多出文件，都不会影响入服和正常游戏。
显示某位玩家的模型时，接收方需要该模型的同名文件（含大小写）及相同内容；
其他文件无需完全一致。只分发 `.vrm` 即可；若主动使用可选的模型配置，可另外统一 `BepInEx/config/ValheimVRM` 下的对应 TXT。进入世界后按 **F8**，保持
**服务器外观同步（服务器支持时）**勾选，确认显示**服务器同步已连接**，再点击模型。
例如 A 选模型 1、B 选模型 2，其他玩家看到的就是 A = 模型 1、B = 模型 2；
A 再切换只影响 A，同名玩家或使用相同模型也各自独立。
发送端与服务端均为 1.8.3 时，每条请求携带递增序号，迟到或重复请求不会覆盖最后选择。
F8 显示“请求顺序保护已启用”；旧版仍可联机，但需要双方升级才能启用此保护。

未安装服务器插件时自动使用本地模式，模型菜单仍可正常使用。
取消 F8 同步勾选会保留自己的本地模型、撤回公开选择，并恢复本机其他玩家的原版外观。
缺少、无法读取或内容不一致的模型会跳过，并保留该玩家已有可用外观，首次加载则显示原版角色。
未缓存模型在导入前校验文件指纹，失败不会阻塞其他玩家的模型切换。补齐或修正未导入的文件后，
按 F8“刷新列表”即可重试；替换已缓存的模型需要重启客户端。
服务器仅同步模型名称和文件指纹，不读取自己的模型文件夹，也不传输 VRM 文件。

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

模型选择按游戏角色保存在 `BepInEx/config/ValheimVRM/avatar_selections.json`，重启或死亡复活后自动恢复。
无效的模型文件会显示导入错误，并保留先前外观。切换外观会保留已装备物品及其属性；
原有模型配置仍可控制装备是否显示、武器位置、碰撞体尺寸和交互距离。

支持可选的服务器外观同步。服务器安装独立 **ValheimVRM.Server** 插件、各客户端
安装相同模型文件夹后，A 切换只会改变其他玩家眼中的 A，不影响 B 的选择。
没有服务器插件时保持本地模式。安装步骤见[服务器同步说明](docs/SERVER-SYNC.md)。

### 物理摆动权重

**物理摆动权重**滑块控制已导出的头发、衣服和身体弹簧物理：**0%** 不摆动，
**100%** 保持原始物理摆幅，默认 **50%**。兼容 VRM 1.0 和 VRM 0.x，拖动即时
生效，松开后保存到 `BepInEx/config/ValheimVRM/physics_options.json`，切换模型及重启后保留。
该设置不改写模型内的刚度、重力、阻尼或碰撞参数，也不会补造未导出的物理。

### 渲染开关

面板提供三个开关，作用于 **VRM 1.0 MToon 材质**：

| 开关 | 默认值 | 效果 |
| --- | --- | --- |
| 场景光照 | 开 | 跟随日光和局部灯光受光。关闭后显示基础色与自发光，不计算场景光照明暗。 |
| 接收阴影 | 开 | 接收阴影贴图产生的阴影。关闭后仍保留光照方向和点光源距离衰减，模型也仍可向场景投射阴影。 |
| 模型泛光 | 关 | 开启后允许模型表面产生泛光。关闭时保留场景、火焰和武器的泛光。 |

**1.8.5 起，加载时自动将 MToon10 的基础色和阴影色限制到下述亮度基准，无需在 F8 中调整。**

修改立即生效，并保存在 `BepInEx/config/ValheimVRM/rendering_options.json`，切换模型及死亡生成布娃娃后同样有效。
关闭场景光照时，接收阴影开关暂时不可操作，但会保留所选状态。
重新开启光照和接收阴影时，会恢复原始着色器，并保留导入时的亮度限制。

这些开关不改写 VRM 文件，也不修改游戏的全局图像设置。
VRM 0.x 仍可导入，但旧版 MToon／游戏材质、Standard 和第三方材质不受这三个开关控制。
其他物体产生的泛光仍可能覆盖到模型，游戏自身的其他屏幕空间后处理仍会生效。

### 模型亮度基准

当前已校准的 Shinano 系列和 KUMALY 2 模型使用以下**导出 VRM 1.0／MToon 材质数值**
作为亮度参考。这些由用户提供的模型不包含在公开发布的插件包内。

| 材质参数 | 当前校准基准 |
| --- | --- |
| 基础色（`pbrMetallicRoughness.baseColorFactor` 的 RGB） | 中性白色材质的各通道为 **0.45**；有颜色的材质可使用更低的通道值。透明度 Alpha 保持原值。 |
| 阴影色（`VRMC_materials_mtoon.shadeColorFactor` 的 RGB） | 中性色的各通道为 **0.2025**；有颜色的阴影可使用更低的通道值。 |

加载时，若某个颜色的最大线性 RGB 通道超过基准，程序会将该颜色的 RGB **整体按比例压低**：
基础色上限为 **0.45**，阴影色上限为 **0.2025**，两者独立处理。例如基础色
`(1, 0.5, 0.2)` 会变为 `(0.45, 0.225, 0.09)`，保持色彩比例与透明度 Alpha。
**等于或低于基准的颜色完全不动**；已校准的模型不会因再次加载、切换模型或渲染开关而继续变暗。
处理发生在 UniVRM 记录默认表情颜色之前，表情重置会恢复到处理后的基准。

较高的基础色数值在旧版客户端中可能造成皮肤、头发和衣服过曝发白；新版本会在导入时压低它。
自发光、反射、边缘高光、动画颜色覆盖、灯光和后处理仍会影响最终画面，因此这不是最终像素亮度封顶。
目前作用于 **VRM 1.0 MToon 材质**，本地角色与通过服务器同步加载的远端角色均适用；
VRM 0.x、Standard 和其他着色器保持原有处理方式。

基准对应 VRM 文件中的线性颜色系数，程序会处理与 Unity 材质 sRGB 颜色的转换。
原始 `.vrm` 文件、贴图及服务器同步使用的文件指纹均不改动。
这与 `settings_模型名.txt` 中的旧版 `ModelBrightness` 配置是独立的；
MToon10 不应用该配置，现有模型配置继续保持 `ModelBrightness=1` 即可。

如果 F8 无法打开菜单，请确认角色存活且已进入世界，关闭其他菜单，并检查 `BepInEx/LogOutput.log`。
在 `BepInEx/config/ValheimVRM/global_settings.txt` 中设置 `EnableAvatarPicker=false` 会禁用此菜单。

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

编译输出为 `release/ValheimVRM-1.8.6.zip`。
服务器打包命令输出 `release/ValheimVRM-Server-1.8.6.zip`，应在客户端构建之后执行。
除非显式传入 `-p:InstallToGame=true`，否则编译不会自动将插件安装到游戏中。
模型目录测试使用 .NET 7 和临时文件，插件目标框架为 .NET Framework 4.7.1。
着色器源码及重建说明见
[shaders/README.md](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/shaders/README.md)。
分发插件必须使用完整构建以嵌入渲染资源，仅执行 `-t:Compile` 不足以生成可分发的 DLL。

验证范围见[运行时依赖来源](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/Libs/README.md)、
[兼容性验证](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/valheim-1.0-validation.md)和
[发布版验证记录](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.8.6-validation.md)。
此版本已在 Windows／D3D11 下完成受控引擎验证，包括实际 ZRpc 序列化、服务器处理逻辑
及两名角色的独立模型绑定。尚未完成真实 Steam／PlayFab 专用服务器联机验收，
Linux、macOS 和 Vulkan 未验证；完整范围见上述记录。
