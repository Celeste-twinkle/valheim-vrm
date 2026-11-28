# 1.8.20 respawn calibration validation

The local avatar initialization in `Player.Awake` is deferred until the next
frame, after `Game.SpawnPlayer` assigns `Player.m_localPlayer` and restores the
character profile. Previously a cached import attached synchronously during
Awake and selected the default height; a cold import usually hid the ordering
bug by yielding during file IO. Remote unchanged-selection checks now verify
the calibration on the actual visual as well as its model and height.

Death ragdolls retain their instance's synchronized settings and are detached
from live-player tracking. Unfinished attachments stop when the player dies;
native ragdolls without a VRM are left alone. The recreated avatar initializes
equipment bindings even when the equipment hierarchy is temporarily inactive.
No calibration, height or equipment offset is accumulated across respawns.

Validated with Valheim 1.0.12, BepInEx 5.4.23.3, Windows x64 and D3D11:

- The new engine regression reproduces the cached-Awake height reset with the
  installed 1.8.19 DLL and passes with 1.8.20.
- Three cached incarnations and a fourth cold-import incarnation retain the
  local character's selected model, 1.4 m height, standing/sitting/model offsets,
  animation/clip profile, all four equipment profiles and height scaling.
  Equipment and animation consumers reference the restored profile.
- Remote reincarnations bind the new character ZDO ID to the retained 300-entry
  animation calibration, item scales/XYZ, physics weight and height. Another
  player using the same model stays at 2.2 m with different controls and keeps
  the same visual instance. The owner's settings do not overwrite the observer's
  preferences or the other player's settings.
- Reset remote calibration is repaired from an unchanged authoritative snapshot;
  a missing binding causes a fresh correctly bound visual to be attached.
- The production ragdoll hook removes the corpse from live-player tracking while
  retaining its per-instance settings and height. Real ZRpc owner requests retain
  complete settings and emit no default/empty updates during death or duplicates
  on unchanged respawn. Server tests retain calibration and request sequence
  watermarks when rebinding to a new character ID.
- The full existing engine synchronization suite passes, including reverse-order
  messages, chunking, independent players, host relay, late join, missing/mismatched
  model folders, unmodded clients, native appearance and local-only mode.
- Client/server builds have zero warnings/errors. Codec and registry tests pass;
  runtime dependency inspection resolves all 335 UniGLTF method references.

Reproduce by building the client, server and `tests/AvatarSyncEngineTests`;
install the test DLLs only in an isolated game copy. Set `VRM_SYNC_TEST_OUTPUT`
to a fresh output directory, use a separate `-savedir` and a real graphics device.
The report must end with `AVATAR_SYNC_ENGINE_TESTS_PASSED` and contain no
`error.txt`. Default fixtures are Shinano_LightAdjustment and Shinano_Sleep;
`VRM_SYNC_TEST_MODELS=Shinano_LightAdjustment|AliciaSolid` exercises the same
respawn regression with a VRM 0.x target. Codec tests run with
`dotnet run --project tests/AvatarSyncTests -c Release`.

These are actual game player/avatar instances and production hooks/RPCs with
controlled character-identity and death state, using in-memory sockets; they do
not create a new public Steam/PlayFab multiplayer session or play through a
complete survival-world death. Rendering code is unchanged. Private models,
game assemblies and test outputs are excluded from the public packages.

Configuration and protocol formats are unchanged. Both the avatar owner and
observers should update their clients to 1.8.20. The matching server package is
provided; 1.8.19 servers can relay the same calibration payload.
