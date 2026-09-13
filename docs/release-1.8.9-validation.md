# 1.8.9 validation — scaled grounding and posture offsets

Test environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1, BepInEx
5.4.23.3. The isolated game is in the repository's ignored
`artifacts/camera-qa/game`; no avatar Unity project is used for game tests.

## Causes and changes

The previous animation retargeter overwrote the imported avatar's hip height
with the game's original hip height. Increasing the visible mesh to 2 m
lengthened the legs without raising that pivot. In controlled 1.8.8 standing
poses, actual Shinano Light and KUMALY sole vertices were approximately
0.25–0.28 m below the character plane.

Ground sitting had a separate one-sided lift based on extremal foot vertices
assigned to a single dominant bone. It did not retain their full skin weights,
so bent feet could overestimate penetration and raise the model too far.
The mesh bake also omitted scale compensation before applying the renderer's
transform. A direct bind-pose/skin-weight calculation agreed with
`BakeMesh(mesh, true)`, while the default overload differed on scaled models.
See Unity's [BakeMesh API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SkinnedMeshRenderer.BakeMesh.html).

The replacement caches visible, weighted lower-body contact vertices and their
bone-local coordinates at attachment. Each frame it samples the current bone
matrices, sharing matrix reads across meshes. There is no per-frame mesh bake
or contact-array allocation. Corrections can raise or lower the hip:

- Standing/movement: match the original animated soles, bounded by the
  character plane when grounded on land.
- Ground sitting: place the lowest leg/pelvis support surface at the character
  plane, including the sit-down and stand-up clips.
- Chairs, thrones and ship seats: match the original pelvis/thigh seat surface.
- Transitions blend the two contact corrections using the animator's transition
  progress. Sleep and ragdoll placement retain their existing path.

The minimum imported visible height remains **2 m**. No model names, model
files, networking identity or physics collider dimensions are changed.

## Engine checks

The pose suite imports Shinano Light Adjustment, Shinano Sleep, KUMALY 2 and the
VRM 0.x AliciaSolid fixture through the production importer.

For each model, 330 sampled poses cover standing, ground sitting, sit-down,
stand-up and chair sitting at three scales and two root heights (0 and 1200 m),
with translated/rotated roots. Independent Unity mesh skinning measures the
actual sole, lower-body support and seat surfaces. All **1,320 samples pass**
with zero manual offsets. Maximum contact error in this run was **0.003540 m**.
Those checks manually evaluated the animator and invoked the synchronizer's
LateUpdate. They did not exercise its real Update/animation/LateUpdate order.
A later continuous-frame test reproduced rising avatars in 1.8.9 because
corrected VRM bones were written back into the native skeleton. Therefore
this test did not establish runtime stability; see the 1.8.10 validation.

Standing +17 cm and sitting −8 cm are checked independently on standing,
ground-sitting and chair poses. Save/reload preserves both values, existing
model parameters and comments; non-finite/out-of-range values are constrained.
Both settings default to zero and the model directory still requires only VRMs.

The same full run checks both hand grips at three scales/four poses, attachment
restoration, spring-clone references and finite physics movement, plus 240
moving frames at physics weights 0/25/50/100% for each model.

Local evidence: `artifacts/grounding-qa/full-final/results.txt` and `unity.log`.
The earlier diagnostic evidence is in `skinning-diagnostic` under that folder.

The lifecycle suite also passed on this build: 360 actual 4K HDR bloom calls
had balanced temporary allocations, three main-menu reloads retained one patch,
and ten attachment-cancellation boundaries released their pending leases.
Independent skinning of the three imported VRM 1.0 models measured **2.000 m**
visible standing height. Known-height fixtures cover already-tall models,
explicit larger scales, repeated application and clone calibration.
Evidence: `artifacts/grounding-qa/lifecycle-final/results.txt`.

## Limits

The contact cache reflects the visible imported geometry and its authored
blend shapes at attachment. It does not implement ground collision for hair,
hand IK, slope foot IK or leg bending to fit every furniture height. Unusual
weighting, animated changes to foot/seat shapes or another mod modifying the
skeleton can require further adjustment. Optional F8 offsets are per-model
settings on the viewing client and are not broadcast by avatar sync.
