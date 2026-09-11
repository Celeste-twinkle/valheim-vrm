# ValheimVRM — Celeste-twinkle fork

[Download the compiled release](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) · [Release source](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/public-release) · [English installation](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.md) · [中文安装说明](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.zh-CN.md)

Windows x64 client build for Valheim 1.0.7. This fork combines the compatibility
fixes proposed in [upstream PR #53](https://github.com/nyaarium/valheim-vrm/pull/53)
with an in-game avatar picker and optional rendering controls. Download
`ValheimVRM-1.7.0.zip` from this fork's Release page for the compiled plugin.

## Fork history

[yoship1639/ValheimVRM](https://github.com/yoship1639/ValheimVRM)
→ [aMidnightNova/ValheimVRM](https://github.com/aMidnightNova/ValheimVRM)
→ [nyaarium/valheim-vrm](https://github.com/nyaarium/valheim-vrm)
→ **[Celeste-twinkle/valheim-vrm](https://github.com/Celeste-twinkle/valheim-vrm)**

Thanks to the original author and each fork's maintainers. This repository provides
an independent compiled release; report problems with this build to
[this fork's Issues](https://github.com/Celeste-twinkle/valheim-vrm/issues).

## Installation

Install BepInEx separately, then extract the **complete release ZIP** into the
folder containing `valheim.exe`. The package includes the matching UniVRM
dependencies; copying only `ValheimVRM.dll` is insufficient. Exit the game before
installing or upgrading, and keep only one installed copy of the plugin.

No avatars are included. Add your own `.vrm` files directly to the game's
`ValheimVRM` folder:

```text
Valheim/
  valheim.exe
  BepInEx/plugins/ValheimVRM/ValheimVRM.dll
  BepInEx/plugins/ValheimVRM/UniVRM.shaders
  valheim_Data/Managed/             (dependencies from the ZIP)
  ValheimVRM/
    My Avatar.vrm
    Another Avatar.vrm
```

See the [English guide](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.md)
or [中文安装说明](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.zh-CN.md)
for prerequisites, upgrades and per-model settings.

## F8 avatar menu / F8 人物外观菜单

1. Enter a world with your character, close chat, inventory and other menus, then
   press **F8** to open the avatar panel.
2. Scroll through the list and click a model to switch. The list supports **any
   number of models**, including filenames with spaces or Chinese characters.
   Subfolders and the `Shared` multiplayer cache are not scanned.
3. After adding or removing files, click **Refresh list / 刷新列表**. If you replace
   the contents of an existing model file, restart the game to clear its cache.
4. Press **F8**, **Esc**, or **Close / 关闭** to close the panel.

Selections are saved per game character in `ValheimVRM/avatar_selections.json`
and restored after restarting or respawning. A failed import displays an error
and retains the previous appearance. Changing appearance keeps your equipped
items and their stats; existing per-model settings can still control equipment
visibility, weapon placement, collider size and interaction distance.

The picker changes local appearance. It does not add live multiplayer outfit
synchronization, so other players are not guaranteed to see your current choice.

**中文快速使用：** 将任意数量的 `.vrm` 直接放入游戏根目录的 `ValheimVRM`
文件夹，进入世界后按 **F8**，滚动列表并点击模型切换。增删文件后点“刷新列表”，
按 F8 或 Esc 关闭。选择按游戏角色自动保存，切换外观时保留装备和装备属性。

### Rendering controls / 渲染开关

The panel includes three controls for **VRM 1.0 MToon materials**:

| Control | Default | Effect |
| --- | --- | --- |
| Scene lighting / 场景光照 | On / 开 | Respond to sunlight and local lights. When off, display base color and emission without scene lighting. |
| Receive shadows / 接收阴影 | On / 开 | Receive shadow-map shadows. Turning this off preserves light direction and point-light distance attenuation; the avatar can still cast shadows. |
| Avatar bloom / 模型泛光 | Off / 关 | Allow the avatar surface to contribute to bloom. When off, scene, fire and weapon bloom remain enabled. |

**There is no brightness ceiling, highlight compression or brightness-limit setting.**
默认开启光照和接收阴影、关闭模型泛光；**亮度上限已完全移除**。

Changes apply immediately and are saved in `ValheimVRM/rendering_options.json`.
They also apply when switching avatars or creating death ragdolls. Turning scene
lighting off temporarily disables the receive-shadows control while remembering
its selection. Restoring lighting and shadows restores the original shader and
exported material parameters.

These controls do not rewrite VRM files or change global graphics settings.
VRM 0.x can still be imported, but its legacy MToon/game materials, Standard and
third-party materials are outside the controls' scope. Bloom from other objects
can still overlap the avatar, and the game's other screen-space effects remain active.

If F8 does not open the menu, check that you are alive and in a world, close other
menus, and check `BepInEx/LogOutput.log`. Setting `EnableAvatarPicker=false` in
`ValheimVRM/global_settings.txt` disables the picker.

## Development

The compiled release's rendering and picker implementation is on
[`codex/public-release`](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/public-release).
The default `main` branch does not contain those implementation changes; check out
the release branch before building. The narrower
[`codex/valheim-1.0-compatibility`](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/valheim-1.0-compatibility)
branch remains the source of the upstream PR.

With a .NET SDK and an installed copy of Valheim plus BepInEx:

```powershell
git switch codex/public-release
$env:VALHEIM_INSTALL_PATH = 'C:\Games\Valheim'
powershell -NoProfile -File tools/Test-RuntimeDependencies.ps1 -ValheimPath $env:VALHEIM_INSTALL_PATH
dotnet build -c Release
dotnet run --project tests/AvatarCatalogTests
```

Build output is `release/ValheimVRM-1.7.0.zip`. Building does not install the plugin
into your game unless you explicitly pass `-p:InstallToGame=true`.
The catalog tests use .NET 7 and temporary files; the plugin targets .NET Framework 4.7.1.
Shader source/rebuild instructions are in
[shaders/README.md](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/shaders/README.md).
Use a full build to include embedded rendering resources; `-t:Compile` alone
is insufficient for a distributable DLL.

Read [runtime dependency provenance](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/Libs/README.md),
[compatibility validation](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/valheim-1.0-validation.md), and
[release validation](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.7.0-validation.md)
for the tested scope. This release was validated on Windows/D3D11; Linux, macOS,
Vulkan and multiplayer model sharing were not validated for this build.
