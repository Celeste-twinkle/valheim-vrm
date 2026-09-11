# ValheimVRM — Celeste-twinkle fork

[Download the compiled release](https://github.com/Celeste-twinkle/valheim-vrm/releases/latest) · [English installation](docs/INSTALL.md) · [中文安装说明](docs/INSTALL.zh-CN.md)

Windows x64 client build for Valheim 1.0.7. This fork combines the compatibility
fixes proposed in [upstream PR #53](https://github.com/nyaarium/valheim-vrm/pull/53)
with an in-game avatar picker and optional rendering controls. It is an independent
release based on nyaarium's fork, with thanks to nyaarium, aMidnightNova and yoship1639.

- Press **F8** to open a scrollable list of your own VRM files. There is no eight-model limit.
- Add files to `ValheimVRM` beside `valheim.exe`, refresh, then select a model.
- Remember selections per character, including after restart or respawn.
- Toggle scene lighting, received shadows and avatar bloom for VRM 1.0 MToon materials.
  Defaults: lighting on, received shadows on, avatar bloom off. No brightness ceiling or highlight compression.
- Keep equipped items and their stats when changing appearance.

No avatars are included. BepInEx is required separately. The package includes the
matched UniVRM dependencies; install the complete ZIP, not only `ValheimVRM.dll`.
See the installation guides for supported materials, per-model settings and the
limits of local-only outfit selection in multiplayer.

## Development

The local rendering/picker build is on `codex/public-release`. The narrower
`codex/valheim-1.0-compatibility` branch remains the source of the upstream PR.

With a .NET SDK and an installed copy of Valheim plus BepInEx:

```powershell
$env:VALHEIM_INSTALL_PATH = 'C:\Games\Valheim'
powershell -NoProfile -File tools/Test-RuntimeDependencies.ps1 -ValheimPath $env:VALHEIM_INSTALL_PATH
dotnet build -c Release
dotnet run --project tests/AvatarCatalogTests
```

Build output is `release/ValheimVRM-1.7.0.zip`. Building does not install the plugin
into your game unless you explicitly pass `-p:InstallToGame=true`.
The catalog tests use .NET 7 and temporary files; the plugin targets .NET Framework 4.7.1.
Shader source/rebuild instructions are in [shaders/README.md](shaders/README.md).
Use a full build to include the embedded rendering resources; `-t:Compile` alone
is insufficient for a distributable DLL.

Read [runtime dependency provenance](Libs/README.md),
[compatibility validation](docs/valheim-1.0-validation.md), and
[release validation](docs/release-1.7.0-validation.md) for the tested scope.
Report fork-build problems to [this fork's Issues](https://github.com/Celeste-twinkle/valheim-vrm/issues).