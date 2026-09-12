# ValheimVRM 1.8.7 安装说明（Celeste-twinkle 分支）

多人外观同步需要服务器另外安装独立服务端插件，详见[服务器同步与本地模式](SERVER-SYNC.md)。

适用于 Windows x64 客户端，验证环境为英灵神殿 1.0.12、Unity 6000.0.75f1、
BepInExPack Valheim 5.4.2333（BepInEx 5.4.23.3）。这是独立 fork 的编译版本。
安装包不含角色模型，请自行准备有权使用的 VRM。

## 安装与升级

1. 退出游戏。尚未安装 BepInEx 时，先安装
   [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/5.4.2333/)，启动一次游戏后退出。
2. 已装旧插件时先备份。`BepInEx/plugins` 下只保留一份 `ValheimVRM.dll`，
   移除重复的旧插件目录。保留自己原有的 VRM 和设置文件。
3. 将 `ValheimVRM-1.8.7.zip` 直接解压到 `valheim.exe` 所在目录，合并
   `BepInEx`、`valheim_Data` 文件夹。务必使用完整包，不能只替换 DLL。
4. 将任意数量的 `.vrm` 放入游戏根目录的 `ValheimVRM` 文件夹中。
   支持中文、空格文件名；不扫描子文件夹和 `Shared` 联机缓存。
5. 进入世界后按 **F8** 打开人物外观面板，点击模型切换。列表可以滚动。
   添加或移除文件后点“刷新列表”；F8 或 Esc 关闭面板。

```text
游戏目录/
  valheim.exe
  BepInEx/plugins/ValheimVRM/ValheimVRM.dll
  BepInEx/plugins/ValheimVRM/UniVRM.shaders
  valheim_Data/Managed/VRM10.dll 等配套依赖
  ValheimVRM/我的模型.vrm
  BepInEx/config/ValheimVRM/       （保存个人选项时自动创建）
```

模型列表不限制为八个。选择保存在 `avatar_selections.json` 中，按游戏角色分别记录，
重启游戏或死亡复活后自动恢复。选择保存在 `BepInEx/config/ValheimVRM/avatar_selections.json`。
已选文件被移除时回到原来的角色同名模型／默认模型匹配方式。
模型加载失败会显示错误并保留先前外观。替换同一模型文件的内容后请重启游戏以清除缓存。

F8 的“物理摆动权重”可减轻过大的摆动，默认 50%，范围 0%～100%。0% 不摆动，
100% 为原始物理摆幅。兼容 VRM 1.0 与 VRM 0.x，拖动即时生效，松开后保存到
`BepInEx/config/ValheimVRM/physics_options.json`。这是本机统一设置，切换模型后保留。

切换外观不会卸下装备，也不会改变装备本身的护甲、伤害属性。原有模型配置仍可影响
碰撞体尺寸、交互距离、武器位置及装备是否显示。插件不修改角色存档。

坐地时会按当前脚底姿态自动补偿下陷，椅子、船和床仍使用各自的偏移设置。
左右手挂点按人形骨骼的手掌比例校准，并跟随最终动画姿态；原有物品偏移仍可微调。
缺少足够指骨的模型会退回手腕跟随方式。本次不增加双手武器的第二只手 IK。

VRM 1.0 模型副本会保留导出的弹簧链和碰撞体，恢复已有的头发、衣物及身体弹簧物理。
插件保留导出参数，无法补回未写入 VRM 文件的 VRChat PhysBone 数据。

## 光照、阴影与泛光

面板的三个开关作用于本机的 **VRM 1.0 MToon 材质**，切换模型及死亡生成布娃娃后同样有效。
默认效果与维护中的本地版一致：

- **场景光照：开**。使用导入后的材质，跟随日光和灯光受光。关闭后显示基础色和自发光，去掉场景光照造成的明暗。
- **接收阴影：开**。关闭后不采样阴影贴图，仍保留光照方向、灯光投影纹理与点光源距离衰减，不关闭模型向场景投射阴影。
- **模型泛光：关**。模型表面不再作为泛光输入；场景、火焰、武器的泛光保留。

**1.8.5 起，MToon10 导入时自动限制基础色 RGB 为 0.45、阴影色 RGB 为 0.2025。**
只对超过相应基准的颜色整体按比例压低，未超过的颜色与透明度不变，无需在 F8 中调整。
原始 VRM 文件和同步指纹不改动。该限制作用于导入颜色系数，自发光、动画颜色覆盖及光照仍影响最终亮度。
完整说明见 [模型亮度基准](../README.zh-CN.md#模型亮度基准)。
光照、阴影重新开启时恢复原始着色器，并保留导入时的亮度限制。关闭光照后，接收阴影选项暂不可操作，
但会保留用户选择。场景其他物体产生的泛光仍可能覆盖到模型，游戏的屏幕空间后处理仍会生效。
不透明／裁剪 MToon 表面会补充自己的延迟渲染深度和法线，避免模型后方的环境遮蔽轮廓
错误叠到身体上。这项兼容修复不关闭游戏的环境遮蔽。

选择立即保存到 `BepInEx/config/ValheimVRM/rendering_options.json`。设置不改写 `.vrm` 文件，
不改全局图像设置，不会改变其他模型查看器的渲染效果。
VRM 0.x 仍可导入，但它使用的旧 MToon／游戏材质以及 Standard、第三方材质不受这三个开关控制。

## 只放 VRM 的安装方式

`ValheimVRM` 模型目录只放 `.vrm` 就能正常使用，无需任何 TXT、JSON、清单文件、
默认模型、固定模型组合或初始化脚本。无配置时内置参数为原始缩放 1.0、亮度 1.0、
启用 MToon、关闭旧版角色淡出；可选配置仍可覆盖这些参数，但缩放受 1.6 米最低高度限制。
1.8.7 起，加载时按站立可见网格高度（含头发和头饰）等比放大，实际倍率为
`max(ModelScale, 1.6 / 原始高度)`。1.2 米模型变为 1.6 米，已足够高或手动放大更多的保持。
导入时仅测量一次，本地和远程模型共用该规则，坐姿或反复切换不会累积缩放。

以下文件由 Mod 按需创建，全部位于 `BepInEx/config/ValheimVRM`：

- 选择模型后生成 `avatar_selections.json`。
- 调整并保存物理滑块后生成 `physics_options.json`。
- 修改渲染选项后生成 `rendering_options.json`。

缺少这些文件或整个配置目录时，使用默认值正常工作。TXT 是可选手动配置，不会自动生成。
1.8.6 起不再读取或自动迁移模型目录中的旧配置，也不再读取 `selected_models.json`。
升级前可把想保留的 TXT 和上述三类 JSON 移到新配置目录，否则使用默认值。
说明、许可证和配置示例位于 `BepInEx/plugins/ValheimVRM`。

## 配置和联机

模型专用配置命名为 `settings_模型文件名.txt`（不含 `.vrm` 后缀）。
这是可选的纯文本文件，不是 Unity/VRM 导出产物，插件目前也不会自动生成它。
没有配置文件时仍能加载模型，使用内置默认参数。

创建方法：复制 `BepInEx/plugins/ValheimVRM/settings_Example.txt.example` 模板。例如模型为 `MyAvatar.vrm`，将副本命名为
`settings_MyAvatar.txt`，放在 `BepInEx/config/ValheimVRM` 文件夹。
使用记事本编辑 `参数名=值`，如 `ModelScale=1.0`（缩放）、`ModelOffsetY=0`
（上下偏移）、`ModelBrightness=1`（亮度）；省略的参数使用默认值。
启用资源管理器的“文件扩展名”，确保名称不以 `.txt.txt` 或 `.example` 结尾。
保存后重启游戏再选择模型；F8 的“刷新列表”只重新扫描文件，不重载已有模型配置缓存。
复制其他模型的配置时，请检查其中是否有仅适用于原模型的比例或偏移值。

也可沿用 `游戏角色名.vrm` 的自动匹配方式。默认模型名为 `___Default.vrm`（三个下划线），
默认配置名为 `settings____Default.txt`（四个下划线）。示例配置以 `.example` 结尾，
不会自动覆盖用户设置。在 `global_settings.txt` 中写入 `EnableAvatarPicker=false` 可禁用 F8 菜单。

只改变本机外观时，无需服务器安装。多人同步时，在已安装 BepInEx 5 的服务器上
解压 `ValheimVRM-Server-1.8.7.zip`，并让各客户端安装相同模型文件，详见
[服务器安装说明](SERVER-SYNC.md)。F8 可随时退出同步；通过游戏“启动服务器”的
房主也可同时安装服务端插件。旧版整文件分享协议默认通过
`EnableLegacyVrmSharing=false` 停用，使用新同步时请保持关闭。分发模型仍需遵守其许可。

## 排错与卸载

- F8 请在进入世界后使用，并先关闭聊天、物品栏和其他菜单。
- 空列表时检查文件是否直接位于游戏根目录的 `ValheimVRM` 中。
- 查看 `BepInEx/LogOutput.log` 中的插件版本 **1.8.7**、模型导入错误和着色器错误。
- 老版本升级请阅读仓库 `Libs/README.md`，避免混用新旧 UniVRM 依赖；
  不要用旧版 Unity.Burst、Unity.Mathematics 覆盖游戏自带 DLL。
- 卸载时退出游戏并移除 `BepInEx/plugins/ValheimVRM`；模型、配置可自行保留。
  不要删除其他插件仍需使用的共享依赖。

本次验证限于 Windows／D3D11；Linux、macOS、Vulkan、多人分享及其他插件组合未验证。
反馈请提交至 [本 fork 的 Issues](https://github.com/Celeste-twinkle/valheim-vrm/issues)，
附上游戏版本、Release 标签及相关日志。

## 未使用模型的内存释放

1.8.4 起，模型的最后一个角色实例被销毁后，约 15 秒自动释放导入模板及其网格、
贴图、材质和骨骼资源。换装、玩家离开场景或恢复原版外观都会解除相应引用。
仍在场景内、只是位于镜头外的玩家继续保留；多人使用相同模型时，要等最后一人
不再使用才释放。导入/绑定过程受到保护。再次选择已释放的模型会重新加载。
模型列表不会预加载整个文件夹。普通本地/服务器同步模式也不再常驻保存 VRM 原始文件字节。
Unity 和显卡驱动可能保留内存池，因此任务管理器中的占用不一定立刻下降。
