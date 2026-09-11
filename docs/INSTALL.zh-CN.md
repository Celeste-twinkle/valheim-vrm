# ValheimVRM 1.7.0 安装说明（Celeste-twinkle 分支）

适用于 Windows x64 客户端，验证环境为英灵神殿 1.0.7、Unity 6000.0.75f1、
BepInExPack Valheim 5.4.2333（BepInEx 5.4.23.3）。这是独立 fork 的编译版本。
安装包不含角色模型，请自行准备有权使用的 VRM。

## 安装与升级

1. 退出游戏。尚未安装 BepInEx 时，先安装
   [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/5.4.2333/)，启动一次游戏后退出。
2. 已装旧插件时先备份。`BepInEx/plugins` 下只保留一份 `ValheimVRM.dll`，
   移除重复的旧插件目录。保留自己原有的 VRM 和设置文件。
3. 将 `ValheimVRM-1.7.0.zip` 直接解压到 `valheim.exe` 所在目录，合并
   `BepInEx`、`valheim_Data`、`ValheimVRM` 文件夹。务必使用完整包，不能只替换 DLL。
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
  ValheimVRM/settings_我的模型.txt  （可选）
```

模型列表不限制为八个。选择保存在 `avatar_selections.json` 中，按游戏角色分别记录，
重启游戏或死亡复活后自动恢复。旧本地版的 `selected_models.json` 可按唯一文件名后缀迁移。
已选文件被移除时回到原来的角色同名模型／默认模型匹配方式。
模型加载失败会显示错误并保留先前外观。替换同一模型文件的内容后请重启游戏以清除缓存。

切换外观不会卸下装备，也不会改变装备本身的护甲、伤害属性。原有模型配置仍可影响
碰撞体尺寸、交互距离、武器位置及装备是否显示。插件不修改角色存档。

## 光照、阴影与泛光

面板的三个开关作用于本机的 **VRM 1.0 MToon 材质**，切换模型及死亡生成布娃娃后同样有效。
默认效果与维护中的本地版一致：

- **场景光照：开**。保留模型原有材质，跟随日光和灯光受光。关闭后显示基础色和自发光，去掉场景光照造成的明暗。
- **接收阴影：开**。关闭后不采样阴影贴图，仍保留光照方向、灯光投影纹理与点光源距离衰减，不关闭模型向场景投射阴影。
- **模型泛光：关**。模型表面不再作为泛光输入；场景、火焰、武器的泛光保留。

**完全没有亮度上限或亮部压缩，也没有亮度封顶选项。**
光照、阴影重新开启时恢复原始着色器及导出材质参数。关闭光照后，接收阴影选项暂不可操作，
但会保留用户选择。场景其他物体产生的泛光仍可能覆盖到模型，游戏的屏幕空间后处理仍会生效。

选择立即保存到 `ValheimVRM/rendering_options.json`。设置不改写 `.vrm` 文件，
不改全局图像设置，不会改变其他模型查看器的渲染效果。
VRM 0.x 仍可导入，但它使用的旧 MToon／游戏材质以及 Standard、第三方材质不受这三个开关控制。

## 配置和联机

模型专用配置命名为 `settings_模型文件名.txt`（不含 `.vrm` 后缀）。
也可沿用 `游戏角色名.vrm` 的自动匹配方式。默认模型名为 `___Default.vrm`（三个下划线），
默认配置名为 `settings____Default.txt`（四个下划线）。示例配置以 `.example` 结尾，
不会自动覆盖用户设置。在 `global_settings.txt` 中写入 `EnableAvatarPicker=false` 可禁用 F8 菜单。

只改变本机外观时，无需服务器安装。菜单切换不提供新增的多人外观实时同步功能，
不要假定其他玩家会看到当前选择。原仓库继承的模型分享协议尚未在这次发布中验证；
分享模型文件需遵守其许可。需要禁用旧分享功能时，模型配置设 `AllowShare=false`，
全局配置设 `AcceptVrmSharing=false`。

## 排错与卸载

- F8 请在进入世界后使用，并先关闭聊天、物品栏和其他菜单。
- 空列表时检查文件是否直接位于游戏根目录的 `ValheimVRM` 中。
- 查看 `BepInEx/LogOutput.log` 中的插件版本 **1.7.0**、模型导入错误和着色器错误。
- 老版本升级请阅读仓库 `Libs/README.md`，避免混用新旧 UniVRM 依赖；
  不要用旧版 Unity.Burst、Unity.Mathematics 覆盖游戏自带 DLL。
- 卸载时退出游戏并移除 `BepInEx/plugins/ValheimVRM`；模型、配置可自行保留。
  不要删除其他插件仍需使用的共享依赖。

本次验证限于 Windows／D3D11；Linux、macOS、Vulkan、多人分享及其他插件组合未验证。
反馈请提交至 [本 fork 的 Issues](https://github.com/Celeste-twinkle/valheim-vrm/issues)，
附上游戏版本、Release 标签及相关日志。
