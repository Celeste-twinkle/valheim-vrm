# 1.8.18 material, fur and equipment validation

Environment: Windows x64, Valheim 1.0.12 / Unity 6000.0.75f1, D3D11,
BepInEx 5.4.23.3, RTX 5090 D. Shader bundles were built in an isolated
Unity 2022.3.22f1 project. Game probes use an isolated copy and save directory
under ignored repository artifacts; private avatars are excluded from release assets.

## Common shader surfaces

The shared surface suite passes **180 depth-consumer checks** across
UniGLTF/UniUnlit, the game's Standard shader and Unlit/Texture. Cases include
Opaque, Cutout, Blend and Standard premultiplied blending; texture alpha
0/0.35/1; enabled/disabled UniUnlit vertex alpha; and Standard smoothness-channel
keywords. Original shader identity and linear scene color are preserved, invalid
early transparent queues are repaired, and bloom masks follow surface coverage.

A missing-depth UniUnlit control reproduces the background overlay. The corrected
path blocks depth-consuming effects behind solid surfaces while retaining front
effects and real transparent backgrounds. The controlled cloud/water-like test
pass validates the camera-depth contract; it is not an exhaustive native weather,
water-shader or multiplayer capture. Unlit/Transparent and Unlit/Transparent Cutout
are absent from this game's Shader.Find inventory and are not claimed as tested.

Native PBR GBuffer surfaces keep their own lighting and clipping. A diagnostic
found that the game's retained Standard variants still use texture alpha for
transparency when a smoothness-channel keyword is toggled. Treating that keyword
alone as proof of solid coverage incorrectly changed scene color; the final
implementation does not make that inference. Opaque mode always ignores stored
alpha. Arbitrary custom alpha formulas, shader displacement and missing shader
variants still require an appropriate adapter; common property names are not a
guarantee of arbitrary custom shader parity.

Both native VRM 0.x MToon and MToon 1.0 additionally pass the existing shadow,
scene-lighting and SSAO composition suite, including HDR, animated UVs, foreground
walls, texture holes, partial alpha and all three alpha modes. Surface preparation
does not change the authored scene RGB in the color-preservation cases.

## Continuous antialiasing and bloom

Each case records **64 consecutive frames after 32 warmup frames** with TAA,
strong scene lighting, scene bloom on and avatar bloom excluded. Metrics use
floating-point linear pixels, not single screenshots.

| Fixture | Absolute coverage range | Approximate relative range |
| --- | ---: | ---: |
| UniUnlit mixed materials | 0.0002644807 | 0.124% |
| UniUnlit all partial Blend | 0.0002089590 | 0.128% |
| Standard mixed materials | 0.0002644807 | 0.124% |
| Standard all partial Blend | 0.0002102405 | 0.129% |
| VRM 1.0 MToon with fur | 0.0003724694 | 0.078% |
| VRM 0.x MToon with fur | 0.0002318621 | 0.108% |

All pass the less-than-1% relative coverage-dropout criterion. Common-shader
fixtures are converted in memory only. Opaque alpha 1/0.5 also preserves final
RGB with avatar bloom included and excluded. This is a coverage regression,
not a claim that every output pixel remains constant under temporal filtering.

## Optional fur lighting and lifetime

Nine lighting cases for each MToon generation compare the original cloth with
zero-length fur: directional, tangent-space normal map, two lights, emission,
shading map, point light, MatCap/rim, MatCap and rim. All **18** pass the 1.5%
mean-relative-error gate. The normal-map regression was about **20.18%** with
the old shader and approximately **0.0001%** after repair. The largest final
mean error is about **0.2241%**, in the legacy two-light case; overlapping
transparent fins still have local multipass differences.

Legacy HDR rim colors are converted to the representation expected by the fur
material, while the two MToon generations use their own lighting formulas.
The effect is not a complete lilToon implementation.

Both VRM generations pass absent/disabled/zero-length/unknown-version imports,
enabled fur, cloning and final-owner cleanup. Invalid extension fields or images
retain the base avatar. The disabled macro produces identical pixels to disabling
the overlay. Enabled fur shares the source mesh; the private sweater fixture
still has 15,553 authored vertices and needs no re-export. Ordinary avatars do
not acquire the optional fur bundle or per-frame fur component.

## Character axes and menu

Two real humanoid fixtures, one per VRM generation, exercise heights 1.4/2/2.2 m.
Four held item types rotate the character and wrist over 90 updates at each
height. Nine back items cover three draw/sheath cycles, six animations and
30 frames at each height: **2,160 held-item updates and 29,160 back-item checks**.
Character-axis offsets, automatic height scaling, repeated absolute application,
reset and native attachment restoration pass. Existing network values remain
per-player; both sender and viewer need 1.8.18+ for the new coordinate meaning.
This release does not change the server relay protocol.

The F8 window uses an alpha-1 dark background for every GUI window state and
restores the caller's GUI colors afterward. Its texture is released on teardown.
The client and server build cleanly; 335 UniGLTF method references resolve.

Reproduction: `tests/AvatarRenderingTests/README.md`,
`tests/AvatarAntialiasingTests/README.md`, `tests/AvatarFurTests/README.md`,
`tests/AvatarPoseTests/README.md`. This environment does not validate Windows 10,
RTX 4080, Vulkan/macOS/Linux, all third-party mods or order-independent transparency.
