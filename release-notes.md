## 1.8.18 — Shared material depth fixes, fur lighting and item controls

- Extend the shared depth/normal, alpha-aware bloom and transparent queue repairs to UniGLTF/UniUnlit, Standard/PBR and common Unlit surfaces. Prevent missing avatar depth from exposing background cloud/water effects over solid surfaces, while retaining real transparency, vertex alpha, Opaque alpha semantics and native PBR lighting. Preserve source shaders; custom shader opacity/displacement still needs dedicated support.
- Make optional fur inherit the source cloth's normal map, tangent frame, shading maps, emission, MatCap and rim parameters. Use native VRM 0.x MToon lighting for legacy materials and MToon 1.0 for newer materials. Existing fur VRMs need no re-export; the extension remains an optional MToon-based effect, not a complete lilToon port.
- Apply left/right/two-handed and back-item XYZ offsets in character axes: +X right, +Y up, +Z forward. Preserve automatic palm calibration, height/2m scaling, authored attachment transforms and absolute, non-accumulating updates. Synchronization remains per player; viewers need 1.8.18+ to interpret the changed axes consistently. Existing nonzero offsets may need adjustment or reset.
- Give the F8 menu an opaque dark background and directional labels for XYZ controls.

Validation: `docs/release-1.8.18-validation.md`.

## 1.8.17 — Optional GPU fur and authoring specification

- Read optional `materials[i].extras.ValheimVRM_fur` in VRM 0.x and VRM 1.0. Keep standard MToon fabric as the fallback; embed only parameters and PNG masks in the model.
- Generate bounded fur fins on the GPU while sharing the source mesh, per-player bones and blend shape weights. Follow model height, material color/alpha/UV animation and rendering options.
- Use the local `AVATAR_FUR_ON` macro and a separate lazily loaded `avatar_fur` bundle. Inactive models create no fur rendering resources or per-frame components; release the bundle with its last imported owner.
- Include matching fur geometry in bloom exclusion and preserve the existing TAA projection and transparent AO handling. Fade density with distance.
- Add the complete [fur integration specification](docs/FUR-EXTENSION.md), JSON Schema, embedding tool and engine regression suite. No avatars are included in public packages. Server protocol and admission behavior are unchanged.

Validation: `docs/release-1.8.17-validation.md`.

## 1.8.16 — Transparent material SSAO and compositing

- Repair alpha-blended MToon materials incorrectly placed in opaque render queues, so Valheim applies background SSAO before drawing genuine transparency. Preserve valid queues, alpha, textures, blend/depth-write settings and source files; both VRM 0.x and 1.0 use the same rules.
- Include fully opaque pixels of blended materials in the avatar depth/normal surface pass. Use live sampled alpha and UV animation; leave partial coverage and holes transparent. Opaque mode ignores alpha, while Cutout keeps its authored cutoff.
- Verify partial-transparent composition against a correctly queued reference, including later queue changes, and regress TAA/bloom with real models using original, all-blended and wholly semitransparent materials.
- Apply the fix on the observing client. Server synchronization is unchanged; updating only a server cannot fix a viewer's rendering.

Validation: `docs/release-1.8.16-validation.md`.

## 1.8.15 — Native character selection and back equipment calibration

- Add **Original character model** at the top of F8, including empty model libraries. Restore the native body/current gear, item sockets, camera and physical baseline; persist per character without default-VRM fallback. Withdraw the shared model through the existing sequenced protocol while continuing to view other players.
- Fix sheathed equipment being overwritten during equipment refresh: retain game-authored attachment position/rotation/scale instead of replacing back-item transforms with legacy offsets and a fixed 1/100 scale or accumulating knife/staff rotations.
- Add independent back-equipment 25–200% scale and XYZ ±50 cm controls, including automatic height scaling, reset/persistence and per-player synchronization. Sender/viewer need 1.8.15+; existing 1.8.14 relays preserve the additional values.
- Release the removed avatar's spring references and retain native-state restoration across repeated VRM/native switches. Keep VRM 0.x/1.0 and model/height/calibration player isolation.

Validation: `docs/release-1.8.15-validation.md`.

## 1.8.14 — Synchronized animation and held-item calibration

- Add F8 animation foldouts with XYZ offsets, search and active-state filtering. Validate 172 native character states and 434 clip names from Valheim 1.0.12; blend clips have individual weighted adjustments.
- Calibrate standing, ground/seat support, swimming, hand contacts, riding, kneeling and reclining from fixed reference poses during loading or height changes. Playback blends cached absolute offsets without resampling animated feet or modifying native bones.
- Add separate left/right/two-handed item uniform scale and XYZ controls. Palm-derived grips retain authored transforms; item size follows player height relative to 2 m. Two-handed item types use their own group exclusively.
- Synchronize every calibration slider: standing/sitting offsets, state/clip XYZ, all three item scales/XYZ, plus physics sway weight and height. Each authenticated player owns independent instance settings; received controls never write the viewer's preferences, even for the same model. Manual changes update in place after release; height changes recalibrate from the reference without accumulating offsets.
- Negotiate calibration-capable requests/snapshot format 3. Complete visual state shares one increasing sequence; reject malformed/stale/downgraded messages before advancing it. Bounded snapshot chunks are assembled atomically. Preserve old-peer model/height behavior, local mode, missing-file fallback and unmodded admission.
- Save personal per-model calibration preferences atomically; missing configuration uses defaults. VRM 0.x and 1.0 share the same calibration and synchronization implementation.

Validation: `docs/release-1.8.14-validation.md`.

## 1.8.13 — Consistent VRM 0.x and 1.0 behavior

- Extend the existing linear base/shade brightness limits to native VRM 0.x MToon imports, before expression baselines are captured. Preserve lower colors, alpha, textures and source-file hashes.
- Remove the legacy global sun/ambient color multiplier from already lit MToon materials. It previously multiplied scene lighting twice and bypassed the VRM 1.0 brightness treatment.
- Apply F8 scene-lighting, shadow-reception and bloom controls to both MToon generations. Preserve original material identities, outlines, render queues, blend modes and texture bindings; support legacy property names in depth/normal and bloom passes.
- Keep the shared 1.4–2.2 m height slider (default 2 m), absolute skeleton/sole calibration, seated contact handling and independent player height synchronization for VRM 0.x and VRM 1.0. Add legacy-avatar regression coverage for repeated height changes, locomotion, sitting and actual player-instance synchronization.
- This is a generic client fix based on VRM/material format, without model-specific exceptions, VRM file edits or online model distribution. Server protocol remains compatible with 1.8.12.

Validation: `docs/release-1.8.13-validation.md`.

## 1.8.12 — Adjustable per-player avatar height

- Add an F8 model-height slider: 1.4–2.2 m in 1 cm steps, default 2 m. Save height per local game character in `BepInEx/config/ValheimVRM/avatar_heights.json`; model folders still only need `.vrm` files.
- Apply exact target height to each player's clone of the cached import, without changing shared model settings, source files or another player's scale. Release the slider to reattach and recalibrate sole/hip clearance, seated contacts, camera height, equipment and spring references through the normal initialization path.
- Synchronize height together with model selection under authenticated player/character identity and the existing increasing request sequence. Same-model players can have different heights. Negotiate height-capable packets/snapshots; reject nonfinite/out-of-range heights before advancing the request order. Preserve local mode, old-peer model synchronization, missing-model fallback and unmodded admission.
- Keep the fixed standing/walking/running reference; height changes never accumulate scale or add animated-foot correction. Update both server and participating clients to enable height synchronization.
- Local Windows client and server distribution bundles include BepInEx 5.4.23.3; public GitHub runtime ZIPs require a separate loader installation. README links both upstream BepInEx releases and the Valheim-specific pack.

Validation: `docs/release-1.8.12-validation.md`.

## 1.8.11 — Calibrate locomotion height from the avatar skeleton

- Fix extra vertical bobbing introduced by following the lowest animated sole during walking and running. Calibrate a standing reference once from each avatar's humanoid rest skeleton, skin bind matrices and sole geometry. Locomotion then uses a constant lift while retaining the original animated hip motion.
- Calibration is independent of the pose at loading and does not modify the native skeleton. Mesh vertices can use different authored coordinate axes, so evaluate their bind-weighted reference positions before measuring soles.
- Keep the 2 m minimum visible height, independent F8 posture offsets and separate seated contact handling. Stable animation takes priority over forcing every foot vertex to touch the floor; small pose-dependent foot intersections or gaps can remain.
- Add real Walk New, Jog New and Run New frame tests, measuring the avatar-minus-native hip offset across idle/start/walk/jog/run/stop and checking that locomotion performs no dynamic contact samples.

Validation: `docs/release-1.8.11-validation.md`. The final locomotion run passed 4,320 continuous frames across four avatars, with zero dynamic contact samples during locomotion and reference standing sole errors below 9 mm. The synchronization protocol, rendering shaders and physics controls are unchanged; update the viewing player's client.

## 1.8.10 — Stop avatar height feedback into the native skeleton

- Fix the 1.8.9 regression where loading a scaled avatar made it rise continuously. Remove both writes of corrected VRM bone positions into the game's native humanoid bones. Every rendered pose starts from the current native animation and receives one contact correction.
- Keep imported height/scale and contact geometry cached. Only animated contact positions are evaluated each frame; the previous frame's height correction is never a new input. The minimum height remains 2 m, and the independent F8 standing/sitting offsets still default to zero.
- Move hand, helmet and back-item sockets directly to the avatar, preserving socket restoration and protecting actual native bones from accidental socket configuration.
- Add continuous Unity frame regression tests alongside the existing sampled pose, equipment and spring tests. Correct the earlier validation's runtime-stability claim, which was based on manually stepped animation and missed the feedback.

Validation: `docs/release-1.8.10-validation.md`. 8,640 continuous Unity frames and 1,320 sampled poses passed across four avatars, including seated attachment, equipment restoration and physics regression checks. Model files and server synchronization protocol are unchanged. Update the viewing player's client to receive the fix.

## 1.8.9 — Correct scaled foot/seat placement and add posture offsets

- Keep the minimum visible model height at 2 m and correct mesh-bake scale compensation.
- Replace original-hip anchoring and the approximate seated foot lift with per-avatar weighted mesh contacts. Standing soles and ground/seat support adapt to the current pose and scale without model-specific height constants.
- Add independent F8 standing and sitting height offsets, default 0 cm, range −50 to +50 cm. Changes preview live and save per model; resetting both restores automatic placement. Existing settings and model files are preserved.
- Keep small-screen F8 controls reachable with a scrolling panel. Height adjustments are local configuration; the server protocol is unchanged.

Validation: `docs/release-1.8.9-validation.md`. Four VRM 0.x/1.0 models pass 1,320 actual posed-mesh checks, with maximum contact error below 4 mm, independent posture offsets, equipment grips and spring physics regression checks.

## 1.8.8 — Fix TAA clothing/bloom artifacts and raise minimum height to 2 m

- Keep transparent avatar layers in the same jittered camera projection as the opaque body while Valheim's TAA is active. Previously, close-fitting stockings could disappear in jagged moving strips because they tested against depth from a different projection.
- Apply the correction per camera render when an avatar is visible, then restore the camera's native transparency setting. Cameras without a visible avatar retain their ordinary behavior. Game antialiasing preferences, original materials, opacity, meshes and model files are preserved.
- The correction covers local and synchronized avatars displayed on the updated client, with either avatar bloom setting. The optional server protocol is unchanged; the viewing player's client must be updated.
- Keep bloom exclusion coverage in the same projection as the scene and sample it at the TAA-resolved offset. Fix the large alternating coverage dropouts when game antialiasing is on and avatar bloom is off, while leaving scene HDR color intact.
- Raise the minimum standing visible mesh height from 1.6 m to 2 m. Smaller models scale uniformly; models already at least 2 m and larger requested scales are preserved.

Validation: `docs/release-1.8.8-validation.md`. Real-model render comparisons cover TAA samples, front/back views, translated world coordinates, ordinary/strong lighting and both avatar bloom settings. A separate static-model test records 64 consecutive HDR frames after 32 warmup frames, including bloom coverage and the actual filtered bloom input.

## 1.8.7 — Fix menu reentry leaks and enforce minimum avatar height

- Install the assembly's Harmony patches once per process. Returning from a world to the main menu previously appended another copy of every patch. Duplicate bloom prefixes overwrote their shared temporary-texture state, leaving full-resolution HDR textures unreleased each frame.
- Cancel avatar attachment safely when a scene closes, the player disappears or a newer visual replaces the in-progress clone. Check lifetime after every asynchronous boundary and release pending attachment ownership on exit.
- Enforce a minimum imported standing mesh height of 1.6 m, with uniform scaling. Apply the same rule to local and synchronized avatars, respecting larger custom scales and retaining the original model files. Measure once before animation; sitting and repeated cloning never accumulate scaling.
- VRM-only model folders, brightness limits, physics/rendering controls and the optional avatar-sync protocol remain available.

Validation: `docs/release-1.8.7-validation.md`. The old build reproduced 4 bloom allocations with only 1 distinct texture released per call after three menu startup callbacks. The fixed build retains one patch and balances every temporary allocation during sustained 4K rendering.

## 1.8.6 — VRM-only model library

- The `ValheimVRM` model folder only needs `.vrm` files. No model TXT, JSON, manifest, default avatar or initialization step is required for local or server-synchronized selection.
- Store selections, physics weight and rendering preferences under `BepInEx/config/ValheimVRM`, created on demand. Optional model/global TXT settings use the same directory.
- Remove reads/migration of old model-folder configuration and `selected_models.json`. Move wanted existing settings to the new directory before upgrading, or use defaults.
- Built-in appearance defaults now match the distributed models: scale 1.0, brightness 1.0, MToon enabled and player fade disabled. Existing explicit settings override these values.
- Package documentation, licenses and examples under `BepInEx/plugins/ValheimVRM`; optional distribution validation checks runtime dependencies without requiring any model or model configuration.
- Sync wire protocol and per-player request sequencing remain unchanged.

Validation: `docs/release-1.8.6-validation.md`. Server instructions: `docs/SERVER-SYNC.md`.

## 1.8.5 — Apply the avatar brightness reference at import

- Automatically cap VRM 1.0 MToon base color at 0.45 and shade color at 0.2025
  in linear RGB space. Only colors above their reference are scaled down;
  preserve RGB ratios, alpha and every lower/equal color exactly.
- Apply the limit during material creation, before expression baselines are
  captured. Calibrated models, repeated imports and rendering-option toggles do
  not accumulate further darkening.
- Keep source VRM files, textures and synchronization hashes unchanged. The same
  import path covers local and synchronized remote avatars. VRM0 and non-MToon10
  shaders retain their previous behavior. Emission and animated color overrides
  are outside this import-only base/shade limit.
- Update bilingual README and installation instructions. Server protocol remains
  compatible with 1.8.3/1.8.4; the server package has a matching version number.

加载时自动压低超过基准的材质颜色，未超过的完全不动；保持色彩比例、透明度和模型文件指纹。
现有已校准的 Shinano 与 KUMALY 无需重新导出，也不会再次变暗。

See `docs/release-1.8.5-validation.md` and `docs/SERVER-SYNC.md`.

## 1.8.4 — Release unused avatar resources

- Dispose native GLB import buffers and importer contexts on success and failure.
- Release imported templates about 15 seconds after their last player instance
  is destroyed. Keep shared avatars, off-camera players and pending attachments
  alive; same-name replacements no longer destroy another player's resources.
- Stop retaining VRM source-file bytes in normal local and server-sync modes.
  Opt-in legacy file sharing still retains the bytes needed for transfer.
- Preserve importer material ownership, dispose legacy color-driver material
  copies and remove the unsafe global VRM0 texture cache. Re-selecting an evicted
  avatar imports it again.
- Keep the 1.8.3 request-order protocol and optional server compatibility unchanged.
  This fix runs on clients; the server package is rebuilt at the matching version.

修复连续换装后旧模型长期占用内存的问题。最后一个使用者离开后约 15 秒回收；
多人共用模型不会被误删，镜头外但仍在场景内的玩家保持正常显示。

See `docs/release-1.8.4-validation.md` and `docs/SERVER-SYNC.md`.

## 1.8.3 — Ordered avatar selection requests

- Add positive 64-bit request sequences after capability negotiation. The server
  accepts only increasing sequences per authenticated connection, so a later-sent
  choice remains authoritative even when delivery is reversed.
- Reject duplicates, stale requests and late legacy-format packets on upgraded
  connections. Opt-out shares the sequence; respawn does not reset it. Reconnects
  get a fresh counter and old RPC callbacks cannot affect the new connection.
- Apply the same rule to the listen-host bridge. New clients also ignore equal
  snapshot revisions and show the negotiated protection state in F8.
- Preserve legacy 1.8.0–1.8.2 compatibility and unmodded admission. Upstream
  ordering requires both sender and server to run 1.8.3; legacy mode retains its
  original arrival-order behavior. Update the complete client and server ZIPs.

请求序号按每位玩家的连接独立递增；先发 A、后发 B，即使 B 先到，也不会被迟到的 A 覆盖。
死亡重生和开关同步不重置序号，重连才重置。F8 可查看保护是否启用。

See `docs/release-1.8.3-validation.md` and `docs/SERVER-SYNC.md`.

## 1.8.2 — Safe fallback for different avatar folders

- Different client/server folders never become an admission requirement. The
  server does not read model files; each receiver verifies only the selected VRM.
- Verify uncached bytes before loading settings or invoking UniVRM. Missing,
  deleted, unreadable, differing or incompatible cached files retain the existing
  appearance (vanilla on first load), with a bounded status message.
- A failing player selection does not block another player. Correct unimported
  files and refresh F8 to recover; restart after replacing a cached model.
- Add engine regression coverage for mismatch containment and recovery, preserving
  unmodded-client compatibility and independent per-player identities.

Update the complete client ZIP for this fix. The server package is also versioned
1.8.2, with unchanged protocol 1 compatibility for 1.8.0/1.8.1 peers.
See `docs/release-1.8.2-validation.md` and `docs/SERVER-SYNC.md`.

文件夹不一致不影响入服；缺少、无法读取或同名不同内容的模型安全跳过，
保留原有可用外观，首次加载显示原版角色。服务器不读取自己的模型文件夹。

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
