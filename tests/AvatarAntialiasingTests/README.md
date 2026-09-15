# Avatar antialiasing engine regression

Build after the Release client, using the installed Valheim/BepInEx references:

```powershell
dotnet build tests/AvatarAntialiasingTests -c Release -p:VALHEIM_INSTALL_PATH=F:/steam/steamapps/common/Valheim
```

Copy only the test DLL into an isolated game's `BepInEx/plugins`. Set
`VRM_AA_TEST_OUTPUT` to an output directory and optionally `VRM_AA_TEST_MODEL`
to a local VRM fixture. The default fixture is `Shinano_LightAdjustment.vrm`
in the isolated game's model folder. Private models are not part of the repository.
Launch with a real graphics device (`-batchmode -force-d3d11`, not `-nographics`)
and an isolated `-savedir`. The probe disables cloud storage and quits on completion.

The test imports and poses the real avatar, then renders an HDR reference with
the same projection for both layers. A controlled postfix reproduces Valheim's
old transparent-projection behavior; the normal test path uses the production
patch. Raw scene pixels are captured before post effects so TAA history cannot
hide a failed depth test. Cases cover four Halton samples, two viewpoints,
two world positions, two directional-light intensities and both bloom settings.
It also checks native camera state after each render and cameras that cannot
see an avatar. `results.txt` must end with `AVATAR_ANTIALIASING_TESTS_PASSED`.

Set `VRM_AA_TEMPORAL_ONLY=1` to freeze the posed mesh, enable actual scene lighting,
TAA and scene bloom, disable avatar bloom, and record 64 consecutive frames after
32 warmup frames. Captures include raw HDR scene, coverage, filtered bloom input
and final output; PNGs are previews, while metrics use linear floating-point reads.
The probe requires an HDR peak above 1 and less than 1% relative mask-area variation.
For a bounded old-DLL comparison only, `VRM_AA_EXPECT_LEGACY=1` skips that last
assertion. The old-DLL recording is expected to show large coverage dropouts.

Set `VRM_AA_BLEND_ALL=1` to change the imported fixture's materials in memory to
Blend with queue 2450; the production preparation must move them to transparent
drawing. Add `VRM_AA_BLEND_PARTIAL=1` for color alpha 0.65 on every material.
Run both projection and temporal suites for each material configuration and VRM
generation. The source VRM is never rewritten.

The original-material fixture also compares alpha 1/0.5 on Opaque surfaces with
avatar bloom included and excluded; final RGB must remain identical. Set
`VRM_AA_OPAQUE_ONLY=1` to run only this focused check.
