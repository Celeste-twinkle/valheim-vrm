# Avatar pose engine regression

Build against the local game and the Release client DLL:

```powershell
dotnet build tests/AvatarPoseTests/AvatarPoseTests.csproj -c Release -p:VALHEIM_INSTALL_PATH=F:/steam/steamapps/common/Valheim
```

Run only in a separate game copy under ignored repository artifacts, with an
isolated `-savedir`. Add the test DLL beside the production plugin and set:

- `VRM_POSE_TEST_OUTPUT`: test report directory.
- `VRM_POSE_TEST_MODELS`: absolute humanoid VRM paths separated by `|`.
- Optional `VRM_GROUNDING_DIAGNOSTIC=1`: run grounding/settings checks only.

The full suite checks actual mesh contact at three scales, translated/rotated
roots and five animation states (330 samples per model), independent standing
and sitting adjustments, settings persistence, hand equipment and spring
physics. Contact references use Unity mesh skinning with scale compensation;
the production code instead caches and evaluates weighted contact points.
Ground-sit support covers the legs and pelvis, not loose hair or hand contacts.

The test reports `AVATAR_POSE_TESTS_PASSED` in the game log and exits. An
`error.txt` means failure. Remove or disable the test DLL after the run.
