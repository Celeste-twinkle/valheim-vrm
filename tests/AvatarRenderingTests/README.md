# Avatar surface rendering regression

Build the Release client and this test project against the installed game.
Run only in an isolated game copy under ignored repository artifacts, with an
isolated `-savedir`, real D3D11 device and `VRM_RENDER_TEST_OUTPUT` set.
The probe disables cloud storage and quits after reporting completion.

The default run checks MToon lighting, shadow options and SSAO composition.
Set `VRM_RENDER_TEST_LEGACY=1` for native VRM 0.x MToon.

For common shader surfaces, copy `DepthConsumer.shader`, `BuildDepthConsumer.cs`
(under Editor) and the stock UniGLTF UniUnlit shader into `Assets/RenderingTests`
in the isolated shader-build project. Run `BuildDepthConsumer.Build` and supply
the resulting test-only bundle through `VRM_RENDER_TEST_BUNDLE`.
Set `VRM_RENDER_TEST_UNLIT=1` for the surface suite.

The surface suite checks texture/vertex alpha, opaque/cutout/transparent modes,
color preservation, bad transparent queues, PBR GBuffer preservation and bloom
coverage. A controlled depth-consuming pass represents cloud/water composition
in front of and behind the avatar. This validates the camera-depth contract;
it is not a capture of every native game cloud/water shader or weather condition.
Unavailable optional built-in Unlit variants are skipped; the report names all
actually tested shaders. Results end in `AVATAR_SURFACE_TESTS_PASSED`; the game
log also reports `AVATAR_RENDER_TESTS_PASSED`. An `error.txt` means failure.

Test shaders and private model fixtures are never included in runtime packages.
