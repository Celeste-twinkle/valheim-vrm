# 1.8.4 avatar resource lifetime validation

Windows x64 / D3D11, Valheim 1.0.12 (Steam build 25253764), Unity 6000.0.75f1,
BepInEx 5.4.23.3. Tests use the ignored isolated game copy, not a Unity project
or the normal game installation.

`tests/AvatarResidencyEngineTests` exercises production import, registration,
attachment and collection with two player fixtures and two VRM 1.0 models:

- Two players share a template; changing one player's model retains every
  resource needed by the other player, including while outside camera view.
- A same-name replacement retains the old generation until its last instance
  is destroyed, then removes its resources without deleting the newer hash/cache.
- Removal of the last player triggers the actual 15-second Update timer. Unity
  reports every tracked template mesh, texture, material and rig asset destroyed.
- Reimport after eviction works, and a pending attachment prevents eviction.
- Color-sync setup does not copy MToon10 materials again. UniVRM's expression
  material instances are released when their avatar is destroyed.
- Every successful import disposes its GLB/native buffers; invalid VRM metadata
  inside a valid GLB also releases those buffers on failure.
- VRM0 texture decoders have independent ownership even for identical image
  bytes, avoiding destruction of another importer's texture.
- Normal local/server-sync mode drops source-file byte arrays. Explicit legacy
  file sharing retains the source bytes required by its existing transfer code.

The existing sync engine regression suite and pure protocol tests cover player
identity, sequence ordering, mixed clients, missing/different folders and optional
settings. The server protocol is unchanged from 1.8.3.

Resource destruction is checked using actual Unity object references, not an
assumed decrease in process working set. Unity and graphics drivers can retain
freed memory in pools. These controlled tests do not establish long-session WAN,
Steam/PlayFab dedicated-server or Linux behavior. Test plugins are not distributed.
