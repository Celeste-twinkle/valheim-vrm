# 1.8.1 mixed client server validation

The Windows/D3D11 isolated game probe uses the production server addon and the
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
