# ValheimVRM — Celeste-twinkle fork

**English** | [简体中文](README.zh-CN.md)

Replace Valheim characters with your own humanoid VRM avatars and switch them with **F8**. Use it locally or install the separate server addon so players can see one another's selected avatars.

**Current release: 1.8.13** · Tested with Valheim **1.0.12**, Windows x64, Unity 6000.0.75f1, BepInEx **5.4.23.3**, D3D11.

[Download Release](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) · [Release source](https://github.com/Celeste-twinkle/valheim-vrm/tree/codex/public-release) · [Changelog](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/release-notes.md)

[Installation](#installation-and-upgrades) · [Models and settings](#models-and-configuration) · [F8 menu](#f8-avatar-menu) · [Height](#adjustable-model-height) · [Brightness](#model-brightness-reference) · [Server sync](#server-synchronization) · [Troubleshooting](#troubleshooting)

## Features

- Import humanoid VRM 0.x and VRM 1.0 models. The scrolling picker supports spaces and Chinese filenames.
- Save selections per game character, preserve equipped items and stats, and adapt item mounts to each avatar's hand bones.
- Adjust physics sway weight, scene lighting, received shadows and avatar bloom.
- Set model height from **1.4 to 2.2 m**, default **2 m**, and synchronize it independently per player. Apply VRM 0.x/1.0 MToon brightness limits at import.
- Optionally synchronize selections independently per player, with increasing request sequences to handle out-of-order messages.
- Release unused avatar resources. **1.8.7 fixes the GPU memory leak from duplicate patches when returning to the main menu, and cancels avatar attachment safely during scene unload.**

The public release includes **no avatars**. Unity packages, FBX files and VRChat projects must first be exported to VRM. Exported bones, materials and spring settings determine which effects can be reproduced.

## Installation and upgrades

### Choose a package

| Use case | Install |
| --- | --- |
| Player client: single-player, local appearance or synchronized appearance | BepInEx 5 + `ValheimVRM-1.8.13.zip` + your own `.vrm` files. |
| Dedicated server that synchronizes avatars | BepInEx 5 + `ValheimVRM-Server-1.8.13.zip`; no avatars or client dependencies needed. |
| Player hosting through **Start server**, with avatar synchronization | Both client and server packages on the host; other players use the client package. |
| Source development | `ValheimVRM-1.8.13-source.zip` contains source, not an installable plugin. |

Neither public runtime ZIP includes BepInEx. See the [installation guide](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/INSTALL.md) for the loader and dependency provenance.

### Prerequisite downloads

| Dependency | Download | Installation notes |
| --- | --- | --- |
| **BepInExPack Valheim (recommended)** | [Valheim-specific package](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) | Preconfigured for Valheim. The tested package is **5.4.2333**, with BepInEx core **5.4.23.3**. |
| BepInEx upstream | [5.4.23.3 Release](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3) · [All releases](https://github.com/BepInEx/BepInEx/releases) | Upstream downloads and version history. For Windows, use BepInEx **5 / x64 / Mono**; the Valheim pack above supplies the game-specific configuration. |

Install the loader once per game/server directory. The complete client ZIP already provides the matching UniVRM runtime libraries and shaders; the server addon does not need those client dependencies.

The separately shared **local Windows x64 bundles** `09_ValheimVRM_1.8.13_完整插件.zip` and `10_ValheimVRM_Server_1.8.13.zip` both include BepInEx **5.4.23.3**, so a fresh installation needs no separate loader download. Stop the game/server before installing. On a server that already has compatible BepInEx, copy only `BepInEx/plugins/ValheimVRM.Server` from the local server ZIP and preserve the existing loader and configuration.

### Player setup

1. Exit Valheim. If necessary, install BepInEx 5, launch the game once, then exit.
2. Extract the **complete client ZIP** beside `valheim.exe`, merging `BepInEx` and `valheim_Data`. Copying only `ValheimVRM.dll` is insufficient.
3. Create `ValheimVRM` in the game root and add `.vrm` files directly inside. One model is enough.
4. Start the game, enter a world, close chat/inventory and other menus, then press **F8** and select a model.
5. Confirm **ValheimVRM 1.8.13** loads in `BepInEx/LogOutput.log`.

```text
Valheim/
  valheim.exe
  BepInEx/
    plugins/ValheimVRM/
      ValheimVRM.dll
      UniVRM.shaders
      README.md, configuration examples, licenses, ...
    config/ValheimVRM/          (created when saving personal options)
  valheim_Data/Managed/         (matching dependencies from the full ZIP)
  ValheimVRM/
    My Avatar.vrm
    Another Avatar.vrm
```

To upgrade, exit the game, back up wanted files and keep only one `ValheimVRM.dll` under `BepInEx/plugins`. Store backup DLLs outside that scanned directory. Extract the full new package while preserving avatars and `BepInEx/config`. Do not mix old UniVRM dependencies or overwrite the game's `Unity.Burst` / `Unity.Mathematics` with older copies.

When upgrading from 1.8.5 or earlier, manually move wanted model/global TXT settings and the three JSON files below to `BepInEx/config/ValheimVRM`. **Since 1.8.6, old model-folder settings and `selected_models.json` are neither read nor automatically migrated.** Without migration, defaults apply.

With a separate r2modman profile, install plugins into the active profile; configuration follows that BepInEx profile. Models are read from `ValheimVRM` under the game process's working directory, normally the game root. Set custom launch scripts' working directory to the game root. To uninstall, exit and remove this plugin's directory, keeping your avatars, settings and shared dependencies required by other mods.

## Models and configuration

**Only `.vrm` files are needed in `ValheimVRM`.** No TXT, JSON, manifest, fixed model set, `___Default.vrm` or initialization script is required. Subfolders, including the old `Shared` cache, are not scanned.

Paths below are relative to the game root. Missing optional files or an absent configuration directory use built-in defaults.

| File / directory | Purpose | Created by |
| --- | --- | --- |
| `ValheimVRM/*.vrm` | Avatars; provide at least one to select. | The user. |
| `BepInEx/config/ValheimVRM/avatar_selections.json` | Model choices per game character. | The mod when saving a selection. |
| `BepInEx/config/ValheimVRM/avatar_heights.json` | Visual height per local game character (default 2 m). | The mod when applying the height slider. |
| `BepInEx/config/ValheimVRM/physics_options.json` | Client-wide physics sway weight. | The mod when saving a slider change. |
| `BepInEx/config/ValheimVRM/rendering_options.json` | Local rendering controls. | The mod when changing an option. |
| `BepInEx/config/ValheimVRM/settings_ModelName.txt` | Optional model scale, offsets and equipment settings. | Created when F8 height offsets are saved; other settings may be added manually. |
| `BepInEx/config/ValheimVRM/global_settings.txt` | Optional global settings, including the F8 picker switch. | The user; not auto-generated. |
| `BepInEx/plugins/ValheimVRM/` | Plugin, shaders, docs, examples and licenses. | Extracting the plugin package. |

For `MyAvatar.vrm`, copy `settings_Example.txt.example` from the plugin directory, rename it to `settings_MyAvatar.txt` and place it in the configuration directory above. Alternatively, create a plain text file containing only the settings you need:

```ini
ModelScale=1.0
ModelOffsetY=0
```

Omitted settings use defaults. Show file extensions in Explorer to avoid `.txt.txt` or a leftover `.example` suffix. Restart after editing: F8 **Refresh list** does not reload cached models or model settings. See the [configuration example](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/settings_Example.txt.example) for available parameters.

Optional automatic matching still supports `CharacterName.vrm` and `___Default.vrm` (three underscores); the latter uses `settings____Default.txt` (four underscores). The F8 workflow needs no default avatar.

## F8 avatar menu

Use F8 while alive in a world. Close with F8, Esc or **Close**. The panel uses Chinese for a Chinese game language, and English otherwise.

- Click a model to switch. Choices persist per character across restarts and respawns. Import failure displays an error and retains the last usable appearance.
- Use **Refresh list** after adding/removing files. Restart after replacing a same-name file or editing model TXT settings.
- There is no fixed list-size limit and browsing does not preload the library. Large models still require sufficient RAM and VRAM.
- **Server avatar sync (when available)** controls publishing your choice and receiving other players' avatars, as described below.

### Standing and sitting height offsets

With both sliders at **0 cm**, 1.8.12 calibrates standing height once from each avatar's humanoid reference skeleton and sole geometry. It reads rest-pose hip/foot positions, skin bind matrices and footwear thickness, including the current scale. Calibration is independent of whether the player is standing, moving or seated when loading the model.

Standing, walking and running use a fixed vertical offset. The original animation's hip motion is retained; animated foot minima no longer raise/lower the entire avatar. The native skeleton remains read-only. Ground/chair sitting retains separate contact handling and transitions. A fixed reference prioritizes stable animation and can leave small foot intersections or gaps in some poses; it is not per-foot IK. There are no model-name-specific height constants.

F8 provides **Standing height offset** and **Sitting height offset**, independently adjustable from **−50 to +50 cm** in 1 cm steps. Positive values raise the avatar; negative values lower it. Changes apply live and save when dragging ends; **Reset both to 0** restores automatic placement. Standing covers ordinary movement; sitting covers ground/chair/ship sitting and getting into/out of ground sitting. Sleeping and ragdolls retain their existing placement.

These optional adjustments are saved per model in `BepInEx/config/ValheimVRM/settings_ModelName.txt` as `StandingHeightOffset` and `SittingHeightOffset` (meters). Saving creates the file when needed and preserves other parameters/comments. They affect this computer's rendering and are not sent by server sync. Existing `ModelOffsetY` still adds a global offset. Use the sliders for unusual footwear or authored shapes; they do not replace automatic grounding or add leg IK to fit every chair.

### Physics weight

The slider ranges from **0% to 100%**, default **50%**. Zero shows no spring rotation; 100% retains the exported motion. Changes apply immediately, save when dragging ends, and persist across switches and restarts.

This controls exported hair, clothing and body spring motion in VRM 0.x and VRM 1.0 while preserving authored stiffness, gravity, damping and collisions. It cannot create missing physics or directly run VRChat PhysBone components. Clothing/body clipping also depends on skinning, blend shapes and collider setup; lowering weight does not repair the source asset.

### Rendering controls

These three controls apply to **VRM 0.x and VRM 1.0 MToon materials**, including avatars and death ragdolls rendered on this client:

| Control | Default | Effect |
| --- | --- | --- |
| Scene lighting | On | Respond to sunlight/local lights; off displays base color and emission. |
| Receive shadows | On | Sample shadow maps; off retains light direction, point-light attenuation and shadow casting onto the scene. |
| Avatar bloom | Off | Allow the avatar surface to contribute to bloom; disabling this preserves scene, fire and weapon bloom. |

Disabling scene lighting temporarily disables the shadow control while remembering its state. Restoring them restores the original shader and imported brightness baseline. Controls do not rewrite VRMs or change global graphics settings.

Since 1.8.13, legacy MToon uses the same controls and no longer multiplies material colors by global sun/ambient light a second time. Standard, non-MToon and third-party shaders retain their own rendering behavior. Bloom from other objects and screen-space effects may still overlap avatars. Semitransparent clothing can also have transparency-sorting issues.

**1.8.8 fixes jagged gaps in close-fitting transparent clothing/stockings with game antialiasing enabled.** TAA uses matching projection jitter for opaque depth, transparent clothing and bloom coverage when an avatar is visible; the camera state is restored afterward. The viewing client needs the update. Materials, opacity and game antialiasing preferences are preserved.

The same release fixes alternating bloom-mask dropouts when **F8 avatar bloom is off and game antialiasing is on**, verified with consecutive HDR frames. Scene lighting can still make the model bright; this option excludes bloom rather than reducing its lit color.

## Adjustable model height

VRM 0.x and VRM 1.0 share the same height range/default, skeleton/sole calibration,
sitting offsets, equipment alignment and per-player height synchronization. These
use each avatar’s humanoid skeleton and geometry, without model-name or exporter-specific exceptions.

F8 → **Model height** sets an absolute visual height from **1.4 to 2.2 m**, in 1 cm steps, default **2 m**. Release the slider to apply; **Reset height to 2 m** restores the default. The setting follows the local game character across model changes and restarts, stored in `BepInEx/config/ValheimVRM/avatar_heights.json`.

Standing visible mesh dimensions are measured once during import, before animation, including hair, ears and headwear. The character's clone uses `scale = selected height / original mesh height`. Measurable models both grow and shrink to the selected target; `ModelScale` no longer overrides it. Unmeasurable geometry retains a safe scale fallback.

Changing height reattaches a fresh clone of the cached model. The same initialization recalibrates humanoid hip/sole clearance, seated contact geometry, camera height, equipment and spring references at the new scale. It does not reload model bytes or resize the shared import or other players. Standing/walking/running retain a constant calibrated lift and the native hip animation; no accumulated or foot-driven gait correction is added. Ground/chair sitting retains its separate animated contact handling. Small pose-dependent sole gaps/intersections can remain; this is not per-foot terrain IK.

**Height synchronization requires server and viewing/sending clients 1.8.12 or newer.** A = 1.4 m and B = 2 m appear at those respective heights, even with the same VRM file. Height travels with the model choice under the authenticated player/character identity and the same increasing request sequence. Late packets cannot undo newer height choices. Respawns, late joins and model changes retain the sender's height.

Without the server addon, or with synchronization disabled, height works locally. Older server/client versions retain model synchronization but cannot display custom remote heights; old senders default to 2 m on updated viewers. F8 reports whether height synchronization is available. Personal standing/sitting offset sliders remain local per-model adjustments and are separate from the synchronized height.

## Model brightness reference

The calibrated Shinano variants and KUMALY 2 use the following **exported
VRM 1.0 / MToon material values** as their brightness reference. These privately
supplied models are not included in the public plugin release.

| Material parameter | Calibrated reference |
| --- | --- |
| Base color (`pbrMetallicRoughness.baseColorFactor`, RGB) | **0.45 per channel** for a neutral white tint; colored tints may use lower channel values. Preserve the original alpha. |
| Shade color (`VRMC_materials_mtoon.shadeColorFactor`, RGB) | **0.2025 per channel** for a neutral tint; colored shade values may be lower. |

At import, if the largest linear RGB channel exceeds its reference, the plugin
scales that entire RGB color down proportionally. Base color is limited to
**0.45** and shade color to **0.2025**, independently. For example, base RGB
`(1, 0.5, 0.2)` becomes `(0.45, 0.225, 0.09)`. Alpha is unchanged, and values
at or below the reference are preserved exactly. Already calibrated models do
not get darker on another import, model switch or rendering-option toggle.
The limit is applied before UniVRM captures the default expression colors.

Higher base-color values can cause washed-out skin, hair or clothing in older
clients. The new import limit reduces that risk. Emission, matcap/rim effects,
lights, animated color overrides and game post-processing still affect the final
image; this does not clamp final pixel brightness. The limiter applies to
**VRM 0.x and VRM 1.0 MToon** materials, including remote avatars loaded from matching local
files; Standard and non-MToon shaders retain their own behavior.

The reference is in linear space. VRM 1.0 stores linear factors, whereas VRM 0.x
MToon stores sRGB color vectors. The importer applies the same linear limits after
accounting for that difference; 0.45/0.2025 are not equivalent raw sRGB values. Original `.vrm` files,
textures and synchronization hashes are unchanged. This is separate from the
legacy `ModelBrightness` setting in `settings_ModelName.txt`, which is not applied
to either MToon generation; existing model settings can keep `ModelBrightness=1`.

## Server synchronization

### Server setup

Install BepInEx 5 on the dedicated server, stop it, extract the complete `ValheimVRM-Server-1.8.13.zip` into its root and start it normally:

```text
Dedicated server/
  valheim_server.exe
  BepInEx/plugins/ValheimVRM.Server/ValheimVRM.Server.dll
  ValheimVRM.Server/              (server documentation and license)
```

**Neither `ValheimVRM.Server` directory needs client avatar files.** The server does not import models and needs no client UniVRM DLLs or shaders. Its own `ValheimVRM` folder can be absent.

Participating players install the complete client and the matching `.vrm` files they want to display. Join a world, enable sync in F8 and confirm **Server sync connected**. Use **1.8.13** on the server and participating clients for consistent current behavior.

### Installation combinations

| Server | Player client / models | Result |
| --- | --- | --- |
| No server addon | Client addon installed | Automatic local mode; changes local visuals without sending avatar-selection requests. |
| Server addon installed | No client addon | Normal admission, vanilla visuals, no avatar synchronization. |
| Server addon installed | Client addon installed, sync enabled | Synchronize each player's selection independently. |
| Server addon installed | Missing files, different folders or different same-name contents | Admission is unaffected. Skip that unavailable update and retain the player's last usable appearance, or vanilla on first load. |
| Server addon installed | Client opts out | Keep the client's own avatar locally, withdraw its public selection and restore remote players to vanilla on that client. |

Whole folders need not match. Each model being displayed needs the **same case-sensitive filename and SHA-256 content hash**. The server relays names/hashes, never VRM bytes. Add missing unimported files and refresh to retry; restart after replacing a cached model. Personal rendering, physics weight and model TXT settings are not synchronized.

### Player isolation and request ordering

If A selects model 1 and B selects model 2, participating viewers see A = model 1 and B = model 2. Choices are bound to connections and character network IDs, not nicknames. Identical nicknames/model choices, respawns, reconnects and late joins retain their own associations.

With **1.8.3 or newer on both sender and server**, F8 shows **Request order protection active**. Each actual request carries an increasing sequence: if request 2 arrives before request 1, late request 1 and duplicate request 2 cannot overwrite the final choice. Opt-out is ordered too; downstream snapshots compare server revisions. This uses connection-scoped counters, not computer clocks or game frames.

Protocol compatibility with 1.8.0–1.8.2 remains, but older peers lack the newer upstream ordering protection; new clients show a compatibility notice. Normal game-version, password and identity checks remain in force. VRM never requires players to install its client addon.

| Configuration file under `BepInEx/config` | Default |
| --- | --- |
| `com.celestetwinkle.valheimvrm.server.cfg` | `[Sync] Enabled = true` |
| `com.yoship1639.plugins.valheimvrm.cfg` | `[AvatarSync] Enabled = true`, also controlled by F8. |

Keep the old whole-file sharing option `EnableLegacyVrmSharing=false` in `global_settings.txt`. The protocol tracks at most 128 active synchronized players. See the [server sync guide](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/SERVER-SYNC.md) for further boundaries.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| F8 will not open | Enter a world, stay alive, close other menus, ensure `EnableAvatarPicker` is not false, and check plugin loading logs. |
| Empty list / new model missing | Put `.vrm` files directly in the model directory; Unity packages cannot load directly. Refresh, check custom launch scripts' working directory, and inspect scan errors in the log. |
| Missing `settings_*.txt` or JSON | Loading still works. TXT is optional; saving F8 height offsets creates the current model TXT. JSON is generated when its corresponding choice is saved. |
| Friends cannot see my switch | Check the server addon, both clients' F8 sync status and the receiver's matching model name/content. The server does not download models to players. |
| Version-incompatible login dialog | Compare client/server game versions and inspect both connection logs. VRM does not reject clients missing its addon. |
| Excessive motion / clothes clip through the body | Reduce physics weight, then inspect exported skinning, blend shapes and colliders if necessary. |
| Leaf-like dark patches remain after disabling shadows | Shadow maps and screen-space post-processing differ. This build supplies depth/normals for opaque/cutout MToon surfaces. Check complete dependencies and include material types/rendering options in reports. |
| Increasing lag or GPU allocation failures after leaving a world | Install complete **1.8.13**, remove duplicate plugin copies and restart. This version fixes the duplicate-patch leak on menu reentry. |
| Memory does not drop when an avatar leaves the camera view | Off-camera active players still need their assets. Eviction begins after the last instance is destroyed; see below. |

For reports, include game/mod versions, reproduction steps and relevant log excerpts:

- `<game directory>/BepInEx/LogOutput.log`.
- Windows: `%USERPROFILE%/AppData/LocalLow/IronGate/Valheim/Player.log`, plus `Player-prev.log` from before restarting if present.
- For networking, include the server addon version and corresponding connection logs. Remove passwords and access tokens.

## Memory use and validation

Imported templates and their meshes, textures, materials and rig resources are released about **15 seconds** after the last avatar instance is destroyed. Shared models stay loaded until their final user leaves; off-camera players still in the scene and pending attachments retain their assets. Selecting an evicted model imports it again. Normal local/server-sync mode does not retain source-file byte arrays permanently.

Unity and graphics drivers can retain memory pools, so Task Manager need not drop immediately after resource release. 1.8.7 passed sustained 4K bloom allocation/release, three actual menu reloads, attachment cancellation, minimum sizing, sitting, grips, physics weight and resource-lifetime probes.

Networking probes cover real ZRpc serialization, production server handlers and independent binding to two player fixtures. A public-network Steam/PlayFab dedicated-server session has not been verified. Linux, macOS, Vulkan and all other mod combinations have not been comprehensively tested. See the [1.8.13 VRM 0.x/1.0 validation](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.8.13-validation.md), [1.8.12 height/sync validation](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.8.12-validation.md) and the earlier [1.8.7 validation record](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/docs/release-1.8.7-validation.md).

## Development and provenance

**Release implementation lives on `codex/public-release`.** The default `main` README describes the current Release; it does not mean `main` contains that implementation. Switch to the release branch before building. `codex/valheim-1.0-compatibility` remains the source branch for the upstream compatibility PR.

Use the .NET 7 SDK, an installed game and BepInEx. The plugin targets .NET Framework 4.7.1:

```powershell
git switch codex/public-release
$env:VALHEIM_INSTALL_PATH = 'C:\Games\Valheim'
powershell -NoProfile -File tools/Test-RuntimeDependencies.ps1 -ValheimPath $env:VALHEIM_INSTALL_PATH
dotnet build ValheimVRM.csproj -c Release
dotnet run --project tests/AvatarCatalogTests
dotnet run --project tests/AvatarSyncTests -c Release
powershell -NoProfile -File tools/Build-ServerPackage.ps1 -ValheimPath $env:VALHEIM_INSTALL_PATH
```

Outputs are `release/ValheimVRM-1.8.13.zip` and `release/ValheimVRM-Server-1.8.13.zip`. The client build cleans the release directory, so build it before packaging the server. Builds install into the game only with explicit `-p:InstallToGame=true`. Use a full build to embed rendering resources; `-t:Compile` alone is not a distributable build.

[Shader rebuilding](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/shaders/README.md) · [Dependency sources/licenses](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/Libs/README.md) · [Project license](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/LICENSE) · [Issues](https://github.com/Celeste-twinkle/valheim-vrm/issues)

Fork history: [yoship1639](https://github.com/yoship1639/ValheimVRM) → [aMidnightNova](https://github.com/aMidnightNova/ValheimVRM) → [nyaarium](https://github.com/nyaarium/valheim-vrm) → **Celeste-twinkle**. Thanks to the original author and maintainers. This fork includes the compatibility changes in [upstream PR #53](https://github.com/nyaarium/valheim-vrm/pull/53) and subsequent independent features.
