## 1.8.1 — Optional synchronization for mixed client servers

- Stop discovery after three unanswered messages. Unmodded clients can join and
  use normal game RPCs without installing ValheimVRM; they receive no avatar snapshots.
- Keep admission, version checks and disconnection behavior unchanged. Preserve
  protocol compatibility with 1.8.0 clients and independent per-player selections.
- Document mixed client servers in the English and Chinese READMEs.

Install the complete client package when updating a client, and update the separate
server ZIP on servers. No model files are included in the public Release.
See `docs/release-1.8.1-validation.md` and `docs/SERVER-SYNC.md`.

服务器允许未安装 Mod 的玩家正常加入；他们显示原版角色，不参与模型同步。
服务端最多探测三次，不向未握手客户端发送外观快照，不增加强制安装或踢人规则。
保留 1.8.0 的玩家独立同步与 F8 物理权重功能。

## 1.8.0 — Per-player server synchronization and adjustable physics

- Add an optional, separate server addon. Each client installs the same local
  VRM folder; the server relays model names and SHA-256 fingerprints only.
- Bind each selection to an authenticated connection and its owned character ZDO.
  A switching avatars updates only A, even when players have identical names or
  choose the same asset. Support late joins, respawns and reconnects.
- Keep local selection when the server addon is absent. The F8 sync checkbox can
  opt out and restore remote vanilla visuals. Missing or different files retain
  the last usable appearance; no avatar bytes are downloaded.
- Disable the legacy whole-file sharing protocol by default and keep remote
  cosmetic changes from resizing player collision or changing interaction range.

- Add a bilingual **Physics sway weight** slider to the F8 avatar menu. Changes
  apply immediately and persist in `ValheimVRM/physics_options.json` after dragging
  or closing the menu. The default is **50%**; **0%** removes spring rotation from
  the displayed pose and **100%** retains the original simulation output.
- Support exported VRM 1.0 and VRM 0.x spring chains without avatar-specific bone
  names. Blend rotations after simulation and restore the unweighted state before
  the next frame, preserving authored stiffness, gravity, drag and collision data.
  Garment constraints downstream of the springs follow the reduced rotation.
- Keep the camera, sitting, hand attachment and ambient-occlusion fixes from 1.7.2.

Install the complete `ValheimVRM-1.8.0.zip` with the game closed. For shared-folder
synchronization, install `ValheimVRM-Server-1.8.0.zip` on the server as well. See
`docs/SERVER-SYNC.md` for setup and optional local mode. Existing settings
and avatar files remain compatible. See `docs/release-1.8.0-validation.md`.

新增可选服务器同步：每位玩家独立选择外观，服务器按连接身份与角色网络 ID 同步。
其他客户端需要相同模型文件；未装服务器插件时仍可仅本地使用。

F8 菜单新增“物理摆动权重”滑块，默认 50%，支持 0%～100% 即时调节并保存。
头发、衣服、身体等已导出的弹簧物理使用统一权重，兼容 VRM 1.0 和 VRM 0.x。
此公开 Release 仅包含插件与源码，不包含私人角色模型。

## 1.7.2 — Ground sitting, held items, spring physics and avatar occlusion

- Prevent ground-sitting poses from burying differently proportioned avatars.
  A small cached set of sole vertices supplies a pose-dependent vertical correction;
  standing, chair, ship and bed placement retain their existing behavior.
- Calibrate both equipment mounts from humanoid wrist/finger bind poses and palm
  proportions. Attachments follow the final VRM pose after constraints, including
  model switches and scaling. No model-specific bone names or offsets are used.
- Restore VRM 1.0 spring chains, collision groups and simulation centers when
  cloning the imported model. Remap references to the clone, restore its rest pose
  and initialize physics at the player's location. Legacy springs run after
  animation and retain their authored center.
- Fix background shapes appearing on opaque/cutout MToon surfaces through the
  game's Amplify Occlusion post-effect. Supply avatar depth, normals and neutral
  material data before deferred lighting while retaining the authored forward
  shading. Transparent overlays, HDR brightness and scene ambient occlusion remain.

Includes the 1.7.1 camera jitter fix. Install the complete `ValheimVRM-1.7.2.zip`
with the game closed; models and existing settings do not need to be regenerated.
See `docs/release-1.7.2-validation.md` for engine tests and their scope.

修复坐地下陷、左右手持物脱离手掌、VRM 1.0 头发／胸部等弹簧物理不动，
以及游戏环境遮蔽把模型后方轮廓叠到身体上的问题。握持点按人形骨骼和手掌
比例自动校准，保留模型已有物理参数。包含此前镜头抖动修复；退出游戏后
更新完整 1.7.2 插件包即可，模型与个人设置无需重做。

## 1.7.1 — Stable camera height after avatar switching

- Fix close-range camera jitter caused by feeding animated head/eye positions
  into Valheim's camera collision origin every frame.
- Calibrate camera height once from the selected VRM clone's rest pose, including
  model scale and ModelOffsetY. The game retains its normal camera collision logic.
- Restore the original eye position before another avatar binds, when the new
  model disables FixCameraHeight, and when the component is disabled or destroyed.
- Select eye/head/neck bones from the new VRM animator rather than the first
  animator under the player, which can belong to the vanilla model.

Windows x64 / Valheim 1.0.7 / BepInExPack Valheim 5.4.2333. Models and existing
settings do not need to be regenerated. Close the game and install the complete
ValheimVRM-1.7.1.zip. See docs/release-1.7.1-validation.md for the regression
results and the limits of the automated engine probe.

修复切换模型后镜头贴近实体时的抖动：镜头高度按新模型的初始姿态校准，
不再每帧追随头骨动画。切换模型或关闭 FixCameraHeight 时恢复原始视点，
支持模型缩放与 ModelOffsetY。退出游戏后安装 1.7.1；模型和个人设置无需重做。

## 1.7.0 — Celeste-twinkle fork, Valheim 1.0.7

Independent Windows x64 release with the Valheim 1.0 import/lifecycle fixes,
F8 avatar picker, per-character saved selections, and VRM 1.0 MToon rendering controls.
The picker scans all top-level .vrm files instead of a fixed set of outfits.
Lighting and received shadows default to on; avatar bloom defaults to off.
Brightness ceilings and highlight compression are absent.

Requires BepInExPack Valheim 5.4.2333. Install the complete release ZIP into the
Valheim game directory. Models are not included. See docs/INSTALL.md and
INSTALL.zh-CN.md for details and supported-material/multiplayer limitations.
## Update 1.6.0

**BepInEx:** 5.4.23.3

**Valheim:** 0.221.4 (n-35)

### Fixes

- Fixed a name casing mismatch for Linux, where the code was trying to load `UniVRM.shaders` with the name `UniVrm.shaders`. *(should fix #28, but didn't confirm)*

### 🎉 New Parallel Texture Loading System

Rewrote the entire texture loading system to skip multiple buffer copies and cache reused textures. When using in-game Valheim shader, you may see load improvements by **x100 or more**. (fixes #26)

The results are so impressive that I'm making it final. `AttemptTextureFix` is no longer a weird *"attempt"*. It's a finished feature, so heres the options to pay attention to:

- ~~`AttemptTextureFix`~~ flag has been **removed**
- `UseMToonShader=false` is the new setting to use the in-game Valheim shader (try it!)
- `UseMToonShader=true` for traditional VRM MToon unlit shader

In *my avatar's* tests with `UseMToonShader=false`:

- Version 1.5.1 material load time: **71.84 seconds**
- Version 1.6.0 material load time: **0.02 seconds**

### Logs in old 1.5.1:
```
🖌️ Processing 15 materials for "Nyaa" VRM  |  UseMToonShader False  |  AttemptTextureFix True
🖌️ Converted "Eve Sweater Atlas (Instance)" in 6.65 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Eve Sweater Atlas Refract (Instance)" in 6.50 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Eve Sweater Atlas Metal Silver (Instance)" in 6.47 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Palette Metal Gold (Instance)" in 0.09 seconds
    "Palette 16x16"  |  16x16  |  ARGB32
🖌️ Converted "Gem Red (Instance)" in 0.01 seconds
    "Palette 16x16"  |  16x16  |  ARGB32
🖌️ Converted "Palette Metal Silver (Instance)" in 0.01 seconds
    "Palette 16x16"  |  16x16  |  ARGB32
🖌️ Converted "Gem Clear (Instance)" in 0.00 seconds
    "Palette 16x16"  |  16x16  |  ARGB32
🖌️ Converted "Eve Sweater Atlas Metal Gold (Instance)" in 6.65 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Eve Sweater Atlas Metal Red (Instance)" in 6.48 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Eve Sweater Atlas Outlined (Instance)" in 6.38 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Eve Sweater Atlas Hosiery (Instance)" in 6.66 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Eve Sweater Atlas Expressions (Instance)" in 6.50 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Eve Sweater Atlas (Instance)" in 6.43 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Eve Sweater Atlas Eyes (Instance)" in 6.45 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Converted "Eve Sweater Atlas Outlined (Instance)" in 6.39 seconds
    "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
    "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
🖌️ Finished processing 15 materials for VRM 'Nyaa' in 71.84 seconds
```

### Logs in new 1.6.0:
```
[Info   : Unity Log] [VrmTextureCache] 💽 Computed VRM hash for 'Nyaa': 18ed5641e5888190d219a9b8bc709167ef3304a635133b6de1fe892c48f98be0
[Info   : Unity Log] [ValheimVRM Async] loading vrm: 33047024 bytes
[Info   : Unity Log] [VrmTextureCache] 💽 Cache MISS - (8192x8192, ARGB32, srgb, 18035262 bytes)
[Info   : Unity Log] [VrmTextureCache] 💽 Cache MISS - (4096x4096, ARGB32, linear, 2300689 bytes)
[Info   : Unity Log] [VrmTextureCache] 💽 Cache MISS - (256x256, ARGB32, srgb, 54254 bytes)
[Info   : Unity Log] [VrmTextureCache] 💽 Cache MISS - (16x16, ARGB32, srgb, 667 bytes)
[Info   : Unity Log] [VrmTextureCache] 💽 Cache MISS - (256x256, ARGB32, srgb, 126690 bytes)
[Info   : Unity Log] [VrmTextureCache] 💽 Cache MISS - (256x256, ARGB32, srgb, 108585 bytes)
[Info   : Unity Log] [VrmTextureCache] 💽 Cache MISS - (512x512, ARGB32, srgb, 511452 bytes)
[Info   : Unity Log] [ValheimVRM] VRM read successful
[Info   : Unity Log] [VrmTextureCache] 💽 RegisterVrm started for 'Nyaa' with hash: 18ed5641e5888190d219a9b8bc709167ef3304a635133b6de1fe892c48f98be0
[Info   : Unity Log] [VrmTextureCache] 💽 RegisterVrm: Created state for Nyaa
[Info   : Unity Log] [ValheimVRM] 🖌️ Processing 15 materials for "Nyaa" VRM  |  UseMToonShader False
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas Refract (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas Metal Silver (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Palette Metal Gold (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Palette 16x16"  |  16x16  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Gem Red (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Palette 16x16"  |  16x16  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Palette Metal Silver (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Palette 16x16"  |  16x16  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Gem Clear (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Palette 16x16"  |  16x16  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas Metal Gold (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas Metal Red (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas Outlined (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas Hosiery (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas Expressions (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas Eyes (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Converted "Eve Sweater Atlas Outlined (Instance)"
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.color"  |  8192x8192  |  ARGB32
[Info   : Unity Log] [ValheimVRM]     "Eve Sweater Atlas.normal"  |  4096x4096  |  ARGB32
[Info   : Unity Log] [ValheimVRM] 🖌️ Finished processing 15 materials for VRM 'Nyaa' in 0.02 seconds
```

## Update 1.5.1

**BepInEx:** 5.4.23.3

**Valheim:** 0.221.4 (n-35)

Fixed the custom texture loader to handle unusual textures *(like indexed color)*.

## Update 1.5.0

**BepInEx:** 5.4.23.3

**Valheim:** 0.221.4 (n-35)

When loading VRMs, progress will now be displayed in the top-left message HUD, telling you the amount of time taken to load the VRM.

Disabled the custom texture loader for indexed color textures. It broke with the latest Valheim update. *(re-enabled in 1.5.1)*

~~Match the .NET version to what Valheim uses, 4.8 (i think?).~~ *(nvm, the build error was something else. Reverting this in 1.5.1)*

## Update 1.4.2

This patch fixes a black-screen freeze when you die. (fixes #20)

Bug fixes by PR #19 revealed a pre-exising race condition when the ragdoll is created (in `Patch_Humanoid_OnRagdollCreated.Postfix`). It was a rare freeze before when it was syncronous. But now that it's asycronous, it always happens.

## Update 1.4.1

Fixed `AttemptTextureFix` feature that converted VRM shader to in-game shader (fixes #18):

*Side note:* I will eventually rename this option to be clear what this option even does.

## Update 1.4.0

*Major Project Restructure:** Much cleanup to the csproj file. And added support for Linux development environments and GitHub runners.

Added null-check in `Patch_Player_Awake.Postfix` to only add `VrmController` if not already present. (fixes #1)

Replaced unsafe `Substring` prefix checks with null-safe `StartsWith` in `Patch_VisEquipment_UpdateLodgroup.Postfix`. (fixes #2)

Implemented Harmony patch to skip `VRM.VRMBlendShapeProxy.OnDestroy` entirely, avoiding Editor assembly reference during disconnect. (fixes #3)

Added post-yield null-guards for `player`/`vrmModel` and re-fetched the `Animator` before camera-height step in `VRM.SetToPlayer`. (fixes #4)

Made `VRMShaders.Initialize()` idempotent with early-return after first call, then call `assetBundle.Unload(false)` to release bundle reference. (fixes #5)

Implemented ragdoll pose mirroring for VRM visibility during physics-driven ragdoll. (fixes #6)
- Parent VRM to ragdoll on `Humanoid.OnRagdollCreated` and keep VRM renderers enabled
- Hide vanilla ragdoll renderers to avoid double visuals
- In `VRMAnimationSync`, copy human bone positions/rotations from ragdoll animator to VRM every LateUpdate when in ragdoll mode (with existing model Y-offset)
