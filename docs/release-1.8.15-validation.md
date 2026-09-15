# 1.8.15 validation — native character selection and back equipment

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1,
BepInEx 5.4.23.3, D3D11. Game probes run in the repository's ignored
artifacts/camera-qa/game. Evidence is under artifacts/action-calibration-qa;
packaging and delivery records are under artifacts/native-back-qa.

## Changes and cause

- F8 always offers Original character model, including an empty VRM library.
  An explicit empty selection is saved per character and bypasses automatic
  character-name/default VRM selection. Returning to VRM clears that choice.
- Restoration releases the VRM visual and controller spring references, restores
  native renderer/item/camera/physical baselines, and keeps other players and
  the client's synchronization setting unchanged. The empty model withdraws
  this player's server selection using the existing increasing request sequence.
- The old UpdateLodgroup postfix overwrote back-item local position and scale
  on equipment refresh, independently of initial avatar calibration. It also
  contained cumulative knife/staff rotation code. Removing those overrides
  preserves the transforms authored by native AttachItem/AttachBackItem,
  including attach_back and equipoffset.
- Back items follow the existing bone-derived body sockets. Each new item
  captures its authored transform once; height/2 m, the equipment multiplier
  and optional back scale produce absolute values. XYZ is applied in socket
  axes. Detach restores the captured transform. No avatar-name exceptions or
  native humanoid bone writes are added.
- The optional Back equipment group is collapsed by default, with zero XYZ and
  100% scale. Normal draw/sheath behavior does not depend on manual correction.
  The controls accommodate body proportions and clothing, save per model and
  synchronize per player without changing an observer's saved preferences.
- Reserved positional entries carry the additional back values in calibration
  format 1. Updated senders/viewers apply them; a production 1.8.14 relay also
  preserves them. Earlier clients retain supported behavior and neutral back
  defaults. Updating all participants to 1.8.15 is recommended.

## Verification

- Client, server and engine probe builds complete with zero warnings/errors.
  Catalog and pure synchronization tests pass, including explicit native
  selection with matching/default VRM files present, character isolation,
  calibration bounds, back roundtrip and neutral defaults for older payloads.
- native-back-actions-final exercises Shiro (VRM 0.x) and Shinano Light
  Adjustment (VRM 1.0), each at 1.4, 2 and 2.2 m. Actual native item attachment
  methods create Hammer, Hoe, SwordIron, KnifeFlint, BowHuntsman, ShieldWood,
  AtgeirBronze, SledgeIron and StaffFireball on original game socket hierarchies.
  Three draw/sheath cycles per item cover Movement, Emote_sit, SitChair,
  In Water, equip_hip and equip_head, with 30 stepped frames per state.
  Total: 29,160 equipment refresh checks. Position/scale/rotation remain at
  their absolute expected values, defaults and nonzero XYZ/scale work, and
  reset restores authored values. Hammer mesh bounds stay above the floor
  tolerance during the tested movement frames.
- The same six model/height cases exercise all 172 native animation states:
  30,960 repeated calibration updates have zero accumulated positional drift.
  Fixed-reference contact error stays below 0.019 m. Another 2,160 stepped
  locomotion frames keep additional baseline variation below 0.000001 m.
- native-back-sync-final runs the production 1.8.15 client and server through
  actual ZRpc with in-memory transport and real game player instances.
  Four VRM/native/VRM cycles restore renderers, capsule, camera pivot and
  interaction distance. Native selection persists on reload, sends one empty
  selection with a growing sequence, suppresses height-based reattachment,
  leaves player B unchanged and resumes publication after choosing VRM again.
- The production relay accepts an enabled empty model as withdrawal and rejects
  an older delayed nonempty selection. Existing tests also cover disabled sync,
  same-model independent players, late join, respawn, reversed requests/chunks,
  invalid packets, missing/different model libraries and unmodded peers.
- Sender controls produce nine complete sequenced requests when individually
  changing standing/sitting, action XYZ, left/right/two-handed/back scale/XYZ
  and physics weight; unchanged values send nothing. Twelve received updates
  retain player instances and group objects, modify A only and leave observer
  configuration files unchanged. Remote values survive external reattachment.
- native-back-sync-1 and native-back-sync-final cover the shared VRM 0.x player
  fixture; native-back-sync-old-server covers the shared VRM 1.0 player fixture.
  The latter loads the unmodified public 1.8.14 server DLL with the new client
  and passes back-value relay, player isolation and native selection tests.

## Limits

These probes use game animation, rendering geometry and production RPC code;
they do not establish pixel-level visual correctness of every avatar or of the
expanded IMGUI layout. Hidden batch-mode windows do not provide reliable UI
screenshots. Public-network Steam/PlayFab sessions were not exercised in this
release's automated tests.

Back placement follows fixed authored sockets and the avatar skeleton. It is
not continuous collision detection against clothes; unusual proportions may
still need optional offsets. Posture calibration retains the stable reference
behavior and limits documented for 1.8.14. Source avatars and game assets are
not included in public packages. Local numbered Windows client/server bundles
retain their BepInEx prerequisites and existing model distribution packages.
