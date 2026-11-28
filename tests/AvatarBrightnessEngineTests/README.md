# Avatar brightness calibration regression

Build the Release client, then this test project against the same Valheim/BepInEx
references. Run the plugin only in an isolated game copy with a real D3D11 device,
an isolated `-savedir`, and `VRM_BRIGHTNESS_TEST_OUTPUT` pointing at a new output
directory. The plugin disables cloud storage and exits after reporting results.

The calibration probe checks old/missing JSON defaults, save/reload, rendering
toggle preservation, range/nonfinite guards, both MToon generations, Standard and
UniUnlit, HDR colors, multiple material slots, independent players, pre-existing
renderer/material property blocks, source material identity, alpha, render queues,
reset/disable restoration, and 100 repeated updates without accumulation. It also
checks a production VRM 1.0 import across expression changes and HDR render/readback
ratios at 10/50/150/200%, followed by the unchanged 100% baseline.

Provide the existing private `Shinano_LightAdjustment.vrm` fixture in the isolated
game's `ValheimVRM` directory. Set `VRM_BRIGHTNESS_CALIBRATION_ONLY=1` to run only
this probe. A full run additionally uses `KUMALY_2.vrm` and the pre-existing import
limit tests; optional legacy fixture paths can be supplied through
`VRM_BRIGHTNESS_LEGACY_MODELS`. Fixtures are never included in releases.

Success includes `AVATAR_BRIGHTNESS_CALIBRATION_TESTS_PASSED` in `results.txt` and
retains PNG captures. An `error.txt` or an unexpected exception/error in the game
log means failure. Synchronization, production server relay, old/new binary
compatibility and same-model player binding are covered separately by
`tests/AvatarSyncEngineTests` and `tests/AvatarSyncTests`.
