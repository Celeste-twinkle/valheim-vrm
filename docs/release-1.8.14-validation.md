# 1.8.14 validation — synchronized animation and held-item calibration

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1,
BepInEx 5.4.23.3, D3D11. All game probes use the repository's ignored
artifacts/camera-qa/game; evidence is under artifacts/action-calibration-qa.

## Behavior

- The extracted name-only catalog contains 172 states across two layers and
  434 distinct clip names. Runtime HasState and clip membership validate entries;
  newly encountered states can be named dynamically and receive a stable fallback.
  No proprietary animation curves, player prefabs or avatars are distributed.
- All states receive an initial reference. Ordinary grounded movement shares
  rest-skeleton hip/sole clearance; seats/saddles, ground sitting/kneeling,
  swimming head level, hand contacts and reclining use fixed sampled references.
  Upper-body layers inherit lower-body automatic support and add only their own
  manual adjustment. Enter/exit clips use their stable resting posture.
- Sampling uses a temporary native hierarchy without gameplay scripts/events.
  It never poses the live native skeleton. Target transforms are restored
  verbatim. Hip-relative measurements remain in unscaled coordinates; playback
  reads them and applies absolute current-height values and transition weights.
  Animated feet are never sampled during playback.
- F8 state/clip XYZ defaults to zero and ranges from -0.5 to +0.5 m. Clip
  adjustments are normalized within each state before transition blending.
  Character axes are independent of animated hip rotation.
- Palm-based item mounts remain generic. Each held item's authored position,
  rotation and scale is captured once. Height/2 m, legacy scale and one selected
  group multiplier produce its absolute size. Left/right/two-handed groups
  have 25–200% scale and independent XYZ. Two-handed types include bows.
  Changing or removing the binding restores authored item/socket transforms.
- JSON stores the owner's personal per-model values, never bones. All calibration
  controls, standing/sitting offsets and physics weight share model/height's
  sequenced state. Receivers bind fresh settings/profile objects to each player
  and update existing profile groups in place. No remote preference files are
  written. VRM 0.x and 1.0 use the same implementation.

## Engine verification

- Client, server and probe builds: zero warnings/errors.
- actions-sync-release (with synchronized controls): Shiro and AliciaSolid (VRM 0.x), Shinano Light Adjustment and
  Moe (VRM 1.0), each at 1.4/2/2.2 m. Every fixture/height exercises 172 states,
  XYZ application/reset and 5,160 repeated updates. Total: 61,920 updates,
  zero accumulated positional drift. Source and target setup transforms remain
  unchanged. Sampled initial seat/ground/hand/head error is below 0.019 m.
- The same fixtures complete 4,320 stepped locomotion frames. Additional
  baseline variation is below 0.000001 m. No gait-following correction occurs.
  Initial calibration timings are recorded per fixture/height in the evidence.
- blend-anchor additionally covers both VRM generations at all three heights:
  native swimming and boat hand anchors, common offsets on blended movement
  clips, and state-transition weights. Equal state offsets neither double nor
  disappear during transitions. Another 30,960 repeated updates have zero drift.
- repeat-final: 72 fresh height changes from standing, ground-sit and chair
  loading poses, with double application of the same requested height and
  2,160 subsequent updates. Same-height/pose drift is zero; native hips,
  shared imports and other instances remain unchanged.
- fullpose-sync-release: 660 fixed-reference samples across scales and translated,
  rotated roots; independent +17/-8 cm standing/sitting offsets. Reference
  standing error is below 0.034 m. Both hands/seven body sockets retain native
  bone protection and restoration. Existing VRM 0.x physics-weight tests pass.
- Item checks use native shield/sword/bow/atgeir type metadata and deliberately
  non-unit authored transforms to detect overwritten or compounded scale.
  All heights pass repeated scaling, XYZ preview, group isolation and restore.
- Actual ZRpc production relay/client tests cover independent same-model player
  heights, 12 consecutive local RequestHeight actions, reversed/stale packets,
  missing/different model folders, respawn and unmodded/old-server fallback.
  Calibration capability adds snapshot format 3 while retaining negotiated
  legacy formats. Unmodded peers receive at most twelve discovery packets over
  three rounds and no avatar snapshots.
- sync-calibration-verified and sync-calibration-vrm1 repeat the production
  client/server suite with VRM 0.x and VRM 1.0 as the shared two-player model.
  Sender tests individually change standing, sitting, action XYZ, all three item
  scale/XYZ groups and physics weight: eight complete requests use sequences
  1–8; unchanged values send nothing. Twelve received in-place updates retain
  each model and held-item group object, change only A, and leave every observer
  config file byte-identical. External reattachment retains received values.
- Relay tests reverse actual ZRpc selection delivery and large snapshot chunks,
  reject malformed and capability-downgrade requests before sequence advancement,
  and verify independent same-model values, old-client height fallback, late
  join, respawn, opt-out and listen-host settings. Production ReceiveStateChunk
  preserves the old state until all parts arrive, then accepts one full revision.
  Mixed revisions, duplicate chunks and forged byte-array lengths are rejected.
- Pure BCL tests verify canonical encoding/roundtrip, offset/scale/physics ranges,
  NaN/infinity rejection, payload/entry limits, unchanged revision suppression
  and independent player state. No Unity/client assembly is required by this data.
- Preferences pass save/reload, model isolation, finite bounds and default/reset
  checks. Existing TXT persistence retains other fields and comments.

## Limits

Fixed references preserve animation motion; they are not continuous hand/foot
IK and cannot guarantee contact for every body proportion or every animation
phase. Small intersections/gaps remain adjustable. Calibration settings are
shared per player when server, sender and viewer support 1.8.14+. Automatic
references are computed from the same VRM and height on each viewer. Animation
playback and spring simulation retain the game's normal network/local timing;
this protocol synchronizes settings, not every simulated bone transform.

Expanded F8 layout still needs visual checking in an interactive game window:
hidden-window screenshot attempts did not execute IMGUI and are not counted
as successful visual verification. Runtime offsets, persistence and player
height UI actions were tested programmatically.

Public-network Steam/PlayFab dedicated sessions, non-Windows graphics backends
and arbitrary third-party controller replacements were not comprehensively
verified. Public packages exclude avatars and BepInEx; local numbered client
and server bundles retain their existing Windows BepInEx prerequisites.
