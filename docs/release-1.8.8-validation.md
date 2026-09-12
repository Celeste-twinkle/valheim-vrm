# 1.8.8 validation — TAA layers, bloom coverage and 2 m minimum height

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1, BepInEx 5.4.23.3,
Direct3D 11, RTX 5090 D. All game probes ran in the ignored isolated copy under
`artifacts/camera-qa/game`, outside the avatar Unity project. The user's friend
reported Windows 10 / RTX 4080; that particular machine was not directly tested.

## Confirmed causes

The installed game's `TaaComponent.SetProjectionMatrix` jitters the camera but
sets `useJitteredProjectionMatrixForTransparentRendering` to false. The opaque
body writes jittered depth, while close-fitting blended stockings test against
that depth from a different projection. This reproduces the reported jagged bare
strips without changing the model, bones, shape keys or network state.

The bloom exclusion pass runs after transparent rendering and tests coverage
against camera depth. With the mismatched projection, large areas of the opaque
body disappear from that mask on alternating TAA samples. Bright scene color then
enters bloom despite the F8 avatar-bloom checkbox being off. TAA also resolves
scene color before bloom preparation, so coverage sampling must use the current
TAA UV offset. The patch restores native camera state after each render and clears
the bloom sampling offset on every camera cull.

Relevant Unity API: [Camera.useJitteredProjectionMatrixForTransparentRendering](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-useJitteredProjectionMatrixForTransparentRendering.html).

## Real-model pixel comparisons

`tests/AvatarAntialiasingTests` imports and poses Shinano_LightAdjustment and
KUMALY_2 using the production importer. It explicitly enables scene lighting and
shadow reception rather than inheriting saved test preferences. The 2 m build
passed 64 cases per model: four Halton samples, front/back views, world positions
at 0 and 3000 m, directional-light intensities 1 and 5, and both F8 bloom states.

Each case compares raw linear HDR pixels before post effects to a reference with
matching projections for both layers. A controlled postfix reproduces the legacy
transparent projection. All 128 cases reproduced corruption in that control;
the production fix matched the reference with a maximum RGB difference of zero.
Disabled avatar targets, excluded layers and avatars outside the camera frustum
retained the game's native transparency setting. The state was restored after
each corrected render.

Evidence: `artifacts/remote-stocking-qa/final-shinano/results.txt` and
`artifacts/remote-stocking-qa/final-kumaly/results.txt`.

## Consecutive-frame bloom verification

The user's second recording was also extracted as 60 consecutive frames, without
frame-rate reduction, from a one-second interval. The engine probe independently
freezes the posed KUMALY mesh, keeps real scene lighting on, enables game TAA and
scene bloom, and disables avatar bloom. After 32 warmup frames it saves 64
consecutive raw-scene, mask, filtered-bloom-input and final-output frames.
Numerical checks use floating-point reads; PNG previews clip HDR values.

The original 1.8.7 DLL reproduced alternating large mask dropouts under strong
light (raw HDR peak 16.95313). Its mask-area range over the recorded sequence was
0.02583791 of the frame. The final 2 m build retained continuous body coverage;
its mask-area range was 0.00009873509 of the frame, approximately 0.034% relative
to its mean coverage. It passed the limit of 1% relative variation for a frozen
fixture. The old and final recordings use different minimum heights/framing;
these are coverage-stability checks, not a pixel-identical cross-version video.

Ordinary TAA silhouette changes, partial alpha coverage and scene HDR brightness
remain. The test does not claim identical final pixels across frames or suppress
all light from surrounding objects. It verifies that the avatar's opaque body
does not repeatedly drop out of bloom exclusion.

Evidence: `artifacts/remote-stocking-qa/temporal-old-hdr/` and
`artifacts/remote-stocking-qa/final-temporal/`. Private models and recordings are
local fixtures and are excluded from public source and release packages.

## Minimum height and lifecycle

The minimum is 2 m at import, measured from visible standing mesh vertices before
game animation, including hair/headwear. Explicit larger scales are respected;
models already at least 2 m retain scale 1. Original VRM bytes, per-player model
selection and the optional network protocol are unchanged.

The lifecycle engine suite covers known 1.2/1.6/2.0/2.2 m fixtures, real model
imports, repeated scaling and clone calibration, sustained 4K bloom allocation,
scene reloads and cancellation during asynchronous avatar attachment. Results are
recorded under `artifacts/remote-stocking-qa/final-lifecycle/`.
All checks passed: 360 3840x2160 bloom calls with balanced temporary allocations,
three actual menu reloads with one patch registration, and ten attachment-cancel
boundaries. Imported KUMALY_2 / Shinano_LightAdjustment / Shinano_Sleep reached
2.000 m at scales 1.620 / 1.434 / 1.515 respectively.
