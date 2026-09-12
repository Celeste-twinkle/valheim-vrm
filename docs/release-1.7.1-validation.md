# 1.7.1 camera regression validation

Environment: Windows x64, Valheim 1.0.7, Unity 6000.0.75f1,
BepInEx 5.4.23.3. The engine probe runs in a disposable game copy with a
separate save directory and exits before loading a character or world.

## Cause and fix

In the installed game's assembly, GameCamera.GetCameraPosition passes the
unsmoothed Character.m_eye position to CollideRay2. VRMEyePositionSync previously
rewrote that position from an animated bone every LateUpdate. That bypassed the
camera's smoothed base offset and made close-range collisions move with the head.
The binding also searched the player's entire hierarchy for an Animator, so it
could select the vanilla rig, before VRMAnimationSync completed its late update.

The new implementation measures the selected clone before the first animation
yield, applies ModelOffsetY, and retains the resulting local eye position. It
holds no animated-bone reference and has no per-frame callback. Each rebinding
restores the original eye position first; opting out, disabling or destroying
the component restores it as well. The game's collision algorithm is unchanged.

## Results

- Release build: zero warnings, zero errors.
- Existing avatar catalog suite: passed (25 models, Unicode, persistence,
  path boundaries and legacy-selection migration).
- Runtime metadata: all 335 UniGLTF method references resolve.
- A BepInEx regression probe uses actual Unity transforms/PhysX and the installed
  GameCamera.RayTestPoint close-range collision helper. With a stationary player,
  a wall, and a synthetic head moving by +/- 0.07 m across 240 samples, the 1.7.0
  DLL reproduces **0.070000 m** maximum collision-result drift. The 1.7.1 DLL
  reports **0.000000 m** with the same inputs.
- The wall still blocks the cast; removing it clears the cast.
- Player translation/rotation, missing or destroyed targets, 25 repeated height
  bindings, ModelOffsetY, opt-out reset and disable/destroy cleanup pass.

This is a controlled engine regression test, not a new full world playthrough
or visual test of every avatar. It does not cover multiplayer, other camera
mods, terrain/snow transitions, or all sitting/ship/bed animations. Headless
shader-unavailable messages are expected and are unrelated to the physics test.
The broader rendering/avatar validation from 1.7.0 remains documented in
[release-1.7.0-validation.md](release-1.7.0-validation.md).

## Reproduce

Build the mod with VALHEIM_INSTALL_PATH pointing at a legally installed game:

```powershell
dotnet build ValheimVRM.csproj -c Release
dotnet build tests/CameraHeightTests/CameraHeightTests.csproj -c Release
```

Use a disposable copy of that game's installation. Install the built mod and
only `ValheimVRM.CameraHeightTests.dll` from the probe output into that copy's
BepInEx/plugins directory. Do not copy its reference DLLs or put this probe in
your normal installation: it deliberately exits the process after testing.
Start the copy with `-batchmode -nographics -savedir <separate-directory>`.
Its BepInEx/LogOutput.log must contain CAMERA_TESTS_PASS and drift 0.000000.
For the negative control, install the original 1.7.0 mod DLL and set the process
environment variable VRM_CAMERA_EXPECT_LEGACY=1; expect BASELINE_JITTER_REPRODUCED
and drift 0.070000. Unset the variable when testing the fixed DLL.
