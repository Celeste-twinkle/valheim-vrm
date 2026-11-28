# 1.7.0 fork release validation

Test environment: Windows x64 / D3D11, Valheim 1.0.7, Unity 6000.0.75f1,
BepInEx 5.4.23.3 (BepInExPack Valheim 5.4.2333), UniVRM 0.131.2.
Tests ran in a separate game copy and local save directory with cloud saves and
preference writes isolated from the normal installation. Test avatars are not
included in this repository or the release.

## Build and package

- Full Release build completed with zero warnings and errors.
- Runtime metadata check resolved all 335 UniGLTF method references in the matched
  VRM 0.x and VRM 1.0 libraries.
- Both `ValheimVRM.avatar_bloom` and `ValheimVRM.avatar_rendering` resources are
  embedded in the DLL. Unity's bundle build reported no shader compiler errors.
- ZIP CRC, extraction layout, DLL/dependency hashes, license notices, and bilingual
  installation guides were checked. No VRMs, user settings, probe plugin, game
  engine libraries, Unity.Burst or Unity.Mathematics are distributed.

## Catalog and game behavior

The standalone catalog test covers an empty directory, 25 models, Unicode/spaces,
uppercase extensions, refresh, per-character persistence, deleted selections,
path boundaries, exclusion of Shared downloads, and migration of old local-build
selections. Ambiguous old identifiers fall back instead of selecting an arbitrary file.

Game tests passed all eight VRM 1.0 fixtures and a ninth, independently named VRM
0.x fixture (the official AliciaSolid model). Every switch retained the equipped
items and armor value. A deliberately invalid VRM was rejected while retaining
the previous model, and a subsequent valid selection succeeded. A fresh catalog
read restored the saved choice. Opening the picker blocked normal game input.

Movement, sword/shield attachment, attacking, sitting, humanoid validity, finite
skinned meshes, death/ragdoll motion and bone lengths passed. The selected avatar
reattached after respawning.

## Rendering

The defaults use the same unmodified shader bundle and authored MToon10 values as
the local build. No model meshes, textures, blend shapes or VRM material data were
rewritten. Existing soft-shading values and transparent/cutout render queues were retained.

- Scene-lighting, shadow-reception and bloom toggles changed the intended rendering
  paths. A model loaded while lighting was disabled inherited that choice.
- Restoring lighting and received shadows produced a pixel-identical 768×768 HDR
  image to the default image taken before toggling. Material objects stay intact
  so UniVRM expression bindings are retained.
- In 13 captures (eight avatars plus day, night, point-light off/on and multiple
  bright lights), enabling bloom exclusion left every pre-bloom HDR channel unchanged.
- HDR peak channels reached approximately **1.52** in daylight, **1.23** under a
  point light and **22.31** under multiple bright lights. There is no output ceiling
  or highlight compression. Night brightness remained approximately **0.176**.
- Avatar halo was suppressed in ten post-processing comparisons. A separate scene
  emitter and its bloom region were pixel-identical with the avatar visible/hidden.
- Point-light response and an occluding-object shadow check passed. Disabling
  received shadows preserved directional shading and point-light distance attenuation.

## Reproduction

Run `tools/Test-RuntimeDependencies.ps1` and `dotnet run --project tests/AvatarCatalogTests`
with `VALHEIM_INSTALL_PATH` set, then build with `dotnet build -c Release`.
Use a disposable game copy, a separate save directory and avatars you may use to
exercise the F8 list, invalid-file recovery, the three toggles, and death/respawn.
Capture the same fixed pose/camera before and after restoring the rendering defaults;
compare linear HDR data separately from the game's post-processing result.

The controls target VRM 1.0 MToon materials. Other materials remain on their native
rendering path. Linux, macOS, Vulkan, multiplayer sharing and interactions with
other mods were not validated. Headless tests verify menu behavior and input blocking;
they are not a substitute for testing every desktop resolution or input device.
