# Avatar pose engine regression

`VRM_EQUIPMENT_AXES=1` selects the focused held/back-item regression. At 1.4,
2.0 and 2.2 m it exercises four hand item types over 90 rotating-character/wrist
samples, then nine actual back items over three headings/draw-sheath cycles and
six stepped animations. It checks character-relative XYZ, automatic/user scale,
non-accumulation and original-transform restoration. Run on VRM 0.x and 1.0
fixtures. Completion includes `AVATAR_EQUIPMENT_AXES_PASSED` in `results.txt`.

`VRM_NATIVE_EQUIPMENT=1` runs only the native wearable visibility regression.
It checks the current Valheim trinket slot, inactive renderer variants, optional
native armor and preservation of held/back equipment.

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

Set `VRM_EQUIPMENT_AXES=1` for focused character-axis regression at 1.4, 2 and
2.2 m. Four held item types rotate the character and wrist over 90 updates each;
nine back items cover three draw/sheath cycles, six animations and 30 frames.
Checks include absolute offsets, height scaling, repeated application and reset.
