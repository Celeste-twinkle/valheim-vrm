# ValheimVRM 2.0.0 validation

Validation date: 2026-09-22 (Asia/Shanghai).

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1, BepInEx
5.4.23.3 and D3D11. The isolated game copy is under the repository's ignored
`artifacts` directory and is not part of the Unity authoring project or release.

## Build and bounded protocol tests

- Client and server `net471` Release builds completed with zero warnings/errors.
- The calibration codec round-tripped compact hidden and shown sets (19 SHA-256
  part IDs, grouped into bounded reserved entries), rejected malformed data and
  preserved canonical format-1 encoding.
- Production registry/wire tests retained per-player isolation, monotonic request
  ordering, stale/duplicate/downgrade rejection, late join, reconnect, respawn,
  opt-out, hash/path validation and the existing 768-entry / 128 KiB limits.
- Reserved part entries use a neutral zero vector. They do not consume the older
  ±50 cm offset allowance and do not change protocol version 1 or calibration
  format version 1.

## Engine integration

The production client/server DLLs and actual imported VRMs ran in an isolated
Valheim process. A mixed `AliciaSolid` / `Shinano_LightAdjustment` run covered the
legacy and current VRM import paths and ended with
`AVATAR_SYNC_ENGINE_TESTS_PASSED`.

- Enumerated every actual renderer with stable, unique hierarchy/component IDs;
  GPU fur overlays were not duplicated in the menu list.
- Hid a visible part, explicitly showed an exporter-inactive hierarchy node, and
  restored authored GameObject/Renderer defaults. Reapplication was absolute and
  did not accumulate.
- Persisted per-model shown/hidden overrides and safely ignored an unknown ID.
- Verified that disabling outgoing sharing omitted all reserved part entries while
  keeping local choices, and disabling incoming application restored remote model
  defaults without writing personal configuration.
- Applied independent states to same-model players and retained them through
  cached updates, model/height reattachment, late join and missing-file recovery.
- Ran three cached respawns and one cold-import respawn. Local choices, remote
  choices, death ragdolls, complete animation/clip calibration, all four equipment
  groups, physics and independent 1.4/2.2 m heights survived without crossing
  player instances or publishing temporary defaults.
- An unchanged snapshot repaired a reset or missing binding; personal preference
  files remained byte-identical throughout remote operations.
- The unmodded-client probe received no avatar snapshot, completed 60 ordinary
  request/reply exchanges and observed no error or disconnect.

## Old-version compatibility

- A full engine run used the 2.0 client with the released 1.8.20 server DLL. It
  relayed the reserved part state, passed synchronization/respawn/isolation tests
  and ended with the normal pass marker.
- Client assemblies built from tag `v1.8.14-fork.1` and from the released 1.8.20
  package both decoded and re-encoded 2.0 hidden/shown reserved keys unchanged.
  Their old avatar logic has no matching animation state, so it ignores the keys
  and keeps authored part visibility.
- The 2.0 production server probe accepted old model-only, height and calibration
  clients, kept legacy capability snapshots separate, and preserved unmodded
  admission behavior.

This covers local isolated client/server behavior and the packet compatibility
boundary. A public Internet dedicated-server login, Linux server runtime and
arbitrary third-party mod combinations remain deployment checks rather than claims
of this Windows test run.
