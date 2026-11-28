# 1.8.16 validation — transparent SSAO and alpha-mode consistency

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1,
BepInEx 5.4.23.3, D3D11. Isolated game: artifacts/camera-qa/game.
Evidence: artifacts/action-calibration-qa/transparent-*; delivery records:
artifacts/transparent-ao-qa. No avatar Unity project or source VRM is modified.

## Reproduction and final behavior

Transparent mode alone is not sufficient to reproduce the reported artifact.
With a normal queue 3000, the fully opaque test surface did not inherit background
AO even with the old exclusion. With Blend, queue 2450 and depth writes disabled,
the same surface received a 0.08131038 maximum foreground red-channel difference
from a blocker entirely behind it. The previous fix skipped all blended materials.

The first candidate added only an opaque-alpha depth/normal pass. It corrected
that solid surface but failed a genuine 35% transparent surface in queue 2450:
maximum RGB difference from correctly queued native composition was 0.2109661.
That candidate was not released. The final change combines two operations:

- Repair blended MToon materials in queues <=2500 before camera drawing.
  Ordinary blending uses 3000; authored depth-writing blending uses 2501.
  All already-valid transparent queues remain unchanged. Mode, color/texture
  alpha, blend factors, depth-write setting, material identity and source files
  are retained. Preparation also handles later invalid queue changes and shader
  option switches. Only an actual repair emits a diagnostic message.
- Keep the opaque/cutout surface pass and also cover effectively solid pixels
  of blended materials. Blend samples texture alpha multiplied by color alpha
  after UV animation; values >=0.99999 provide depth/normals. Partial coverage
  does not write a solid surface. Opaque ignores alpha; Cutout uses its cutoff.

Valheim's Amplify Occlusion post effect is attached before transparent rendering.
Correct queues therefore composite fabric over the already AO-shaded background.
Background occlusion is still visible through transparent fabric; it no longer
multiplies the whole fabric surface as though it were opaque. The queue boundary
and sorting categories follow [Unity's Built-in rendering order](https://docs.unity3d.com/6000.0/Documentation/Manual/built-in-rendering-order.html).
The implementation changes avatar material preparation, not the game's global
AO component, graphics settings or renderer scheduling.

## Engine coverage

- Client, server and probe builds have zero warnings/errors. The server change
  only updates the package version; synchronization protocols are unchanged.
- transparent-ao-composition-legacy and transparent-ao-composition-vrm1 run the
  same real Amplify Occlusion scene with native VRM/MToon and VRM10/MToon10.
  Per family: 16 opaque-in-Blend queue/depth/fix cases; 40 composition comparisons
  covering alpha 0, .35, .7, .999 and 1, queues 2450/2500/2501/3000 and both depth
  settings. Invalid queues are changed after setup to test per-camera repair.
  Every corrected full-image RGB comparison matches its correctly queued
  reference exactly. At alpha .35, real background AO remains visible with a
  center red-channel delta of 0.09092287; it is not globally suppressed.
- Another 48 image comparisons per family cover HDR on/off, UV animation,
  Opaque/Cutout/Blend, foreground occlusion and color alpha 1/.5. The texture
  contains holes, .35, .7 and fully opaque texels. All visible-color deltas are
  zero with the surface pass on/off. Shadow reception toggles and the previous
  background-AO regression controls also pass.
- Real Shiro (VRM 0.x) and Shinano Light Adjustment (VRM 1.0) each run three
  material configurations: original; every material changed in memory to Blend
  and queue 2450 with authored alpha; and the same with color alpha .65 on all
  materials. No source asset bytes are changed. Queue repair is checked before
  drawing, and true partial materials retain their opacity.
- Each of those six fixtures passes 64 projection/light/bloom cases: 384 total.
  Corrected raw HDR output matches a common-projection reference; forced old
  transparent projection reproduces corruption. Avatar visibility/layers and
  post-render restoration of camera state remain covered.
- Each fixture also records 64 consecutive strong-light frames after 32 warmup
  frames, with game TAA/bloom enabled and avatar bloom excluded: 384 measured
  frames total. Coverage stays within the existing 1% relative variation limit,
  without alternating mask dropouts. These are temporal recordings, not a
  single-frame screenshot judgment.
- An additional original-material probe compares color alpha 1 and .5 for
  Opaque surfaces with avatar bloom both included and excluded. The final RGB
  comparison must remain unchanged: an unused alpha value must not accidentally
  change the bloom mask or make an Opaque surface semitransparent.

## Limits

The remote user's actual avatar was not supplied. The controlled material
combinations reproduce and fix this cause; they do not establish that every
reported dark patch has the same cause. Supported surfaces are VRM 0.x MToon and
VRM 1.0 MToon10. Other shaders retain their own behavior.

This preserves native transparent composition, not order-independent
transparency. Intersecting or self-overlapping transparent meshes can still
need correct authoring/sorting; a single depth buffer cannot encode every
transparent layer. Authored transparent depth writes are preserved. Arbitrary
third-party AO implementations applied after transparency are not replaced.

The viewing client needs this update. A server-only update cannot change its
rendering. Public-network sessions and non-Windows graphics backends were not
part of this rendering regression. Public packages exclude avatars/BepInEx;
local numbered Windows client/server bundles retain their existing loader.
