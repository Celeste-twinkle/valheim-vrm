# ValheimVRM — Celeste-twinkle fork

**English** | [简体中文](README.zh-CN.md)

[Download the compiled release](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) · [Release source](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/public-release) · [English installation](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.md) · [中文安装说明](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.zh-CN.md)

Windows x64 client build for Valheim 1.0.12. This fork combines the compatibility
fixes proposed in [upstream PR #53](https://github.com/nyaarium/valheim-vrm/pull/53)
with an in-game avatar picker and optional rendering controls. Download
`ValheimVRM-1.8.3.zip` from this fork's Release page for the compiled plugin.

## Fork history

[yoship1639/ValheimVRM](https://github.com/yoship1639/ValheimVRM)
→ [aMidnightNova/ValheimVRM](https://github.com/aMidnightNova/ValheimVRM)
→ [nyaarium/valheim-vrm](https://github.com/nyaarium/valheim-vrm)
→ **[Celeste-twinkle/valheim-vrm](https://github.com/Celeste-twinkle/valheim-vrm)**

Thanks to the original author and each fork's maintainers. This repository provides
an independent compiled release; report problems with this build to
[this fork's Issues](https://github.com/Celeste-twinkle/valheim-vrm/issues).

## Installation

Install BepInEx 5 separately, then extract the **complete release ZIP** into the
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

## Using the server package

Server synchronization is optional. Download the packages from this fork's
[Release page](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest):

| Install on | Package | Setup |
| --- | --- | --- |
| Every player's client | `ValheimVRM-1.8.3.zip` | Install the complete client as above and distribute the same `ValheimVRM` model folder. |
| Dedicated server | `ValheimVRM-Server-1.8.3.zip` | Install BepInEx 5 first, extract beside the server executable, then restart the server. |
| A player hosting through **Start server** | Both packages | Install both in the host's game directory; other players only need the client package. |

The server DLL must end up at
`BepInEx/plugins/ValheimVRM.Server/ValheimVRM.Server.dll`.
The server needs no models, UniVRM dependencies or client shaders. BepInEx is
installed separately and is not included in the server ZIP.
**Players without this mod can also join normally.** They see the original game
characters and do not participate in avatar synchronization. The server does not
require the client addon or reject/disconnect players who lack it.

Folders may differ without affecting admission or normal play. To display a
particular remote avatar, the receiver needs its matching filename (including
case) and VRM contents; other files may differ or be absent. Distribute the model-specific `settings_ModelName.txt` files consistently
as well. Join the world, press **F8**, leave **Server avatar sync (when available)**
enabled, and check for **Server sync connected** before choosing a model.
If A selects model 1 and B selects model 2, every participating client sees
A = model 1 and B = model 2. A switching again changes only A; identical player
names or model choices still use independent avatar instances.
With a 1.8.3 sender and server, increasing request sequences reject late or
duplicate choices. F8 shows **Request order protection active**. Legacy peers
still connect, but both sender and server must upgrade for this protection.

Without the server addon, the picker automatically works locally. Opting out in
F8 keeps your local avatar, withdraws your shared selection, and restores remote
players to their original appearance on your client. Missing or different model
files produce a message and retain the last usable appearance (vanilla on first
load). Unreadable files are also skipped. Hashes are checked before importing
uncached models, and a failed choice does not block other players. Add or correct
unimported files and use **Refresh list**; restart after replacing a cached model.
The server relays names and hashes, never VRM files, and ignores its model folder.

After the first launch, server configuration is in
`BepInEx/config/com.celestetwinkle.valheimvrm.server.cfg`, with `[Sync] Enabled = true`
by default. The client F8 switch saves `[AvatarSync] Enabled` in
`BepInEx/config/com.yoship1639.plugins.valheimvrm.cfg`.
See [server synchronization](docs/SERVER-SYNC.md) for more setup and troubleshooting details.

## F8 avatar menu

1. Enter a world with your character, close chat, inventory and other menus, then
   press **F8** to open the avatar panel.
2. Scroll through the list and click a model to switch. The list has **no fixed model-count limit** and supports filenames with spaces
   or Chinese characters.
   Subfolders and the `Shared` multiplayer cache are not scanned.
3. After adding or removing files, click **Refresh list**. If you replace
   the contents of an existing model file, restart the game to clear its cache.
4. Press **F8**, **Esc**, or **Close** to close the panel.

The panel uses Chinese when the game language is Chinese, and English otherwise.

Selections are saved per game character in `ValheimVRM/avatar_selections.json`
and restored after restarting or respawning. An invalid model file displays an import error
and retains the previous appearance. Changing appearance keeps your equipped
items and their stats; existing per-model settings can still control equipment
visibility, weapon placement, collider size and interaction distance.

Optional server synchronization is available with the separate **ValheimVRM.Server** addon.
With the same VRM folder on each client, A changing their model updates only A on
other clients. Without the server addon, selection remains local. See
[server installation and modes](docs/SERVER-SYNC.md).

### Physics weight

The **Physics sway weight** slider scales exported hair, clothing and body spring
motion from **0%** (no spring rotation) to **100%** (original motion). The default
is **50%**. It works with VRM 1.0 and VRM 0.x, takes effect immediately, and is
saved locally in `ValheimVRM/physics_options.json` when dragging ends. The setting
applies across model switches; authored physics parameters are preserved.

### Rendering controls

The panel includes three controls for **VRM 1.0 MToon materials**:

| Control | Default | Effect |
| --- | --- | --- |
| Scene lighting | On | Respond to sunlight and local lights. When off, display base color and emission without scene lighting. |
| Receive shadows | On | Receive shadow-map shadows. Turning this off preserves light direction and point-light distance attenuation; the avatar can still cast shadows. |
| Avatar bloom | Off | Allow the avatar surface to contribute to bloom. When off, scene, fire and weapon bloom remain enabled. |

**This plugin imposes no brightness ceiling or highlight compression and has no
brightness-limit setting.**

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
dotnet run --project tests/AvatarSyncTests -c Release
powershell -NoProfile -File tools/Build-ServerPackage.ps1 -ValheimPath $env:VALHEIM_INSTALL_PATH
```

Build output is `release/ValheimVRM-1.8.3.zip`. Building does not install the plugin
into your game unless you explicitly pass `-p:InstallToGame=true`.
Run the server packaging command after the client build to produce
`release/ValheimVRM-Server-1.8.3.zip`.
The catalog tests use .NET 7 and temporary files; the plugin targets .NET Framework 4.7.1.
Shader source/rebuild instructions are in
[shaders/README.md](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/shaders/README.md).
Use a full build to include embedded rendering resources; `-t:Compile` alone
is insufficient for a distributable DLL.

Read [runtime dependency provenance](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/Libs/README.md),
[compatibility validation](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/valheim-1.0-validation.md), and
[release validation](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.8.3-validation.md)
for the tested scope. Windows/D3D11 engine probes cover actual ZRpc serialization,
production server handlers and independent model attachment to two player fixtures.
A real Steam/PlayFab dedicated-server session, Linux, macOS and Vulkan have not
been validated for this build.
