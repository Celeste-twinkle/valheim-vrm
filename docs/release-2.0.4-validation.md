# ValheimVRM 2.0.4 validation

This client-only release fixes configuration replacement failures reported after successful avatar switches. It is based on the public 2.0.3 release, preserves its session-resident template behavior and retains the configuration-save implementation from the user-confirmed 2.0.3.1 test DLL. The configuration schemas, model format and synchronization protocol are unchanged; the existing 2.0.2 server addon remains compatible.

## Incident evidence

The supplied 2.0.3 log contains two `IOException` failures with the message corresponding to Windows `ERROR_UNABLE_TO_REMOVE_REPLACED` at `File.Replace → AvatarCatalog.Select → OutfitSwitcher.Switch`. Model import and visual application had already succeeded. First-time creation used `File.Move`, while subsequent saves used unbacked `File.Replace`, explaining the first-save-only report. The old log does not identify a locking process, filesystem or specific ACL problem.

The user reported that the 2.0.3.1 test DLL resolved the issue and supplied a new log. It contains eight successful saves: three first-time creations and five updates (rendering once, parts once, calibration three times). Every update failed backed `File.Replace` three times with HRESULT `0x80070497` / native error 1175, then succeeded with `SAVE_OK method=windows-move-fallback`. There are no final save failures, cleanup failures or avatar-switch failures. This confirms that native Windows fallback, rather than backed replacement or retries, recovered persistence on that computer.

The logged system is Windows 10 x64 on NTFS with approximately 144 GB free and normal Archive target attributes. Initial creation, backup copy and native replacement succeeded, so a blanket lack of directory read/write access does not explain the failure. The underlying reason for this environment's persistent `File.Replace` error remains unidentified. No avatar-selection or height-offset TXT save was performed in the new log; field confirmation directly covers rendering, parts and calibration, while the same writer is used by selection/TXT saves and covered by regressions. Raw user logs are not published.

## Validation performed for this release

- Compile the complete 2.0.4 client and the synchronization engine probe with zero warnings and zero errors.
- Run the production catalog/configuration writer tests on .NET 7: repeated creation/replacement, Unicode paths, isolated character selections, cached catalog/manual refresh, bounded transient retry and directory-stage diagnostics.
- Run the same source tests inside the actual game's Unity Mono runtime, including the real `MoveFileExW` call after injected Windows error 1175, unsupported replacement, partial replacement recovery, restoration of a missing target, actual readonly/held-open target preservation, successful saving after releasing the handle, cleanup errors preserving the original exception and logger independence.
- Regress the standalone synchronization protocol suite; no protocol or server code changes are included.
- Verify the shipped DLL version and all four embedded resources: `avatar_bloom`, `avatar_rendering`, `avatar_fur` and `player-animation-catalog.json`.
- Verify ZIP integrity, packaged DLL SHA-256, source archive contents and published asset hashes. Packages contain no game assemblies, avatars, user configurations or private encrypted-model implementation.

The new engine probe covers actual VRM/native switching with readonly and held-open selection files, accurate warnings, per-session choice retention and optional TXT creation/update preserving unrelated settings and comments. It compiles for this release but was not executed in a game scene: an existing Valheim process was running and was left undisturbed. The user confirmation applies to the supplied 2.0.3.1 test DLL, not a separate full-scene run of 2.0.4. Linux/macOS and other filesystem/mod combinations were not newly validated.

## Installation and behavior

Use `ValheimVRM-2.0.4.zip` for a complete client installation; install BepInEx 5 separately. Users with a complete 2.0.3 installation may use the DLL-only `ValheimVRM-2.0.4-update.zip` after exiting the game. Preserve model files and `BepInEx/config`; no migration or deletion is required. Existing servers need no update.

Configuration logs are written to `BepInEx/LogOutput.log` with the `[ValheimVRM Config]` prefix. Save failures remain visible and retain the applied model for the current session; only a successful save persists it across restart. Readonly/permission restrictions are not changed automatically. Ordinary model selection does not generate the optional model-settings TXT.

See the [Chinese diagnostic guide](CONFIG-SAVE-DIAGNOSTICS.zh-CN.md) and [incident retrospective](CONFIG-SAVE-POSTMORTEM.zh-CN.md).
