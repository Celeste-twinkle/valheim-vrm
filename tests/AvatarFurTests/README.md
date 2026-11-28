# Optional fur engine regression

Build the Release client and `tests/AvatarFurTests` using the installed Valheim
references. Copy only the test DLL into an **isolated** game's plugins directory.
Set `VRM_FUR_TEST_OUTPUT` and `VRM_FUR_TEST_MODEL` to an output directory and a
private VRM fixture authored with `tools/Add-FurMetadata.py`. Launch with a real
D3D11 graphics device, `-batchmode`, and a separate `-savedir`. Never install
test DLLs in a player's normal game. The probe disables cloud saves and quits.

The test generates in-memory absent/disabled/zero/unknown/invalid-type/missing-
image variants, then imports the enabled fixture. It asserts no optional shader
bundle, material or component in inactive cases; source mesh identity, enabled
local macro, independent clone references and matching shape weights when present;
pixel-identical disabled-macro/base rendering; visible enabled fur; and destruction
of materials, masks and the last bundle lease. Run it for VRM 0.x and 1.0 fixtures.

Also run `AvatarAntialiasingTests` with the same file and
`VRM_AA_TEMPORAL_ONLY=1`. This records 64 consecutive frames after 32 warmup frames
with TAA, strong HDR lighting, scene bloom and avatar bloom exclusion. Fur uses
the real color and named coverage passes. For pose/height regressions use
`AvatarPoseTests` with the same fixture, including `VRM_HEIGHT_ADJUST=1` and
`VRM_HEIGHT_REPEAT=1` for repeated calibration. See those suites' README files.

`results.txt` ends with `AVATAR_FUR_TESTS_PASSED`; `error.txt` indicates failure.
Private fixture files and generated images are not committed or distributed.

The lighting probe compares linear HDR pixels on a uniform cloth fixture with
fur on/off for native VRM 0.x and 1.0 materials: directional light, normal map,
two lights, emission, shading maps, a point light, MatCap and rim (including
legacy HDR color conversion). Average error must be below
1.5% (allowing displaced fins to sample a slightly different point-light field).
`VRM_FUR_LIGHTING_ONLY=1` runs this probe without importing an avatar.
`VRM_FUR_LIGHTING_BASELINE=1` records a pre-fix diagnostic without the parity gate;
never use that flag for release validation.
