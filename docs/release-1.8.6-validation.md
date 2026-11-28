# 1.8.6 validation — VRM-only model directory

Validated on 2026-09-13 with Windows x64 Valheim 1.0.12, Unity 6000.0.75f1,
BepInEx 5.4.23.3 and D3D11, using the ignored isolated game under the repository.
No game copy or temporary avatar import was placed in the Unity project.

## Runtime verification

- Client, server and engine test projects build with zero warnings/errors.
- Catalog tests: 25 arbitrary model names, Unicode/uppercase extension handling,
  separate configuration persistence, path boundaries, no legacy configuration
  fallback, and defaults when configuration is absent.
- Start with only three VRM files (two Shinano models and KUMALY) in `ValheimVRM`
  and no `BepInEx/config/ValheimVRM` directory. The actual local F8 request path
  imports and switches both Shinano models, applies the supported defaults,
  retains humanoid rigs and attaches physics weight support.
- Selection, physics and rendering changes create their JSON files only under
  BepInEx/config. All source VRM hashes and model-directory entries are unchanged.
- A second game process restores the selected model, 25% physics weight and
  rendering preferences from those JSON files. No TXT file is generated or needed.
- The full production client/server ZRpc regression passes in both processes:
  independent player instances, reversed request delivery, duplicate rejection,
  respawn/reconnect, optional unmodded participation and missing/differing models.
  Remote switches also leave the model directory free of auxiliary files.

Engine evidence (ignored local artifacts):
`artifacts/vrm-only-qa/engine-1/results.txt` and
`artifacts/vrm-only-qa/engine-restart/results.txt`.

## Distribution behavior

Client documentation, licenses and examples are placed under
`BepInEx/plugins/ValheimVRM`. The private model ZIPs retain identical VRM bytes
and no longer include required model-settings sidecars. The optional distribution
validator checks plugin dependencies, without a required avatar set, model TXT,
JSON or default model. It is not a game startup prerequisite.

Old model-folder configuration and `selected_models.json` are intentionally no
longer read or automatically migrated. Optional TXT settings belong in
`BepInEx/config/ValheimVRM`; missing settings use built-in defaults. Users can move
wanted settings before upgrading. The local installation update preserves this
user's existing choices and configuration at the new location.

## Limits

These are controlled tests in the actual Windows game engine, not a complete
world playthrough or a real Steam/PlayFab dedicated-server login. Linux, macOS,
Vulkan and arbitrary third-party mod combinations were not tested. The server
sync wire protocol is unchanged. This release does not reconvert avatar files.
