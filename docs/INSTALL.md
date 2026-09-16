# Install ValheimVRM 1.8.19 (Celeste-twinkle fork)

**1.8.19 expands every offset slider to −100 to +100 cm per axis**, with 1 cm steps and default zero. This covers standing/sitting height, animation state/clip XYZ, and left/right/two-handed/back equipment XYZ. Existing saved values are retained. To synchronize offsets beyond ±50 cm, update sender, observers and server addon to 1.8.19 or newer; older versions may reject or clamp extended calibration. Model-height and item-scale ranges are unchanged.

For optional per-player multiplayer appearance, also install the separate server
addon. See [server setup and local fallback](SERVER-SYNC.md).

Windows x64 client release, tested with Valheim 1.0.12, Unity 6000.0.75f1 and
BepInExPack Valheim 5.4.2333 (BepInEx 5.4.23.3). This is an independent fork
release, not a release by the upstream maintainer. No avatars are included.

Prerequisite downloads: [Valheim-specific BepInEx pack (recommended)](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) · [BepInEx 5.4.23.3 Release](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.3) · [All BepInEx releases](https://github.com/BepInEx/BepInEx/releases). The client ZIP includes its matching UniVRM libraries and shaders. Public runtime ZIPs require a separate loader installation; the local Windows x64 bundles numbered 09 and 10 include BepInEx 5.4.23.3.

## Install or upgrade

1. Close Valheim. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/5.4.2333/)
   if BepInEx is not already installed. Start the game once and close it.
2. Back up an existing ValheimVRM installation and its settings. Keep only one
   `ValheimVRM.dll` inside `BepInEx/plugins`; remove an older duplicate plugin
   folder before extracting this release. Keep your `.vrm` files and settings.
3. Extract `ValheimVRM-1.8.19.zip` directly into the folder containing `valheim.exe`.
   Merge its `BepInEx` and `valheim_Data` folders. Use the complete
   package: replacing only the plugin DLL does not fix mismatched UniVRM libraries.
4. Put your own `.vrm` files directly in the `ValheimVRM` folder beside the game.
   Subfolders, including `Shared`, are not listed. Spaces and Unicode names work.
5. Enter a world and press **F8**. Select a model from the scrollable list.
   **Refresh list** discovers added or removed files; F8 or Esc closes the panel.

```text
Valheim/
  valheim.exe
  BepInEx/plugins/ValheimVRM/ValheimVRM.dll
  BepInEx/plugins/ValheimVRM/UniVRM.shaders
  valheim_Data/Managed/VRM10.dll          (and the other supplied libraries)
  ValheimVRM/My Avatar.vrm               (your own model)
  BepInEx/config/ValheimVRM/            (created when saving personal options)
```

The list has no eight-model limit. Loading is asynchronous; an invalid file shows
an error and leaves the previous avatar in place. Selections are saved per character
in `BepInEx/config/ValheimVRM/avatar_selections.json` and restored on respawn and restart.
If a selected file disappears, normal character-name/default matching is used.
Restart the game after replacing a model's contents; loaded models are cached.

Use **Physics sway weight** in F8 to reduce excessive spring motion. Default: 50%.
0% removes spring rotation and 100% retains the original motion. It applies to
VRM 1.0 and VRM 0.x and saves to `BepInEx/config/ValheimVRM/physics_options.json` after dragging.

Changing appearance keeps items equipped and preserves their armor/damage stats.
Existing per-model settings can still change collider size, interaction distance,
weapon placement and equipment visibility. Switching does not edit character saves.

The first F8 list entry is always **Original character model**, available even with no VRM files. It restores the native body, current gear, item mounts, collider and camera baseline. The choice is saved per game character and suppresses character-name/default VRM fallback after respawn or restart. Server sync stays enabled: other viewers see the native character, and selecting a VRM publishes it again.

**Back equipment (optional adjustment)** is collapsed by default under **Held item calibration**. It adds an independent **25–200% uniform scale** and **−100 to +100 cm X/Y/Z offsets**, default 100%/0. Normal drawing/sheathing needs no manual adjustment; these controls accommodate different back proportions or thick clothing. Sheathed items retain the game's original attachment transforms and item offsets, scale with height, and apply absolute values without repeated rotation. Back controls share the owning player's sequenced calibration; sender and viewer need 1.8.15+, while a 1.8.14 server can relay values within its old ±50 cm limits.

F8 **Animation position calibration** exposes searchable state/clip foldouts with
XYZ offsets (−100 to +100 cm each). Loading/height changes calibrate fixed standing,
seated, swimming, gripping, riding and reclining references. Playback reads the
cache and blends transitions without live sole tracking or accumulated corrections.

**Held item calibration** has separate left/right/two-handed 25–200% uniform scale
and XYZ adjustments. Palm bones determine grips; original item size follows
height / 2 m before the selected group's multiplier. Two-handed items use only
their own group. Authored attachments are preserved; missing fingers use a wrist
fallback. Preferences preview live and save on release in
BepInEx/config/ValheimVRM/avatar_calibration.json. Server, sender and viewer 1.8.15+ share all these
controls, standing/sitting offsets, physics weight and height per player. Remote
instances use the sender's values without writing the viewer's preferences. Manual
changes update in place after release; model/height changes rerun fixed calibration
from the original reference. F8 reports whether full calibration sync is active.
Fixed references may retain small intersections/gaps; no additional hand/foot IK.

VRM 1.0 spring chains and their collision groups are retained on player clones,
so exported hair, clothing and body springs can move. The plugin preserves the
exported parameters; it cannot reconstruct VRChat PhysBones omitted from the VRM.

## Rendering panel

Lighting and shadow controls apply to **VRM 0.x and VRM 1.0 MToon materials** on this client; bloom exclusion also covers the common surfaces described below, including newly
selected models and ragdolls. Defaults match the maintained local build:

| Control | Default | Effect |
| --- | --- | --- |
| Scene lighting | On | Use the imported material with scene lights. Off shows its base color and emission without scene-light shading. |
| Receive shadows | On | Use normal shadow reception. Off bypasses shadow maps while retaining light direction, light cookies and point-light distance attenuation. It does not disable shadow casting. |
| Avatar bloom | Off | Exclude the model's visible surface from the game's bloom input. World/fire/weapon bloom remains enabled. |

The options are saved immediately to `BepInEx/config/ValheimVRM/rendering_options.json`.
Since 1.8.5, MToon10 imports cap linear base RGB at **0.45** and shade RGB at
**0.2025**: only colors exceeding their reference are scaled down proportionally;
lower/equal values and alpha remain unchanged. No F8 adjustment is needed.
The original VRM files and sync hashes are preserved. This limits imported color
factors, not the final brightness from emission, animated overrides or lighting.
See [model brightness reference](../README.md#model-brightness-reference).
Lighting and shadows on restores the original shader with the import limits intact.
Turning off lighting also removes received shading; the shadow preference is retained
for when lighting is turned back on. Bloom originating elsewhere can still overlap
the avatar, and screen-space post-processing remains controlled by the game.
Opaque/cutout MToon avatars now contribute their own deferred depth and normals,
preventing background ambient-occlusion silhouettes from being applied to their
surface. This compatibility pass does not turn off the game's ambient occlusion.

Since 1.8.13, VRM 0.x MToon shares the brightness limit and all three rendering
controls, without a second global sunlight color multiplier. Standard and
non-MToon shaders retain their own rendering behavior. The panel does not alter the source `.vrm` file or global graphics
settings. Rendering in another VRM viewer is determined by that viewer.

## VRM-only model folder

Only `.vrm` files are needed in `ValheimVRM`. Models need no TXT sidecar, manifest,
fixed companion models, `___Default.vrm` or initialization script. Built-in
appearance defaults are brightness 1.0, MToon enabled and player fade disabled.
F8 model height is 1.4–2.2 m, default 2 m. Release the slider to reattach the
cached model at `selected height / original mesh height`, recalibrating standing
and seated placement. Geometry is measured once before animation; the shared
import remains unchanged. For measurable models, `ModelScale` does not override
the height slider. Server and participating clients 1.8.12+ synchronize height
per player; no server addon is needed for local height adjustment.

The mod creates its configuration directory and these files on demand:

- `avatar_selections.json`: after selecting a model.
- `avatar_heights.json`: after applying height; stored per local game character.
- `physics_options.json`: after saving a physics slider adjustment.
- `rendering_options.json`: after changing a rendering option.

These files go under `BepInEx/config/ValheimVRM`. Missing files use defaults.
Since 1.8.6, old model-folder configurations and `selected_models.json` are ignored;
there is no automatic migration. Move any wanted TXT files and the three current
JSON files into the new configuration folder before upgrading. Documentation,
licenses and example TXT files are under `BepInEx/plugins/ValheimVRM`.

## Optional settings and multiplayer

The optional per-model settings filename is `settings_<model filename without .vrm>.txt`.
This is a plain text file, not a Unity/VRM export; the plugin does not generate it.
Missing settings files use built-in defaults and do not prevent loading, switching
or server synchronization. To create one, copy
`BepInEx/plugins/ValheimVRM/settings_Example.txt.example`. For `MyAvatar.vrm`, name the copy
`settings_MyAvatar.txt` and place it in `BepInEx/config/ValheimVRM`.
Edit `Name=Value` lines in a text editor, for example `ModelScale=1.0`,
`ModelOffsetY=0` or `ModelBrightness=1`; omitted values use defaults. Show file
extensions to avoid `.txt.txt` or a remaining `.example` suffix. Check copied
model-specific offsets and scale. Save and restart the game: F8 **Refresh list**
rescans files but does not reload already-cached model settings.

To use character-name matching without the picker, use `<Character Name>.vrm`.
The fallback model is `___Default.vrm` (three underscores); its settings file is
`settings____Default.txt` (four underscores). Examples are supplied, not automatically
installed over your own settings. Set `EnableAvatarPicker=false` in
`BepInEx/config/ValheimVRM/global_settings.txt` to disable F8.

No server installation is required for local appearance. For per-player synchronized
selection, install `ValheimVRM-Server-1.8.19.zip` on a BepInEx 5 server and give each
client identical model files. See [server setup](SERVER-SYNC.md), including the
F8 opt-out switch and client-hosted servers. The legacy whole-file sharing protocol
is disabled by default with `EnableLegacyVrmSharing=false`; keep it disabled when
using this new protocol. Share model files only when their license permits it.

## Troubleshooting and removal

- Press F8 after entering a world, outside chat, inventory and other menus.
- An empty list means no top-level `.vrm` files were found in the game folder above.
- Check `BepInEx/LogOutput.log` for plugin version **1.8.19**, import errors or unsupported shaders.
- If upgrading from a much older UniVRM set, follow [Libs/README.md](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/Libs/README.md).
  Do not overwrite Valheim's own Unity.Burst/Unity.Mathematics libraries with older copies.
- To uninstall, close the game and remove `BepInEx/plugins/ValheimVRM`. Keep your models
  and settings as desired. Do not remove shared libraries required by other mods.

Linux, macOS, Vulkan, multiplayer sharing and combinations with other mods are not
covered by the Windows/D3D11 validation. Report fork-build issues at
[Celeste-twinkle/valheim-vrm](https://github.com/Celeste-twinkle/valheim-vrm/issues),
including the game version, release tag and relevant log excerpt.

1.8.10 fixes continuously rising avatars in 1.8.9 by removing VRM-to-native bone position writes. Model dimensions and contact geometry are cached at import/attachment; each frame applies an independent contact delta to the native animation pose. The minimum stays at 2 m and both F8 posture offsets default to zero.

1.8.12 calibrates a fixed standing/locomotion offset at load from the humanoid reference skeleton and sole geometry. Walking and running keep native hip animation without live lowest-foot correction. Sitting retains independent contact handling; small pose-dependent foot intersections are possible.

**1.8.18 extends the shared post-processing fixes beyond MToon.** `UniGLTF/UniUnlit`, Standard/PBR and ordinary Unlit avatar materials exposing `_MainTex` or `_BaseMap` also participate in surface depth, transparent queue correction and antialiasing/bloom coverage. Missing surface depth can let background cloud/water depth effects cover a solid avatar. The bridge follows the actual Opaque, Cutout or Blend mode, includes enabled UniUnlit vertex alpha, and ignores stored alpha in Opaque mode. Existing PBR deferred lighting buffers are preserved. Original shaders and lighting remain intact; scene-lighting/shadow toggles and MToon brightness limits remain MToon-specific, while bloom exclusion covers these common surfaces. Arbitrary custom displacement, nonstandard opacity calculations and other rendering pipelines need dedicated adapters. Genuine partial transparency still composes with the background in the game's native order; this cannot hide clouds or water that should remain visible through it.

**1.8.16 fixes SSAO depth and composition for transparent materials.** Some exported blended materials use an opaque render queue, causing background AO to darken the body or transparent clothing. The mod repairs such invalid queues so background SSAO finishes before transparent composition. Valid queues, alpha, textures, blending, original depth-write settings and model files are preserved. Surface depth/normals also follow the mode: Opaque ignores alpha, Cutout uses its cutoff, and Blend writes solid surface data only where final sampled alpha is nearly one. Genuine partial transparency and holes still reveal the normally AO-shaded background. Both VRM generations share this handling. The observing player must update the complete client package; a server-only update cannot fix their rendering. Intersecting multilayer transparent meshes still depend on the game's native sorting; this does not implement order-independent transparency.
