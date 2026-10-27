# ValheimVRM 2.0.6 validation

Client and server packages both use version **2.0.6**. This public release adds a
single F8 brightness calibration slider and retains the preceding mounted-player,
configuration-save and resident-template fixes.

## Behavior and compatibility

- The slider runs from 10% to 200% in 1% steps. Default/reset 100% preserves the
  existing MToon import limits: base color 0.45 and shade color 0.2025 in linear
  space. Old rendering JSON without `Brightness` uses 100%.
- Preview is live; release, menu close and plugin teardown persist the preference
  through the existing configuration writer. Other rendering toggles preserve it.
- Each player instance scales recognized base/shade, emission, rim, matcap and
  outline color properties after expression/fur updates, without editing shared
  materials, source bytes, alpha or render queues. Shader outputs without those
  color factors are outside this control; it is not a final-pixel exposure limit.
- The wire protocol remains version 1 with the existing capability negotiation,
  request sequence and snapshot revision. Brightness uses the existing reserved
  positional key `legacy:render.brightness`; neutral 100% omits the key and leaves
  the previous default payload byte-for-byte unchanged.
- The prior public client is **2.0.5**, and its matching server addon is **2.0.2**.
  Updated clients can synchronize brightness through that old server. Old clients
  can use server 2.0.6 and safely ignore the extension. Old senders default to
  100% on new viewers. Displaying adjustments requires sender and viewer 2.0.6.
- Received brightness belongs only to the named player instance. Same-model
  players keep independent values, and observers' personal preferences stay intact.
  Part-visibility sharing switches do not suppress brightness.

## Verification

Release client/server and the brightness/synchronization probes build without
warnings or errors. Standalone protocol tests cover range/nonfinite and malformed
carrier rejection, unchanged neutral payloads, canonical roundtrips, revision
deduplication and player isolation. Catalog/configuration tests retain Windows
1175 recovery, readonly/locked-file protection and failed-save session selection.
All 335 UniGLTF method references resolve for both VRM runtimes.

Real Valheim/Mono tests run in isolated Windows game directories with D3D11,
Valheim 1.0.12 / 1.0.20 and BepInEx 5.4.23.3 / 5.4.23.5:

| Client binary | Server binary | Coverage |
| --- | --- | --- |
| 2.0.6 | 2.0.6 | Full production synchronization suite, live brightness owner requests and independent same-model instances. |
| 2.0.6 | Previous 2.0.2 | Full suite including forwarding the additive brightness key, reordered requests/chunks, late join and reattachment. |
| Previous 2.0.5 | 2.0.6 | The unmodified preceding client's complete engine regression against the new server. |

The synchronization suite also covers older protocol capabilities, unmodded peers,
ZDO ownership, malformed input, model/hash mismatch containment, opt-out, native
character restoration, respawn and observers' unchanged personal configuration.

The brightness probe covers VRM 0.x/1.0 MToon, Standard and UniUnlit; independent
players/material slots; HDR and ordinary colors; source identity/alpha/queues;
existing renderer-wide and per-material blocks; reset/disable restoration;
100 repeated updates; and a production VRM 1.0 expression reset. HDR GPU readback
checks 10/50/150/200% RGB ratios and default restoration. Full runs retain the
previous import-limit and expression-baseline regression checks.

Local reports are under `artifacts/brightness-release`: `final-new-new`,
`final-published-old-server`, `final-old-new`, `final-legacy-brightness`,
`final-current-brightness` and `final-current-sync`. Brightness reports include
`AVATAR_BRIGHTNESS_CALIBRATION_TESTS_PASSED`; synchronization reports end with
`AVATAR_SYNC_ENGINE_TESTS_PASSED`. Expected injected configuration-save warnings
remain part of the persistence regression; unexpected runtime errors are failures.

## Packaging

The complete client, client DLL update, server and tagged-source archives are
verified for ZIP integrity, identical tested DLLs, matching version metadata and
SHA-256. The client retains all four embedded resources and unchanged UniVRM/
FreeImage/shader dependencies. Public packages exclude avatars, personal settings,
game/BepInEx binaries, test DLLs and the private encrypted-model implementation.
Existing 2.0.3/2.0.4/2.0.5 complete installations can use the client DLL update;
the new server archive contains only its plugin and documentation.

These are isolated engine/socket tests, not a live two-client Steam/PlayFab
acceptance run. Linux/macOS, other graphics APIs and arbitrary third-party shader
or multiplayer-mod combinations were not newly verified.
