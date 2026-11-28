# 1.8.11 validation — stable skeleton-based locomotion height

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1,
BepInEx 5.4.23.3, D3D11. The isolated game stays under ignored
`artifacts/camera-qa/game` in the mod repository.

## Cause and implementation

1.8.10 removed native-bone feedback but still matched the lowest animated
sole every frame. Alternating foot contacts and bent/scaled legs then added
a second vertical motion signal on top of the native hip animation. In the
2 m Shinano Light fixture, the extra offset varied by 0.155735 m across
idle, walking, jogging, running and stopping. Evidence:
`artifacts/locomotion-qa/old-gait/results.txt`.

The new reference reads the avatar's humanoid skeleton description, hip and
foot bones, mesh bind matrices, skin weights and sole geometry once at
attachment. Mesh data can use different authored axes from the runtime rig;
bind-weighted reference skinning puts those vertices into the same frame
before measuring hip-to-sole clearance. The cached native/avatar clearances
produce a fixed lift at the current scale. The native rig is never modified.
No live contact sampling occurs during ordinary standing, walking or running.
The game animation's hip motion passes through unchanged.

Ground/chair sitting and their transitions retain separate contact handling.
The minimum visible height remains 2 m and F8 offsets stay independent. A
single skeletal reference intentionally prioritizes stable animation over
forcing every foot vertex onto a surface. Small foot intersections/gaps in
some poses are possible; this is not per-foot terrain IK or universal
furniture fitting.

## Validation method

LocomotionProbe runs the real player controller and requires actual Walk New
and Run New clips, also covering Jog New, start/stop and speed changes.
LateUpdate observers measure target hip minus native hip every frame and
verify that the native bone positions remain unchanged. Three scales and
world heights 0/1200 m check stable correction through the movement cycle.
A Harmony probe counts production contact samples, independently enforcing
that the locomotion path does not sample contacts each frame. Separate
measurements check identical calibration from standing and seated load poses.

The ordinary pose suite independently skins sole/seat geometry and checks
standing/sitting manual offsets, both hand grips, seven body mounts,
restoration and spring physics. Standing uses a small tolerance for the
fixed-reference behavior; seated support keeps the stricter contact test.

## Recorded results

The final build passed 4,320 continuous locomotion frames (1,080 per avatar)
with Shinano Light Adjustment, KUMALY 2, Shinano Sleep and the VRM 0.x
AliciaSolid fixture. Standing/ground-sitting/chair-sitting initialization
produced identical skeletal calibration for all four. Production dynamic
contact samples during standing/walking/jogging/running/stopping: **zero**.
The avatar-minus-native hip offset remained constant within floating-point
precision at each scale, while native hip motion continued.

Reference standing sole heights relative to the character plane were:
Shinano Light -8.519 mm, KUMALY +6.220 mm, Shinano Sleep -3.487 mm and
AliciaSolid -8.914 mm. These residuals are reported rather than removed by
per-frame whole-avatar correction. Evidence:
`artifacts/locomotion-qa/gait-final/results.txt`.
An earlier longer run of the same skeleton-calibration implementation also
passed 8,640 continuous gait frames in `skeleton-reference/results.txt`.

The full pose/equipment/physics suite also passed all 1,320 sampled poses,
independent +17/-8 cm posture offsets, two hand grips, seven body sockets,
restoration and spring-reference/weight checks. Across the deliberately wider
0.7/1.0/1.4 scale sweep, the largest sampled standing sole residual was
47.974 mm; seated contact checks retained an 8 mm limit. No dynamic standing
correction was reintroduced to hide those residuals. Evidence:
`artifacts/locomotion-qa/full-final/results.txt`.

The rendering shaders, synchronization protocol and physics implementation
are unchanged. External animation mods, arbitrary avatars and furniture or
slope IK remain outside the tested scope.
