# ValheimVRM — Celeste-twinkle fork

[English](README.md) | **简体中文**

[下载编译版](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) · [发布版源码](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/public-release) · [详细安装说明](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.zh-CN.md)

适用于 Windows x64 客户端，已在英灵神殿 1.0.7 上验证。本 fork 将
[上游 PR #53](https://github.com/nyaarium/valheim-vrm/pull/53) 中的兼容性修复与游戏内模型选择菜单、
可选渲染控制整合为独立发布版。请从本仓库的 Release 页面下载 `ValheimVRM-1.7.2.zip`。

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

菜单改变的是本机显示的外观，没有新增多人外观实时同步功能，因此不能保证其他玩家看到当前选择。

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
```

编译输出为 `release/ValheimVRM-1.7.2.zip`。
除非显式传入 `-p:InstallToGame=true`，否则编译不会自动将插件安装到游戏中。
模型目录测试使用 .NET 7 和临时文件，插件目标框架为 .NET Framework 4.7.1。
着色器源码及重建说明见
[shaders/README.md](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/shaders/README.md)。
分发插件必须使用完整构建以嵌入渲染资源，仅执行 `-t:Compile` 不足以生成可分发的 DLL。

验证范围见[运行时依赖来源](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/Libs/README.md)、
[兼容性验证](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/valheim-1.0-validation.md)和
[发布版验证记录](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.7.2-validation.md)。
此版本已在 Windows／D3D11 下验证；Linux、macOS、Vulkan 和多人模型分享尚未在此版本中验证。
