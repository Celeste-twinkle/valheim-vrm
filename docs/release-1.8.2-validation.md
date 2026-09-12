# 1.8.2 different-folder validation

Windows x64 / D3D11, Valheim 1.0.12 (Steam build 25253764), Unity 6000.0.75f1,
BepInEx 5.4.23.3. The normal game and ignored isolated copy use the same
`assembly_valheim.dll` SHA-256:
`27a766a8d23a7bd8b6a54fb9ad0452a96c305fb3629b39c40527c09a1c393a84`.

The engine probe runs the production client switcher and remote-player polling
against three distinct character ZDOs. It covers missing and empty catalogs,
deleted and exclusively locked files, differing uncached bytes (deliberately
invalid VRM data), and a different already-cached model hash. Each failed choice
retains that player's existing visual, or untouched vanilla renderers on first
load. The picker returns to idle and the sync connection remains available.

A scoped importer observer verifies that these mismatches never invoke UniVRM;
the mismatched uncached file creates neither model nor settings cache entries.
Repeated polls do not retry a failed choice. Another player's valid selection
still applies. Replacing the unimported file with matching bytes and refreshing
loads independent avatars without reconnecting or writing local selections.
Replacing an already-cached same-name model still requires a client restart.

The existing production server/ZRpc tests also cover an unmodded client with
60 normal request/reply exchanges, independent identities, spoof rejection,
respawn, late join, opt-out and disconnect. The server has no avatar filesystem
access: an absent or different server model folder cannot affect admission or
the registry. Local rendering/model settings are not synchronized or compared.

These are controlled engine tests with in-memory sockets and character fixtures.
Real Steam/PlayFab dedicated-server login, WAN play, Linux and complete world
playthroughs have not been validated. Run `tests/AvatarSyncEngineTests` using the
isolated setup in [the 1.8.0 record](release-1.8.0-validation.md). Never distribute
the test DLL or install it in a player's normal game.
