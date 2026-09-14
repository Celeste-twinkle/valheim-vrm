# 1.8.12 validation — adjustable and synchronized avatar height

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1,
BepInEx 5.4.23.3, D3D11. The isolated game is under the repository's
ignored `artifacts/camera-qa/game`; test output is in `artifacts/height-sync-qa`.

## Implementation

The importer caches unscaled visible mesh height before animation. Personal
target height (1.4–2.2 m, default 2 m) is applied only to a player clone using
target/original height. The F8 slider applies on release by reattaching a fresh
clone of the cached import. Normal attachment recalculates the humanoid standing
reference, seated geometry, camera and attachment references. There is no mesh
remeasurement from an already animated avatar or accumulated scale adjustment.
Standing/walking/running keep the fixed calibrated hip offset and native motion.

Local height is stored by game character in `avatar_heights.json`. Remote
height is held only in that authenticated player's network selection. Shared
templates, per-model settings and another player's preferences are unchanged.
Ordinary remote model refresh also reads the current synchronized height.

HeightHello negotiates selection packets with sequence + height, and snapshot
format 2. Legacy peers retain their previous format and default remote height.
Height and model share a single ordered request; full validation precedes order
advancement. A delayed legacy message cannot downgrade an upgraded connection.

## Checks

- Client and server Release builds: no errors or warnings.
- Pure registry/order tests: independent same-model player heights, both bounds,
  height-only revisions, no-op requests, NaN/infinity/out-of-range rejection,
  authenticated identities, respawn, disconnect and reconnect.
- Real ZRpc and production server/client handlers: capability negotiation,
  1.4/2.0/2.2 m choices, reversed request delivery, stale/duplicate rejection,
  invalid high-sequence packet isolation, opt-out, host bridge, legacy fallback,
  late legacy snapshot rejection and bounded discovery for unmodded peers.
- Production player-clone checks: A repeatedly changes between 1.4/2.2/2/1.4 m
  while same-model B remains 2 m; shared import/settings and local preferences
  remain unchanged. Missing/different/unreadable VRM files retain safe fallback.
- Local F8 action uses the production picker's RequestHeight path, applies 2.2 m,
  saves/reloads the character preference and leaves the other player unchanged.
  An ordinary external remote reattachment retains its synchronized height.
- Four fixtures (Shinano Light Adjustment, Moe, KUMALY 2 and VRM 0.x AliciaSolid):
  4,320 continuous Unity start/walk/run/stop frames at 1.4, 2.0 and 2.2 m.
  Additional avatar-minus-native hip variation is at most 0.000001 m.
  Native bone positions remain unchanged.
- 900 independent baked-mesh contact checks initialize from standing, ground
  sitting and chair sitting and sample standing, ground-sit transitions and
  chair sitting. Maximum sampled contact error is 0.027805 m (standing);
  seated contacts remain within the 0.008 m test limit. Small standing sole
  gaps/intersections are retained instead of adding gait-dependent correction.

Evidence: `pose-1/results.txt` and `sync-final/results.txt` under the output
directory. An initial isolated run could not initialize Steam while Steam was
closed; it did not execute the test suite. The subsequent runs used Steam.

The network probes exercise real game RPC serialization and production handlers
with isolated sockets/player fixtures. A public-network Steam/PlayFab dedicated
server login and Linux deployment were not performed for this update. Actual
furniture, poses and model proportions can differ from these fixtures; this
does not implement terrain IK or guarantee exact sole contact in every pose.
