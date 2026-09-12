# 1.8.7 validation — Main-menu reentry, GPU memory and minimum height

Environment: Windows x64 Valheim 1.0.12, Unity 6000.0.75f1, BepInEx 5.4.23.3,
D3D11. Validation uses the ignored isolated game under `artifacts/camera-qa/game`.

## Observed failure and reproduction

The user's 2026-09-13 Player-prev.log contains 1,504 failures to create a
3840x2160 render texture with error 0x8007000e. Both recent logs include an
`AttachToPlayer` null reference after leaving a world. Existing native dump
folders were older; the recent evidence is the game runtime logs.

`FejdStartup.Awake` called the assembly patch installer again on every return to
the menu. Harmony retained the duplicate registrations. Three extra production
startup callbacks on 1.8.6 produced four bloom prefixes. Instrumenting the real
bloom method found four temporary acquisitions but only one distinct texture
released per call: the repeated prefixes overwrote their shared Harmony state.
A bounded 256x256 diagnostic released the leaked allocations itself, avoiding a
machine-wide out-of-memory reproduction.

## Fixed-build checks

- Exactly one bloom prefix after repeated startup callbacks; no duplicate method
  registrations owned by this plugin across all patched game methods.
- Three actual main-menu scene reloads also retained exactly one bloom prefix.
- 360 actual 3840x2160 HDR bloom calls: every temporary acquisition balanced by
  one release. After warmup, texture count stayed at 16 and observed render-texture
  native memory stayed at 264,490,560 bytes in the first controlled run.
- Destroy player/visual objects at 10 asynchronous attachment boundaries. No
  exception; pending attachment ownership returned to zero in every case.
- Model and user configuration files are not rewritten by the fix.

Evidence: `artifacts/progressive-lag-qa/repro-2/results.txt` and
`artifacts/progressive-lag-qa/height-final/results.txt` (local ignored artifacts).

## Minimum height and pose regression

Known 1.2/1.6/1.8 m mesh fixtures verify actual rendered height, explicit larger
scales, disabled geometry, pre-existing root scale, repeated application and
cached calibration on clones whose pose/mesh has changed. Invalid custom scale
does not produce a non-finite transform.

Three production imports (same DLL as the lifecycle test):

| Model | Imported standing mesh | Effective scale | Final height |
| --- | --- | --- | --- |
| KUMALY_2 | 1.234 m | 1.296 | 1.600 m |
| Shinano_LightAdjustment | 1.395 m | 1.147 | 1.600 m |
| Shinano_Sleep | 1.321 m | 1.212 | 1.600 m |

These are visible mesh heights including hair/headwear, rounded for display.
The source VRMs are unchanged. The pose suite on these three models also passed:
99 sitting samples each across three scales and translated/rotated roots,
both hand grips over three scales and four poses, switch/unbind restoration,
spring-clone references and physics weights over 240 moving frames. The lowest
sampled sole point was -0.014687 m (within the existing 2 cm pose-test tolerance).
Evidence: `artifacts/progressive-lag-qa/pose-final/results.txt`.

The resource-lifetime suite passed after minimum-height scaling was added:
shared templates survive other players switching; idle templates and owned
native assets are released by the production 15-second timer; imports, failed
imports and per-instance expression materials release their resources. Evidence:
`artifacts/progressive-lag-qa/residency-height-final/results.txt`.
The catalog/configuration and per-player request-ordering suites also passed.

## Scope

ValheimVRM's server sync remains optional and does not reject unmodded players.

Controlled Windows engine tests do not establish compatibility with every mod,
GPU driver, Linux server or Steam/PlayFab deployment.
