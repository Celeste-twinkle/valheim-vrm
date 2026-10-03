# ValheimVRM 2.0.5 validation

This client-only release fixes mounted players appearing at the creature's origin
to observers while the rider's own view remains correct. It starts from public
2.0.4 and retains its configuration-save repair and the 2.0.3 template residency
behavior. No server, shared protocol, configuration schema or model format changes
are included. The existing 2.0.2 server addon remains compatible.

## Cause and compatibility

The native `ZSyncTransform.OwnerSync` sends the attachment joint only when the
parent ZDO changes. Standing on a creature and then riding that same creature
keeps the parent unchanged. The riding position becomes zero relative to the
saddle, but the joint can remain empty in the transmitted ZDO. The native receiver
then places the rider at the creature's origin. The local `Player.UpdateAttach`
still follows the saddle, explaining the different views.

`PlayerAttachmentSync.cs` adds two guarded postfixes:

- An owner compares the native attachment joint after synchronization and writes
  the existing game field only when it changed. This also clears a stale joint on
  dismount without changing the parent. Native/older receivers use the correct
  joint without needing this mod's fallback.
- An updated observer recovers the saddle position when an older sender omitted
  the joint. It requires the connected creature's active saddle, matching network
  user and the player's synchronized native riding animation. It changes only the
  remote transform after native parent/rotation synchronization; received ZDO data
  is unchanged.

Both paths use existing game data. Updating the rider fixes transmission to older
observers; updating the observer covers the missing-joint case from an older rider.
Two clients without this repair retain the native defect.

## Validation on 2026-10-04

The exact same Release DLL, built against Valheim 1.0.16, passed in both isolated
Windows x64 / D3D11 runtimes:

| Game | Unity | BepInEx core / Valheim pack | Result |
| --- | --- | --- | --- |
| Valheim 1.0.16 (network 40) | 6000.0.75 | 5.4.23.5 / 5.4.2351 | Passed |
| Valheim 1.0.12 (network 40) | 6000.0.75 | 5.4.23.3 / 5.4.2333 | Passed |

Each run used real `Player.GetRelativePosition`, native owner/remote position
methods, ZDO serialization/deserialization and scene lookup. Harmony reverse
patches executed unmodified game methods for the old sender and native receiver.
Creature/saddle hierarchies were synthetic, with the saddle about 3.132 m from
the creature origin; the player's native animator came from the game's prefab.

Each runtime passed:

- Reproduce the unmodified same-parent mount bug, then verify repaired sender with
  native receiver and old sender with repaired observer. Verify received-data
  immutability.
- 60 same-parent mount/dismount cycles with changing positions/headings and
  20/1200 m altitude, without drift. 100 unchanged sends do not increase
  `DataRevision`.
- Standing, another saddle user, inactive or unloaded saddle, late mount loading,
  non-owner writes and complete detach guards.
- VRM 0.x and VRM 1.0 at 1.4, 2.0 and 2.2 m heights, totaling 540 moving/turning
  samples per runtime. An independent baked-mesh sampler measured at most
  0.000925 m seat-support error while preserving the configured 0.07 m model
  offset plus 0.08 m sitting offset.

Both reports end with `AVATAR_ATTACHMENT_ENGINE_TESTS_PASSED`; neither Unity log
contains unexpected errors or exceptions. The client and engine test plugin built
with zero warnings and zero errors. The unchanged catalog/configuration tests
passed, including Windows 1175 fallback, partial replacement recovery and
readonly/held-open file protection. The standalone protocol tests passed ordering,
per-player isolation, calibration/part roundtrips, old format-1 relay compatibility,
respawn/reconnect/late join and invalid-data rejection. All 335 UniGLTF references
resolved for VRM 0.x and 1.0 against each game installation.

DLL version: `2.0.5.0`. SHA-256:
`a26539e4dfc3f682d510bf072a0bb1a8c3a19c741537eaa0c85670e885a9040e`.
All four embedded resources are retained: `avatar_bloom`, `avatar_rendering`,
`avatar_fur` and `player-animation-catalog.json`.

## Packages and reproduction

The complete and DLL-only update packages contain the same tested DLL. The source
archive includes the regression plugin and documentation from the tagged public
release commit. Package checks cover ZIP integrity, matching DLLs, dependency and
shader identity, source contents and SHA-256. Game assemblies, avatars, personal
configurations, test binaries and private encrypted-model implementation are
excluded from runtime packages. Asset sizes and SHA-256 digests are verified during
upload before publishing the GitHub Release.

Use `ValheimVRM-2.0.5.zip` for a complete installation and install BepInEx 5
separately. A complete 2.0.3 or 2.0.4 installation can use
`ValheimVRM-2.0.5-update.zip` after exiting the game. Keep existing models and
configuration; no migration or deletion is required. Existing servers need no
update.

See the [regression plugin instructions](../tests/AvatarAttachmentEngineTests/README.md)
and [Chinese cause/fix explanation](MOUNTED-PLAYER-SYNC.zh-CN.md). Local reports are
`artifacts/riding-release/current-engine/results.txt` and
`artifacts/riding-release/legacy-engine/results.txt`.

These isolated tests do not constitute a two-client live multiplayer session.
Steam/PlayFab transport, Linux/macOS, other graphics APIs and all combinations of
third-party mount mods were not newly verified.
