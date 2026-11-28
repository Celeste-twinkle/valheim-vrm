# Mounted player synchronization regression

Build the client and this `net471` BepInEx test plugin against the same Valheim
installation. Run the plugin only in an isolated game copy with a separate
`-savedir`; it disables cloud saves, replaces network singletons temporarily,
and exits after the suite. Do not add it to a normal play installation.

```powershell
dotnet build tests/AvatarAttachmentEngineTests/AvatarAttachmentEngineTests.csproj -c Release -p:VALHEIM_INSTALL_PATH=F:/steam/steamapps/common/Valheim
```

Set `VRM_ATTACHMENT_TEST_OUTPUT` to a fresh report directory. Optional
`VRM_ATTACHMENT_TEST_MODELS` contains absolute humanoid VRM paths separated by
`|`; use both a VRM 0.x and a VRM 1.0 fixture. Add the test DLL beside the client
DLL in the isolated game's BepInEx plugins directory.

The suite uses real `Player.GetRelativePosition`, `ZSyncTransform.OwnerSync`,
ZDO serialization/deserialization, scene lookup and remote position methods.
Synthetic creature/saddle hierarchies isolate the network defect; the player's
native animator is cloned from the installed game's player prefab. Reverse
patches execute the unmodified game's methods to reproduce the missing-joint
bug and to verify compatibility with unpatched receivers.

Checks cover standing on a creature and then riding the same parent, 60
mount/dismount cycles, movement/rotation and altitude changes, complete detach,
unchanged-state revisions, fixed sender/native receiver, old sender/fixed
observer, received-data immutability, inactive/unloaded saddles, late mount load,
other riders and non-owner writes. Optional VRM probes check three heights,
270 moving/turning samples per model, and independently baked mesh support with
the expected sitting/model offsets, using the existing pose suite's sampler.

Success ends `results.txt` with `AVATAR_ATTACHMENT_ENGINE_TESTS_PASSED`. Failure
creates `error.txt`. Inspect the Unity log for unexpected exceptions as well as
the pass marker. These isolated engine tests do not replace a two-client live
multiplayer check.
