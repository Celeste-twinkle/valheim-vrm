# ValheimVRM — Celeste-twinkle fork

[English](README.md) | **简体中文**

将英灵神殿角色替换为自己的 VRM 人形模型，游戏内按 **F8** 切换。可以只在本机生效，也可以安装独立服务器插件，让其他玩家看到各自选择的模型。

**当前发布版：1.8.12** · 验证环境：Valheim **1.0.12**、Windows x64、Unity 6000.0.75f1、BepInEx **5.4.23.3**、D3D11。

[下载 Release](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) · [发布版源码](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/public-release) · [更新记录](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/release-notes.md)

[安装与升级](#安装与升级) · [模型与配置](#模型与配置文件) · [F8 菜单](#f8-人物外观菜单) · [身高](#模型身高调整) · [亮度](#模型亮度基准) · [服务器同步](#服务器同步) · [常见问题](#常见问题)

## 当前功能

- 支持导入 VRM 0.x／VRM 1.0 人形模型；模型列表可滚动，支持中文、空格文件名。
- 选择按游戏角色保存，切换后保留已装备物品及属性；持物挂点按模型手部骨骼适配。
- 提供物理摆动权重、场景光照、接收阴影和模型泛光控制。
- 模型身高 **1.4–2.2 米**可调，默认 **2 米**，可按玩家独立同步；加载时执行 MToon10 亮度基准上限。
- 可选服务器同步按玩家独立记录选择，并用递增请求序号处理乱序消息。
- 释放不再使用的模型资源；**1.8.7 修复退回主菜单时重复安装补丁造成的显存泄漏，以及场景卸载期间的模型挂接异常。**

公开 Release **不包含角色模型**。`.unitypackage`、FBX 和 VRChat 工程不能直接放进模型目录，需要先导出为 VRM；导出的物理、材质和骨骼决定可还原的效果。

## 安装与升级

### 选择安装包

| 使用场景 | 安装内容 |
| --- | --- |
| 玩家客户端，含单人、本地外观和联机外观 | BepInEx 5 + `ValheimVRM-1.8.12.zip` + 自备 `.vrm`。 |
| 专用服务器，需要同步玩家外观 | BepInEx 5 + `ValheimVRM-Server-1.8.12.zip`；无需模型或客户端依赖。 |
| 通过游戏“启动服务器”的房主，需要外观同步 | 在房主游戏目录安装客户端包和服务器包；其他玩家按客户端方式安装。 |
| 仅下载源码 | `ValheimVRM-1.8.12-source.zip` 用于开发，不能代替编译好的插件包。 |

公开客户端、服务器 ZIP 均不附带 BepInEx。前置加载器与依赖来源见[详细安装说明](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.zh-CN.md)。

### 前置依赖下载

| 前置 | 下载直达 | 安装说明 |
| --- | --- | --- |
| **BepInExPack Valheim（推荐）** | [Valheim 专用整合包](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) | 已配置 Valheim 所需的加载入口。本项目验证的整合包版本为 **5.4.2333**，BepInEx 核心版本为 **5.4.23.3**。 |
| BepInEx 上游 | [5.4.23.3 Release](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3) · [全部 Release](https://github.com/BepInEx/BepInEx/releases) | 上游下载及版本记录。Windows 使用 **BepInEx 5／x64／Mono**；上方专用整合包已包含游戏所需配置。 |

同一个游戏／服务器目录只需安装一份加载器。完整客户端 ZIP 已提供配套 UniVRM 运行库和着色器，无需另行下载；服务端不需要这些客户端依赖。

另外分发的**本地 Windows x64 完整包** `09_ValheimVRM_1.8.12_完整插件.zip` 和 `10_ValheimVRM_Server_1.8.12.zip` 均已包含 BepInEx **5.4.23.3**，首次安装无需再下载前置。安装前退出游戏／停止服务器。服务器已有兼容 BepInEx 时，只复制本地服务端 ZIP 中的 `BepInEx/plugins/ValheimVRM.Server`，保留原加载器和配置。

### 玩家安装步骤

1. 正常退出游戏。尚未安装 BepInEx 5 时先安装加载器，启动一次游戏后退出。
2. 将**完整客户端 ZIP**解压到 `valheim.exe` 所在目录，合并 `BepInEx` 和 `valheim_Data`。不能只复制 `ValheimVRM.dll`。
3. 在游戏根目录创建 `ValheimVRM`，把自己的 `.vrm` 直接放进去。一个模型就能使用。
4. 启动游戏，进入世界后关闭聊天、物品栏等菜单，按 **F8** 选择模型。
5. 在 `BepInEx/LogOutput.log` 中确认加载的是 **ValheimVRM 1.8.12**。

```text
Valheim/
  valheim.exe
  BepInEx/
    plugins/ValheimVRM/
      ValheimVRM.dll
      UniVRM.shaders
      README.zh-CN.md、配置示例、许可证等
    config/ValheimVRM/          （保存个人选项时按需生成）
  valheim_Data/Managed/         （完整 ZIP 提供的配套依赖）
  ValheimVRM/
    我的模型.vrm
    另一个模型.vrm
```

升级时，先退出游戏并备份需要保留的文件，检查 `BepInEx/plugins` 下只有一份 `ValheimVRM.dll`；旧 DLL 备份应移到插件扫描目录之外。覆盖完整新包，保留 `.vrm` 和 `BepInEx/config`。不要混用旧 UniVRM 依赖，也不要用旧 `Unity.Burst`／`Unity.Mathematics` 覆盖游戏自带 DLL。

从 1.8.5 或更早版本升级时，可手动把想保留的模型 TXT、全局 TXT 和下表三个 JSON 移到 `BepInEx/config/ValheimVRM`。**1.8.6 起不再读取或自动迁移模型目录中的旧配置，也不读取 `selected_models.json`。** 不迁移则使用默认值。

使用 r2modman 等独立配置档时，应把插件装到实际启用的配置档，配置也跟随该 BepInEx 配置档。模型目录取游戏进程工作目录下的 `ValheimVRM`，正常启动时位于游戏根目录；自定义启动脚本请把工作目录设为游戏根目录。卸载时退出游戏，移除本插件目录；保留自己的模型、配置和其他 Mod 使用的共享依赖。

## 模型与配置文件

**`ValheimVRM` 文件夹只放 `.vrm` 就能正常工作。** 不需要 TXT、JSON、清单、固定模型组合、`___Default.vrm` 或初始化脚本。不扫描子文件夹，包括旧 `Shared` 缓存目录。

下表路径均相对于游戏根目录；缺少可选配置或整个配置目录时，使用内置默认值。

| 文件／目录 | 用途 | 谁创建 |
| --- | --- | --- |
| `ValheimVRM/*.vrm` | 模型文件；至少准备一个供选择。 | 用户放入。 |
| `BepInEx/config/ValheimVRM/avatar_selections.json` | 各游戏角色的模型选择。 | Mod 保存选择时生成。 |
| `BepInEx/config/ValheimVRM/avatar_heights.json` | 各本机游戏角色的外观身高，默认 2 米。 | 应用身高滑块时由 Mod 保存。 |
| `BepInEx/config/ValheimVRM/physics_options.json` | 本机统一的物理摆动权重。 | Mod 保存滑块设置时生成。 |
| `BepInEx/config/ValheimVRM/rendering_options.json` | 本机渲染开关。 | Mod 修改选项时生成。 |
| `BepInEx/config/ValheimVRM/settings_模型名.txt` | 可选模型缩放、偏移、装备等参数。 | 保存 F8 高度微调时自动创建；其他参数可手动填写。 |
| `BepInEx/config/ValheimVRM/global_settings.txt` | 可选全局参数，如是否启用 F8 菜单。 | 用户手动创建，不会自动生成。 |
| `BepInEx/plugins/ValheimVRM/` | 插件、着色器、说明、示例和许可证。 | 解压插件包。 |

例如模型为 `MyAvatar.vrm`，可复制插件目录里的 `settings_Example.txt.example`，改名为 `settings_MyAvatar.txt` 并放到上述配置目录。也可新建纯文本文件，只写需要修改的参数：

```ini
ModelScale=1.0
ModelOffsetY=0
```

省略的参数使用默认值。打开资源管理器的“文件扩展名”，避免误存成 `.txt.txt` 或保留 `.example`。保存后重启游戏；F8“刷新列表”不重载已缓存的模型或模型配置。全部参数见[配置示例](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/settings_Example.txt.example)。

可选自动匹配仍支持 `游戏角色名.vrm` 和 `___Default.vrm`（三个下划线）；后者的配置名为 `settings____Default.txt`（四个下划线）。正常使用 F8 无需准备默认模型。

## F8 人物外观菜单

进入世界且角色存活时使用 F8；F8、Esc 或“关闭”退出。中文游戏显示中文，其余语言显示英文。

- 点击模型切换；选择按角色保存，重启和死亡复活后恢复。导入失败会提示错误并保留先前可用外观。
- 增删模型后点“刷新列表”。替换同名模型文件或修改 TXT 后重启游戏。
- 列表没有固定数量上限，也不会预加载整个文件夹；大型模型仍需要相应内存和显存。
- “服务器外观同步（服务器支持时）”控制是否公开自己的选择并接收其他玩家外观，详见下方同步说明。

### 站姿和坐姿高度微调

两个滑块默认均为 **0 厘米**。1.8.12 在加载时读取模型人形骨架的参考姿态、髋骨／脚骨位置、蒙皮绑定数据及鞋底厚度，计算一次站姿高度基准，并包含当前缩放。站着、走动或坐下时加载，都使用同一参考骨架标定。

站立、行走、奔跑使用固定的垂直偏移，保留原版动画自带的髋部起伏；不再跟踪每帧最低脚底顶点来抬升或降低整个人物。原版骨架保持只读。坐地、椅子及相关过渡仍有独立的接触处理。固定基准优先保证动画稳定，部分姿势可能有少量脚底穿入或离地，不提供逐脚 IK，也不按模型名称硬编码高度。

F8 的 **“站姿高度偏移”** 和 **“坐姿高度偏移”** 可分别调整 **−50～+50 厘米**，步长 1 厘米。正值抬高、负值降低；拖动即时预览，松开保存；点击“两项恢复为 0”回到自动定位。站姿偏移作用于通常的站立与移动；坐姿偏移作用于坐地、椅子、船上坐姿和坐地的坐下／起身过程。睡眠与死亡布娃娃保留原有定位。

微调按模型写入 `BepInEx/config/ValheimVRM/settings_模型名.txt` 的 `StandingHeightOffset`、`SittingHeightOffset`（单位米）。缺少文件时自动创建，保留其他参数和注释。微调只影响本机渲染，不通过服务器同步；原有 `ModelOffsetY` 仍会叠加全局偏移。滑块用于特殊鞋底、服装形状的补充校准；默认自动定位仍然生效，也不会为所有椅子自动增加屈膝 IK。

### 物理摆动权重

滑块范围 **0%～100%**，默认 **50%**。0% 不显示弹簧旋转，100% 保持导出的原始摆幅；拖动即时生效，松开保存，切换模型和重启后保留。

适用于 VRM 0.x 和 VRM 1.0 中已导出的头发、衣服和身体弹簧物理，不改写原始刚度、重力、阻尼和碰撞参数。它不能补造导出时缺失的物理，也不会直接运行 VRChat PhysBone 组件。身体与衣服穿模还取决于模型蒙皮、形态键和碰撞设置，降低权重不等于修复资产本身。

### 渲染开关

三个开关作用于 **VRM 1.0 MToon 材质**，保存后对本机显示的模型和死亡布娃娃生效：

| 开关 | 默认值 | 效果 |
| --- | --- | --- |
| 场景光照 | 开 | 跟随日光与局部灯光；关闭后显示基础色和自发光。 |
| 接收阴影 | 开 | 接收阴影贴图；关闭后仍保留光照方向、点光源距离衰减和向场景投射阴影。 |
| 模型泛光 | 关 | 开启后允许模型表面产生泛光；关闭时场景、火焰、武器泛光保留。 |

关闭场景光照时，接收阴影暂不可操作，但会记住选择。重新开启会恢复原始着色器及导入后的亮度基准。选项不改写 VRM 文件或游戏全局图像设置。

VRM 0.x 可导入，但旧 MToon／游戏材质、Standard 和第三方材质不受这三个开关控制。其他物体的泛光及游戏屏幕空间后处理仍可能覆盖模型；半透明衣服也可能有透明排序问题。

**1.8.8 修复开启游戏抗锯齿后，贴身半透明衣服／丝袜出现锯齿状缺口的问题。** TAA 的投影偏移现在同时用于身体、透明衣物和泛光遮罩；画面中有 VRM 时，该相机本帧的透明绘制统一使用相同投影，渲染后恢复相机原状态。游戏抗锯齿仍可开启，不改模型材质或透明度。此修复在观察者客户端生效，好友看到异常时也需要更新客户端，单独升级服务端无效。

**F8 关闭“模型泛光”、游戏开启抗锯齿**时，泛光排除遮罩逐帧丢失造成的闪烁也已修复，并通过连续 HDR 帧验证。该选项排除泛光，不会降低场景光照本身造成的模型亮度。

## 模型身高调整

F8 → **模型身高**可设置 **1.4–2.2 米**的目标外观高度，步长 1 厘米，默认 **2 米**。松开滑块后应用；“身高恢复为 2 米”恢复默认值。身高按本机游戏角色保存，切换模型、重启或重生后沿用，配置为 `BepInEx/config/ValheimVRM/avatar_heights.json`。

导入时在动画播放前测量一次站立可见网格高度，包含头发、耳朵和头饰。每位玩家的模型实例使用 `实际缩放 = 所选身高 / 原始网格高度`；可测量模型会放大或缩小到目标值，旧 `ModelScale` 不再覆盖该目标。无法测量几何时使用安全缩放回退。

调整身高时，从缓存模型创建该玩家的新显示实例，按初始化流程重新标定髋部到脚底的高度、坐姿接触几何、镜头高度、持物挂点和物理引用。无需重新读取模型文件，不会修改共享模板或其他玩家实例。站立、走路和跑步使用固定标定偏移，保留原版髋部动画，不累加补偿，也不把抬脚当成误差；坐地、坐椅继续单独处理动画接触位置。特殊姿势可能保留少量脚底穿入或离地，并非逐脚地形 IK。

**身高同步要求服务端、发送方及观察方客户端均为 1.8.12 或更新版本。** A 设置 1.4 米、B 设置 2 米时，各自显示对应身高，即使两人使用同一个模型。身高与模型选择一起绑定已认证的玩家连接和角色网络 ID，并共用递增请求序号；迟到的旧请求不能覆盖新身高。重生、后来加入和切换模型后仍按发送方身高显示。

无服务端插件或关闭同步时，身高调整只在本机生效。旧版服务器／客户端仍可同步模型，但不支持自定义远程身高；新版观察者将旧版发送方按默认 2 米显示。F8 会提示是否支持身高同步。原有站姿、坐姿偏移滑块仍是本机按模型保存的微调，与同步身高分别处理。

## 模型亮度基准

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

## 服务器同步

### 服务端怎么安装

在专用服务器上安装 BepInEx 5，停止服务器，将完整 `ValheimVRM-Server-1.8.12.zip` 解压到服务器根目录，再按原启动方式启动：

```text
专用服务器目录/
  valheim_server.exe
  BepInEx/plugins/ValheimVRM.Server/ValheimVRM.Server.dll
  ValheimVRM.Server/              （服务器包自带说明和许可证）
```

**这两个 `ValheimVRM.Server` 目录都不需要放客户端模型文件。** 服务器不导入模型，不需要客户端 UniVRM DLL 或着色器；服务器自己的 `ValheimVRM` 目录可以不存在。

参与同步的玩家安装完整客户端，并准备需要显示的同名、同内容 `.vrm`。进入世界后按 F8，保持同步勾选，确认“服务器同步已连接”。推荐服务器和参与同步的客户端统一为 **1.8.12**。

### 不同安装组合的行为

| 服务器 | 玩家客户端／模型 | 结果 |
| --- | --- | --- |
| 未装服务器插件 | 已装客户端 | 自动本地模式，只改变本机显示，不向服务器发送模型选择请求。 |
| 已装服务器插件 | 未装客户端 | 正常入服，看到原版角色，不参与外观同步。 |
| 已装服务器插件 | 已装客户端并开启同步 | 按每位玩家独立同步选择。 |
| 已装服务器插件 | 客户端缺文件、文件夹不同、同名文件内容不同 | 不影响入服；跳过无法显示的那次模型更新，保留该玩家上次可用外观，首次显示原版。 |
| 已装服务器插件 | 客户端关闭同步 | 本机保留自己的模型，撤回公开选择，并恢复本机其他玩家的原版外观。 |

整个模型文件夹无需完全一致：只有要显示的模型需要**相同文件名（含大小写）与 SHA-256 内容指纹**。服务器只中转名称与指纹，不传输 VRM。补齐未导入的文件后刷新列表重试；替换已缓存的同名文件后重启客户端。个人渲染、物理权重及模型 TXT 不由服务器同步。

### 玩家隔离与请求顺序

A 选择模型 1、B 选择模型 2，其他客户端看到的就是 A = 模型 1、B = 模型 2。选择绑定网络连接和角色网络 ID，不依赖昵称；同名玩家、相同模型、重生、重连和后来加入的玩家各自正确关联。

发送端与服务器都为 **1.8.3 或更新版本**时，F8 显示“请求顺序保护已启用”。每次实际发送请求携带递增序号：先发 1、后发 2，即使 2 先到，迟到的 1 和重复的 2 都不会覆盖最终选择。关闭同步也受保护；下行快照比较服务器 revision。使用连接内序号，不依赖电脑时间或游戏帧号。

旧 1.8.0～1.8.2 的协议兼容仍保留，但旧端不具备新增的上行顺序保护，新客户端会显示兼容模式。游戏本身的版本、密码与身份校验照常生效；VRM 插件不强制玩家安装客户端。

| 配置文件（均在 `BepInEx/config` 下） | 默认值 |
| --- | --- |
| `com.celestetwinkle.valheimvrm.server.cfg` | `[Sync] Enabled = true` |
| `com.yoship1639.plugins.valheimvrm.cfg` | `[AvatarSync] Enabled = true`，也可用 F8 修改。 |

保持 `global_settings.txt` 中的旧整文件共享选项 `EnableLegacyVrmSharing=false`。协议最多记录 128 名同时在线的同步玩家。更完整的边界说明见[服务器同步文档](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/SERVER-SYNC.md)。

## 常见问题

| 现象 | 检查方法 |
| --- | --- |
| F8 无法打开 | 进入世界、保持角色存活并关闭其他菜单；确认没有设置 `EnableAvatarPicker=false`，检查插件加载日志。 |
| 列表为空／刚放入的模型不显示 | `.vrm` 必须直接位于模型目录；`.unitypackage` 不能直接使用。点“刷新列表”，自定义启动脚本需检查工作目录，扫描异常见日志。 |
| 缺少 `settings_*.txt` 或 JSON | 不影响正常加载；TXT 可选；保存 F8 高度微调时会自动创建当前模型的 TXT，JSON 在保存相应选项时自动生成。 |
| 自己换装了，好友看不到 | 检查服务器插件、双方 F8 同步状态，以及接收方是否具有相同名称和内容的模型文件。服务器不自动下载模型给玩家。 |
| 进服提示“版本不兼容” | 核对客户端与服务器的游戏版本，并查看双方连接日志。VRM 服务端不会因客户端缺少本插件而拒绝入服。 |
| 摆动过大／衣服与身体穿模 | 降低 F8 物理权重；仍穿模时检查导出资产的蒙皮、形态键和碰撞设置。 |
| 身体有树叶状暗斑／关闭接收阴影仍存在 | 阴影贴图与屏幕空间后处理不同。本版为不透明／裁剪 MToon 表面补充深度与法线；检查是否装全新依赖，反馈时附材质类型及渲染选项。 |
| 退出世界后越来越卡、报显存分配错误 | 更新完整 **1.8.12** 并排除重复 DLL。该版已修复主菜单重入时重复补丁造成的显存泄漏。 |
| 模型退出视野后内存没有立刻下降 | 镜头外的活动角色仍需模型；只有最后一个实例销毁后才进入释放等待，详见下一节。 |

反馈问题请提供游戏版本、Mod 版本、触发步骤和相关日志片段：

- `<游戏目录>/BepInEx/LogOutput.log`。
- Windows：`%USERPROFILE%/AppData/LocalLow/IronGate/Valheim/Player.log`，以及重启前的 `Player-prev.log`（若存在）。
- 联机问题还需服务端插件版本和对应连接日志；请隐去密码、访问令牌等信息。

## 内存与验证范围

模型的最后一个角色实例销毁后，约 **15 秒**释放导入模板及其网格、贴图、材质和骨骼资源。多人共用一个模型时，要等最后一人停止使用；镜头外但仍在场景中的玩家、正在挂接的模型会保留。重新选择已释放的模型会再次导入。普通本地／服务器同步模式不常驻保留 VRM 源文件字节。

Unity 和显卡驱动可能保留内存池，因此资源已释放不代表任务管理器占用立刻下降。1.8.7 已通过持续 4K 泛光分配／释放、三次实际主菜单重载、异步挂接取消、模型缩放、坐姿、持物、物理权重及资源生命周期测试。

联机验证覆盖实际 ZRpc 序列化、生产服务端处理器和两名角色的独立绑定；尚未完成真实 Steam／PlayFab 专用服务器公网验收。Linux、macOS、Vulkan 和所有其他 Mod 组合未得到全面验证。1.8.12 的贴地与姿态回归见[验证记录](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.8.12-validation.md)。旧版生命周期测试见[1.8.7 验证记录](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.8.7-validation.md)。

## 开发与来源

**发布代码位于 `codex/public-release`。** 默认 `main` 的 README 用于介绍当前 Release，不代表 `main` 包含最新实现；编译前请切换发布分支。`codex/valheim-1.0-compatibility` 保留作为上游兼容性 PR 的来源。

准备 .NET 7 SDK、已安装的游戏与 BepInEx；插件目标框架为 .NET Framework 4.7.1：

```powershell
git switch codex/public-release
$env:VALHEIM_INSTALL_PATH = 'C:\Games\Valheim'
powershell -NoProfile -File tools/Test-RuntimeDependencies.ps1 -ValheimPath $env:VALHEIM_INSTALL_PATH
dotnet build ValheimVRM.csproj -c Release
dotnet run --project tests/AvatarCatalogTests
dotnet run --project tests/AvatarSyncTests -c Release
powershell -NoProfile -File tools/Build-ServerPackage.ps1 -ValheimPath $env:VALHEIM_INSTALL_PATH
```

输出为 `release/ValheimVRM-1.8.12.zip` 和 `release/ValheimVRM-Server-1.8.12.zip`。客户端构建会清理 release 目录，应先构建客户端，再打服务器包。默认不安装到游戏；只有显式传入 `-p:InstallToGame=true` 才安装。必须完整构建以嵌入渲染资源，不能用 `-t:Compile` 输出代替发布包。

[着色器重建](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/shaders/README.md) · [依赖来源与许可证](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/Libs/README.md) · [项目许可证](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/LICENSE) · [问题反馈](https://github.com/Celeste-twinkle/valheim-vrm/issues)

Fork 继承链：[yoship1639](https://github.com/yoship1639/ValheimVRM) → [aMidnightNova](https://github.com/aMidnightNova/ValheimVRM) → [nyaarium](https://github.com/nyaarium/valheim-vrm) → **Celeste-twinkle**。感谢原作者与维护者；本分支包含[上游 PR #53](https://github.com/nyaarium/valheim-vrm/pull/53) 的兼容性修复及后续独立功能。
