# Install ValheimVRM 1.8.3 (Celeste-twinkle fork)

For optional per-player multiplayer appearance, also install the separate server
addon. See [server setup and local fallback](SERVER-SYNC.md).

Windows x64 client release, tested with Valheim 1.0.12, Unity 6000.0.75f1 and
BepInExPack Valheim 5.4.2333 (BepInEx 5.4.23.3). This is an independent fork
release, not a release by the upstream maintainer. No avatars are included.

## Install or upgrade

1. Close Valheim. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/5.4.2333/)
   if BepInEx is not already installed. Start the game once and close it.
2. Back up an existing ValheimVRM installation and its settings. Keep only one
   `ValheimVRM.dll` inside `BepInEx/plugins`; remove an older duplicate plugin
   folder before extracting this release. Keep your `.vrm` files and settings.
3. Extract `ValheimVRM-1.8.3.zip` directly into the folder containing `valheim.exe`.
   Merge its `BepInEx`, `valheim_Data`, and `ValheimVRM` folders. Use the complete
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
  ValheimVRM/settings_My Avatar.txt      (optional)
```

The list has no eight-model limit. Loading is asynchronous; an invalid file shows
an error and leaves the previous avatar in place. Selections are saved per character
in `avatar_selections.json` and restored on respawn and restart. Earlier local-build
selections in `selected_models.json` are migrated when a unique filename matches.
If a selected file disappears, normal character-name/default matching is used.
Restart the game after replacing a model's contents; loaded models are cached.

Use **Physics sway weight** in F8 to reduce excessive spring motion. Default: 50%.
0% removes spring rotation and 100% retains the original motion. It applies to
VRM 1.0 and VRM 0.x and saves to `ValheimVRM/physics_options.json` after dragging.

Changing appearance keeps items equipped and preserves their armor/damage stats.
Existing per-model settings can still change collider size, interaction distance,
weapon placement and equipment visibility. Switching does not edit character saves.

Ground sitting now raises the posed soles to the character's ground plane when
needed. Chair/ship/bed offsets remain separate. Hand attachments are calibrated
from humanoid palm proportions and track the final animated pose; existing item
offsets remain available for individual equipment. Avatars without enough finger
bones use a wrist-following fallback. This does not add two-hand weapon IK.

VRM 1.0 spring chains and their collision groups are retained on player clones,
so exported hair, clothing and body springs can move. The plugin preserves the
exported parameters; it cannot reconstruct VRChat PhysBones omitted from the VRM.

## Rendering panel

These controls apply to **VRM 1.0 MToon materials** on this client, including newly
selected models and ragdolls. Defaults match the maintained local build:

| Control | Default | Effect |
| --- | --- | --- |
| Scene lighting | On | Use the model's authored material with scene lights. Off shows its base color and emission without scene-light shading. |
| Receive shadows | On | Use normal shadow reception. Off bypasses shadow maps while retaining light direction, light cookies and point-light distance attenuation. It does not disable shadow casting. |
| Avatar bloom | Off | Exclude the model's visible surface from the game's bloom input. World/fire/weapon bloom remains enabled. |

The options are saved immediately to `ValheimVRM/rendering_options.json`. There is
**no brightness ceiling or highlight compression**, and no brightness-limit option.
Lighting and shadows on restores the original shader and exported material values.
Turning off lighting also removes received shading; the shadow preference is retained
for when lighting is turned back on. Bloom originating elsewhere can still overlap
the avatar, and screen-space post-processing remains controlled by the game.
Opaque/cutout MToon avatars now contribute their own deferred depth and normals,
preventing background ambient-occlusion silhouettes from being applied to their
surface. This compatibility pass does not turn off the game's ambient occlusion.

VRM 0.x files remain supported by the importer. Their legacy MToon/game shaders,
Standard materials, and third-party shaders are not controlled by these three
VRM 1.0 options. The panel does not alter the source `.vrm` file or global graphics
settings. Rendering in another VRM viewer is determined by that viewer.

## Existing settings and multiplayer

The optional per-model settings filename is `settings_<model filename without .vrm>.txt`.
This is a plain text file, not a Unity/VRM export; the plugin does not generate it.
Missing settings files use built-in defaults and do not prevent loading, switching
or server synchronization. To create one, copy a supplied model's `settings_*.txt`
or `settings_Example.txt.example`. For `MyAvatar.vrm`, name the copy
`settings_MyAvatar.txt` and place it beside the VRM in the game's `ValheimVRM` folder.
Edit `Name=Value` lines in a text editor, for example `ModelScale=1.0`,
`ModelOffsetY=0` or `ModelBrightness=1`; omitted values use defaults. Show file
extensions to avoid `.txt.txt` or a remaining `.example` suffix. Check copied
model-specific offsets and scale. Save and restart the game: F8 **Refresh list**
rescans files but does not reload already-cached model settings.

To use character-name matching without the picker, use `<Character Name>.vrm`.
The fallback model is `___Default.vrm` (three underscores); its settings file is
`settings____Default.txt` (four underscores). Examples are supplied, not automatically
installed over your own settings. Set `EnableAvatarPicker=false` in
`ValheimVRM/global_settings.txt` to disable F8.

No server installation is required for local appearance. For per-player synchronized
selection, install `ValheimVRM-Server-1.8.3.zip` on a BepInEx 5 server and give each
client identical model files. See [server setup](SERVER-SYNC.md), including the
F8 opt-out switch and client-hosted servers. The legacy whole-file sharing protocol
is disabled by default with `EnableLegacyVrmSharing=false`; keep it disabled when
using this new protocol. Share model files only when their license permits it.

## Troubleshooting and removal

- Press F8 after entering a world, outside chat, inventory and other menus.
- An empty list means no top-level `.vrm` files were found in the game folder above.
- Check `BepInEx/LogOutput.log` for plugin version **1.8.3**, import errors or unsupported shaders.
- If upgrading from a much older UniVRM set, follow [Libs/README.md](https://github.com/Celeste-twinkle/valheim-vrm/blob/codex/public-release/Libs/README.md).
  Do not overwrite Valheim's own Unity.Burst/Unity.Mathematics libraries with older copies.
- To uninstall, close the game and remove `BepInEx/plugins/ValheimVRM`. Keep your models
  and settings as desired. Do not remove shared libraries required by other mods.

Linux, macOS, Vulkan, multiplayer sharing and combinations with other mods are not
covered by the Windows/D3D11 validation. Report fork-build issues at
[Celeste-twinkle/valheim-vrm](https://github.com/Celeste-twinkle/valheim-vrm/issues),
including the game version, release tag and relevant log excerpt.
