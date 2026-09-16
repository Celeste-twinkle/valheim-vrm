# 1.8.19 offset range validation

The UI, local clamp and shared client/server codec now use one **1 metre**
per-axis offset limit. Standing/sitting offsets, animation state/clip XYZ and
left/right/two-handed/back equipment XYZ retain 1 cm steps and zero defaults.
No automatic calibration, animation, coordinate frame, height or scale formula changed.

Validated on Windows x64, Valheim 1.0.12, BepInEx 5.4.23.3 and D3D11:

- Codec round trips preserve −100, −75, +75 and +100 cm for every offset group.
  Values outside ±100 cm, NaN and infinities remain invalid. Malformed payloads,
  ordering, duplicate rejection and independent player registry state pass.
- In an isolated game copy, actual local preference save/reload preserves the
  extended animation and all four equipment-group offsets. Local clamping keeps
  valid values intact, clamps outside values to ±1 m and resets nonfinite values.
- Real ZRpc serialization and the production server relay transmit a complete
  ±100 cm calibration. Reversed requests keep the later sequence, and host relay,
  late join, chunking, respawn, opt-out and unmodded-client checks pass.
- Two actual player instances using the same model retain independent values.
  Applying −100, −75, +75 and +100 cm to A preserves standing/sitting, animation
  XYZ and all four equipment XYZ groups without reloading the model or affecting
  B or the observer's preferences. Existing in-place update and ownership checks pass.
- The existing engine sync suite also passes model switches, missing/mismatched
  libraries, native-model restoration, disconnect/reconnect and local preferences.
- Release client/server builds complete without warnings or errors. Runtime
  dependency inspection resolves all 335 checked UniGLTF method references.

Reproduction: `dotnet run --project tests/AvatarSyncTests -c Release`;
build `tests/AvatarSyncEngineTests` after the client/server, install those DLLs
only into an isolated game copy, set `VRM_SYNC_TEST_OUTPUT`, and launch with a
real graphics device and isolated save directory. The engine report must end
with `AVATAR_SYNC_ENGINE_TESTS_PASSED`; an `error.txt` indicates failure.
Private fixtures and game references are not included in the public packages.

The packet layout is unchanged, but **sender, observers and server addon need
1.8.19+** to use offsets outside the old ±50 cm range. Older releases may reject
or clamp that calibration. Tests use real game RPCs over isolated in-memory
sockets; this is not a new public-network Steam/PlayFab session or a rendering
regression suite. Existing shader/rendering validation remains in the 1.8.18 record.
