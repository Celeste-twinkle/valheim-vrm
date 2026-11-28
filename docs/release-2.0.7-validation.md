# ValheimVRM 2.0.7 validation

- Built the client, sync server and standalone Windows x64 manager from the public 2.0.6 branch. The EXE contains both release ZIPs and a checksum-verified BepInEx 5.4.23.3 Windows x64 package.
- Checked Steam discovery on a Windows 11 Steam client, modern and legacy Steam library formats, manual paths with spaces and Chinese characters, and separate client and dedicated-server paths.
- Exercised fresh installs and uninstalls for the client, the client-hosted sync addon and a Windows dedicated-server test directory. Checked duplicate plugin cleanup, shared runtime conflicts, rollback after a destination failure and preservation of separate model/config folders.
- Ran the standalone EXE and used its native file picker and model list. Exercised the WPF file-drop handler and Unicode filenames. Verified model deletion and same-name import handling.
- Booted an isolated copy of the installed Valheim Windows client with the bundled BepInEx. The loader initialized the client and sync server plugins. Dedicated-server verification covered the filesystem and manager controls.
- Existing `AvatarCatalogTests` and `AvatarSyncTests` passed.

[Manager guide](MANAGER.md) · [简体中文](MANAGER.zh-CN.md)
