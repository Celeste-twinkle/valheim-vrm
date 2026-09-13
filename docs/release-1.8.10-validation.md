# 1.8.10 validation — remove height feedback

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1,
BepInEx 5.4.23.3, D3D11. Tests run in the repository's ignored
`artifacts/camera-qa/game`, outside the avatar Unity project.

## Reproduction and correction

In 1.8.9, VRMAnimationSync wrote target humanoid positions into native bones
in both Update and LateUpdate. The new contact solver then read the modified
native feet as its next support reference. Continuous Unity frames reproduced
about 0.079 m of upward drift per frame: the target hip rose from 1.1215 m
to 5.0658 m relative to its stationary character root by frame 50.
The fixture stopped at that safety bound. Evidence:
`artifacts/live-grounding-qa/old/results.txt` and `error.txt`.

The fix removes both write-back loops. HumanPoseHandler reads the native
animation and resets the VRM pose before the current frame's contact delta
is applied. Imported size and cached contact geometry are not recomputed
per frame. Dynamic contact evaluation adapts to animation without feeding
corrected positions back into the input skeleton. There are no model-name
height constants and the minimum visible standing height stays at 2 m.

Equipment sockets follow the finished avatar without moving native humanoid
bones. Head/back sockets preserve their authored offsets and native animation
orientation; palm-relative hand grips retain their existing geometry mapping.
Detachment restores socket transforms. A socket that is itself a humanoid
bone, or an ancestor of one, is ignored.

## Continuous frame tests

The new LivePoseProbe uses actual Unity Update, animation and LateUpdate.
It does not Rebind or manually evaluate the animator between frames. A
pre-sync observer records native bone transforms; a post-sync observer
asserts that they remain unchanged. Independent Unity BakeMesh evaluation
checks the rendered foot, seat and lower-body contact surfaces.

Four models cover VRM 0.x and 1.0: Shinano Light Adjustment, Shinano Sleep,
KUMALY 2 and AliciaSolid. Each runs 1,080 continuous frames across standing,
sit-down, ground sitting, stand-up and chair sitting, at three scales and
root heights 0 and 1200 m. The first fixed run passed all 4,320 frames,
with maximum sampled contact error 0.000122 m and no native skeleton writes.
Evidence: `artifacts/live-grounding-qa/fixed-live/results.txt`.

The same continuous suite also passed with the source already sitting when
the VRM was attached: another 4,320 frames across the same models/scales/states,
with maximum contact error 0.000122 m. This checks that a seated load does not
contaminate subsequent standing placement. Evidence:
`artifacts/live-grounding-qa/seated-attach/results.txt`.

The final build also passed the full sampled-pose suite: 1,320 poses with
maximum independent mesh contact error 0.004028 m, separate standing +17 cm
and sitting -8 cm offsets, both palm grips and seven body sockets at three
scales/four poses, native-bone socket rejection and restoration on unbind.
All four models passed physics-weight checks over 240 moving frames at
0/25/50/100%, and the three VRM 1.0 clones retained their spring references
with finite motion. Evidence:
`artifacts/live-grounding-qa/full-final/results.txt`.

The earlier 1.8.9 suite manually evaluated the animator before isolated pose
checks. That was useful for geometry accuracy but did not test the full
frame sequence and therefore missed the regression. Its validation record
has been corrected to make this limitation explicit.

## Scope

The continuous tests use the real player animation controller and real avatar
imports in an isolated game fixture. They do not establish compatibility with
every external animation mod, avatar or furniture shape. Contact geometry
reflects blend shapes at attachment; animated shape changes and arbitrary
slope/furniture IK remain outside this solver. Network protocol, model assets,
TAA/bloom shaders and physics weights are unchanged by this release.
