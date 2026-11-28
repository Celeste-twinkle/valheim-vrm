# 1.8.1 mixed client server validation

The current Windows x64 / D3D11 probes run on Valheim 1.0.12 (Steam build
25253764), Unity 6000.0.75f1 and BepInEx 5.4.23.3. The normal game and isolated
copy have the same `assembly_valheim.dll` SHA-256:
`27a766a8d23a7bd8b6a54fb9ad0452a96c305fb3629b39c40527c09a1c393a84`.
Earlier validation records describe their original game versions.

The isolated game probe uses the production server addon and the
game's actual ZRpc serializer. One client registers no ValheimVRM RPC handlers,
matching how an unmodded client dispatches unknown methods. Modded clients in
the same fixture still switch models independently.

The unmodded client receives at most three discovery messages and zero avatar
snapshots. Sixty ordinary request/reply exchanges complete with ZRpc Success,
and its connection remains ready and connected. It never acquires another
player's selection. Tests also retain authenticated character ownership,
listen-host initialization, respawn, late join, opt-out and disconnect coverage.

This is an engine protocol test with in-memory sockets and controlled character
ZDOs. It does not include a real Steam/PlayFab dedicated-server login, WAN play,
Linux or a complete multiplayer world playthrough. The addon adds no admission
hook, mandatory client version check or missing-mod kick.

Run `tests/AvatarSyncEngineTests` as described in
[the 1.8.0 validation record](release-1.8.0-validation.md). The test DLL belongs
only in the ignored isolated game installation and is never shipped in packages.
