# 1.7.2 avatar pose and rendering regression validation

Environment: Windows x64, D3D11, Valheim 1.0.7 / Unity 6000.0.75f1,
BepInEx 5.4.23.3, UniVRM 0.131.2. Tests use a disposable game copy under the
repository's ignored `artifacts/` directory and a separate save directory.
They import fixtures and use the game's player Animator without loading a world
or modifying an installed character. Private avatars are not distributed.

## Reproduced causes

- Ground sitting: the game skeleton's seated hip pivot is below the floor.
  Copying that world position to a different humanoid buries the avatar's legs.
  The reported outfit's soles reached approximately -0.253 m in seated idle.
- Equipment: moving vanilla humanoid bones does not adapt the original grip
  offset to the VRM's palm dimensions, and UniVRM constraints execute afterwards.
  The new attachment component uses skin bind poses and wrist/finger proportions,
  then updates both game mounts at execution order 12000.
- Physics: the imported VRM 1.0 fixture has 57 spring chains, but a player-side
  Instantiate clone had zero chains although the joint components remained.
  Explicitly copying and remapping nested lists restores springs and colliders.
  The clone's rest transforms are restored and its runtime is created after
  parenting at the player's location. Source-avatar references are not retained.
- Rendering: Amplify Occlusion in PostEffect mode can apply background geometry
  to forward-only MToon surfaces in a deferred camera. Disabling received light
  shadows does not remove this effect. A tilted background object reproduces it.
  The compatibility pass supplies depth, normals and neutral albedo/specular
  values after the G-buffer, retaining the original MToon visible color passes.

## Engine checks

- Eight local VRM 1.0 fixtures and the independently proportioned official
  [AliciaSolid VRM 0.x fixture](https://github.com/vrm-c/UniVRM/blob/master/Tests/Models/Alicia_vrm-0.51/AliciaSolid_vrm-0.51.vrm)
  pass the pose suite. Each gets 99 samples across sitting down, seated idle and
  standing up, three model scales, and translated/rotated roots. Baked weighted
  foot vertices stay above the test plane; standing resets the automatic lift.
- Both hands pass identity mapping, 0.6/1.0/1.4 model scales, four animation
  states and repeated binding/restoration checks. The two skeleton families
  produce different grip distances corresponding to their different palm sizes.
- VRM 1.0 clones retain their source chain/group counts, all tested joint and
  collider references belong to the clone, and spring motion is finite and
  nonzero under a controlled movement sequence. The reported outfit's hair and
  chest joints changed from zero motion before the fix to dynamic rotation.
- The background-AO test measures a maximum foreground linear-channel change
  of approximately 0.0813 before the fix and 0 after it. Normal light-shadow
  controls are exercised separately from AO.
- Complete linear-image comparisons cover HDR on/off, opaque/cutout/blended
  modes, static/animated UVs and foreground walls. The compatibility pass must
  preserve visible color within 0.001 per channel. Transparent overlays remain
  on their original rendering path.
- The camera-height/PhysX suite, catalog suite, runtime dependency audit and
  Release build are included in the release checks.

This is controlled engine validation, not a complete new world playthrough.
The sitting plane follows the character root; cloth-ground collision, steep
terrain and all seat types are not solved by this correction. Palm calibration
does not add secondary-hand IK for two-handed weapons. Missing exported physics,
all humanoid rig conventions, multiplayer, other mods, Vulkan and other operating
systems are outside these tests.

## Reproduction

Set `VALHEIM_INSTALL_PATH` to the installed game, build the mod in Release, then
build `tests/AvatarPoseTests` and `tests/AvatarRenderingTests`. Install the full
release into a disposable game copy and run one probe DLL at a time from its
`BepInEx/plugins` folder. Do not put these test DLLs into the normal game.

For pose tests, set `VRM_POSE_TEST_OUTPUT` to an output directory and
`VRM_POSE_TEST_MODELS` to absolute VRM fixture paths separated by `|`.
For rendering tests, set `VRM_RENDER_TEST_OUTPUT` to an output directory; the
test creates its own scene geometry and needs no avatar file.
Launch the disposable executable with `-batchmode -force-d3d11 -savedir <isolated
save directory> -logFile <test log>`. Each probe exits on completion and writes
its measurements, or `error.txt` on a failed assertion. Rendering tests also
write before/after PNGs. Keep the whole test installation and artifacts ignored.

Build the shader bundle using `shaders/README.md`. The bundle contains shader
assets only. See the previous camera release's validation document for the
camera-height test setup.
