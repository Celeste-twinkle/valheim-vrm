# 1.8.17 optional GPU fur validation

Environment: Windows x64, Valheim 1.0.12 / Unity 6000.0.75f1, D3D11,
BepInEx 5.4.23.3. Shader bundles compiled using an isolated Unity 2022.3.22f1
project with no avatar assets. Private fixtures are excluded from public source
and runtime packages.

## Import, macro and lifecycle

- VRM 1.0 and native VRM 0.x fixtures import optional per-material extras through
  their existing brightness-limited material generators.
- Absent, explicitly disabled, zero-length and unknown-version variants create no
  fur surface, material, shader bundle or lease. The legacy suite also checks
  invalid field types and missing image references; the base avatar remains usable.
- Enabled overlays share the original mesh. At 1.4 m, cloned renderers and bones
  refer to their own avatar, while shared material resources remain owned by the
  template. Destroying the clone does not unload the live template's resources.
- The `AVATAR_FUR_ON` disabled variant renders exactly the same RGB pixels as
  disabling the overlay altogether. Enabling it changes 11,665 pixels in the
  VRM 1.0 fixture and 10,772 pixels in the legacy fixture at the tested viewpoint.
- Destroying the last imported owner releases the fur materials, decoded masks
  and optional `avatar_fur` bundle. Ordinary avatars do not load this bundle.

## Temporal bloom and antialiasing

Both fixtures were rendered for 32 warmup frames followed by **64 consecutive
recorded frames**, under actual TAA with strong scene lighting and scene bloom,
while avatar bloom was excluded. The same geometry/alpha path draws the fur
coverage and visible color.

| Fixture | Absolute mask coverage range | Relative coverage range |
| --- | ---: | ---: |
| VRM 1.0 | 0.0003724694 | about 0.078% |
| VRM 0.x | 0.0002318621 | about 0.108% |

Both passed the existing less-than-1% coverage-dropout criterion. The test also
checks that changing alpha on Opaque materials does not change final scene/bloom
coverage. These are controlled continuous-frame regressions, not a guarantee
against every game's post-processing or transparency combination.

## Geometry and calibration

The private conversion fixture's sweater retains its original **15,553 vertices**;
no fur mesh is written to the VRM. The shader overlay references the same mesh.
The embedded masks and metadata add about 61 KiB to the fallback VRM.

Repeated height testing covers 36 changes among 1.4, 2.2 and 2.0 m, with standing,
ground sitting and chair sitting, duplicate application of each height and 30
pose updates per change. Maximum same-height/pose drift was **0 m**; native hips
and the imported template remained unchanged.

The normal animation/equipment suite also passed 330 fixed-reference pose samples
at three scales and two translated roots, with a maximum reference contact error
of 0.024767 m. Both hands, seven body equipment sockets, native skeleton protection,
274 moving spring joints and 57 cloned spring chains passed the existing checks.

## Limits

GPU fur still costs overlay skinning, geometry processing and transparent
overdraw. This release does not claim a measured multiplayer FPS improvement,
general GPU-memory benchmark, all GPU/API compatibility, full lilToon feature
parity or order-independent transparency. Unsupported devices keep standard
MToon fabric. The server synchronization protocol is unchanged; the server does
not render or download fur assets.

Reproduction: `tests/AvatarFurTests/README.md`,
`tests/AvatarAntialiasingTests/README.md`, `tests/AvatarPoseTests/README.md`.
