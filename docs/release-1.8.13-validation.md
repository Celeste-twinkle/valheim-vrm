# 1.8.13 validation — VRM 0.x / 1.0 behavior

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1,
BepInEx 5.4.23.3, D3D11. Tests run in the repository's ignored
`artifacts/camera-qa/game`, with evidence in `artifacts/legacy-brightness-qa`.

## Cause and implementation

VRM 0.x MToon bypassed the MToon10-only import brightness wrapper and rendering
controls. MToonColorSync also multiplied its already lit base/shade/emission
by global sun/ambient light every frame. This explains the excessive daylight
brightness and ineffective scene-lighting switch.

Both native MToon importers now use the same linear RGB limits, before expression
baselines are captured: base 0.45, shade 0.2025. Legacy VRM vectors are already
sRGB; MToon10 converts its linear factors to sRGB. The shared limiter preserves
lower values and alpha. Original VRM files, textures and synchronization hashes
are untouched. Both lit shader families bypass the old global color multiplier.

The legacy options shader retains upstream MToon properties and keywords. Both
generations share the F8 lighting/shadow/bloom controls and depth/normal pass,
with their respective alpha, culling, depth-write and UV-animation conventions.
Material identities, blend mode, render queue and expression bindings remain
intact. No asset names or exporter versions select a special implementation.

Height, grounding, equipment and player synchronization continue through their
shared humanoid/mesh path. The default is 2 m, adjustable from 1.4 to 2.2 m;
each player clone scales from cached unscaled geometry and recalibrates from
the source pose. Locomotion retains its fixed reference with no accumulated lift.

## Validation

- Client, server and test assemblies build with zero warnings/errors. Embedded
  shaders rebuild with Unity 2022.3.22f1 and report AVATAR_RENDERING_BUNDLE_OK.
- Production imports of Shiro and AliciaSolid (both VRM 0.x): 52 above-limit
  base/shade properties capped. Shinano Light Adjustment and KUMALY 2 (VRM 1.0):
  all 70 calibrated properties preserved exactly. Boundary, HDR, proportional
  RGB, alpha and 1,000 repeated limiting operations pass the existing tests.
- Old 1.8.12 comparison reproduces ineffective scene-lighting controls on both
  legacy fixtures. Shiro's strong-light HDR mean peak channel was 1.394141;
  after the fix it is 0.329180 in the same fixture. These are scene-specific
  measurements, not a final-output brightness target. Unlit day/night maximum
  RGB delta is 0 in both Forward and Deferred paths after the fix (previously
  5.995847 for Shiro). Material colors no longer react to global sun multipliers.
- Twenty render-toggle combinations per legacy fixture preserve material
  identity, alpha, textures, queues, keywords and reset expression baselines.
  Original model bytes remain unchanged.
- Legacy and MToon10 shadow tests: disabling reception removes the occluder's
  shadow; enabling restores it. Depth/normal regression covers 48 combinations
  per shader family of HDR, UV animation, opaque/cutout/blend and foreground
  occlusion. Visible color remains unchanged; background AO no longer overlays
  the avatar. Evidence: legacy-render-coverage and render-coverage.
- Shiro and AliciaSolid at 1.4, 2.0 and 2.2 m: 2,160 continuous Unity locomotion
  frames, zero extra height variation; 450 standing/ground-sit/chair contact
  samples. Maximum sampled error is 0.027805 m, within the standing tolerance;
  seated samples meet the 0.008 m limit. Seventy-two repeated height changes
  with 2,160 manually stepped updates produce 0.000000000 m cumulative drift.
- An additional 660 pose/contact samples exercise three scales, translated
  origins and independent standing/sitting offsets. Maximum error is 0.039592 m
  (standing). Both hands and seven equipment sockets pass scale/pose/restore
  checks. Physics tests step 78 Shiro and 30 AliciaSolid joints for 240 updates
  each at 0/25/50/100% weight, without simulation feedback (error <0.00002 degrees).
- Actual legacy player clones and real ZRpc production handlers pass independent
  same-model 1.4/2/2.2 m height changes, 12 consecutive local UI requests,
  persistence, reversed packet delivery, respawn, missing/different models,
  ordinary refresh, and unmodded/old-server fallback. The transport is unchanged.
- Shiro and Shinano each pass 64 TAA projection/light/bloom cases with zero
  corrected HDR difference from matched-projection references. Each also passes
  64 consecutive strong-light/bloom-excluded frames after 32 warmup frames:
  mask coverage range is 0.00025177 and 0.00025409 respectively, with no alternating
  coverage dropouts. Evidence: legacy-aa-coverage, aa-final, legacy-temporal-coverage
  and temporal-coverage.

Other evidence directories: baseline-4, brightness-final, pose-1, repeat-1,
fullpose-final and sync-final. The first two baseline launches could not initialize
Steam while it was closed/still logging in; they did not run the suite. One probe
needed correction for the old driver's duplicated material instances, and another
for Unity Color setter round-trip precision. Final runs pass the completed assertions.

## Limits

VRM 0.x and VRM 1.0 are the supported humanoid formats. Invalid files, missing
required bones, arbitrary third-party shaders or absent exported physics cannot
be made equivalent by a format compatibility layer. Standard/non-MToon shaders
retain their own shading. The color-factor limit is not a final HDR-pixel clamp:
authored emission, rim/matcap, lights and post-processing can still be bright.
Grounding is a skeleton/mesh calibration, not terrain IK or a guarantee that
every pose or piece of furniture has zero intersection.

Public-network Steam/PlayFab dedicated-server login and Linux/macOS/Vulkan were
not exercised in this update. The server protocol remains compatible with 1.8.12.
