# 1.8.0 server sync and spring weight validation

Windows x64, D3D11, Valheim 1.0.7 / Unity 6000.0.75f1, BepInEx 5.4.23.3.
Game tests run in a disposable, ignored installation under the repository's
`artifacts/` directory with separate saves; no test DLL is installed in the
normal game. Model assets are private fixtures and are not shipped in this repo.

The extended AvatarPoseTests exercise two VRM 1.0 outfits (167 and 235 spring
joints) and the independently authored AliciaSolid VRM 0.x fixture (30 joints).
For each fixture, 240 translated/rotated frames switch through 100%, 50%, 25%,
0%, 50%, and 100%. Angular displacement follows the requested fraction within
0.06 degrees. Restoring the simulation pose changes no original hierarchy
rotation beyond numerical precision (under 0.00002 degrees). Rebinding removes
the display correction. Zero weight retains the simulation's internal state so
raising the weight does not require reconstructing spring chains.

Settings checks cover save/reload at 0/25/50/100%, clamping and nonfinite inputs.
The same fixtures also run sitting, scaled palm attachment and clone-reference
checks. Release compilation, catalog tests and the runtime dependency audit are
included. These are controlled engine probes, not a complete world playthrough.

The slider scales rotation away from the imported spring rest pose. It does not
add cloth collision, modify avatar geometry, or create physics absent from the
VRM. Interpolating a collision-resolved spring pose can change contact with
clothing, so avatar-specific clothing clearance still belongs in the asset.

Reproduce with the AvatarPoseTests instructions in `release-1.7.2-validation.md`.
The new physics probe is included in that test DLL. Do not deploy test DLLs to
the ordinary game installation.

## Server synchronization checks

The pure registry suite checks independent A/B selections, rapid changes, duplicate
character rejection, respawn IDs, disconnect/reconnect, local opt-out and bounded
names/hashes. Character names are never keys.

AvatarSyncEngineTests run in the isolated game with the production server DLL.
The actual ZRpc serializer and server handlers use in-memory test sockets and
controlled character ZDOs. Tests cover the handshake, three initial receivers,
forged character ownership rejection, latest-choice coalescing, a later fourth
receiver, respawn, opting out, disconnect cleanup and a listen-host selection
submitted before the server's first Update. The server does not load
VRM assets.

Two inactive game Player fixtures then import and attach different VRMs using
the client's real switching coroutine. A changing to the same asset as B leaves
B's avatar object unchanged; skins reference their own clone's bones. Remote
capsule dimensions and the local selection file stay unchanged. Client checks
also reject stale revisions, exclude the local player from remote replacement,
and match the new character ID after respawn.

These probes do not include a real Steam/PlayFab login to an external dedicated
server, WAN conditions, a full multiplayer world playthrough, or Linux. The
server package is provided for that deployment; no user server address or access
was available for live acceptance testing.

Build `tests/AvatarSyncEngineTests`, copy its DLL and the server addon into only
the isolated game's plugin directory, and set `VRM_SYNC_TEST_OUTPUT` to an ignored
output folder. Provide `Shinano_LightAdjustment.vrm` and `Shinano_Sleep.vrm` as
private local fixtures. Launch as documented for the earlier pose suite. The
probe exits with `results.txt`, or `error.txt` if an assertion fails.
