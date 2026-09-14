# Rendering controls

`AvatarRendering/MToon10` and `AvatarRendering/MToon` derive from UniVRM 0.131.2 under the MIT notices in
`../Libs/Managed/MToon-LICENSE.txt` and `../Libs/Managed/UniVRM-LICENSE.txt`.
It is a Built-in Render Pipeline variant for Windows x64. The original installed
MToon shader for each format is retained whenever lighting and shadow reception are enabled.
The legacy source is [UniVRM 0.131.2 MToon](https://github.com/vrm-c/UniVRM/tree/v0.131.2/Packages/VRM/MToon/Shaders).

The variant bypasses `UNITY_SHADOW_ATTENUATION`, preserving distance/cookie
attenuation in `UNITY_LIGHT_ATTENUATION`. The flat-color branch returns base color
and emission in the base pass and zero RGB in additional-light passes. It does not
clamp output brightness. `AlbedoLit/AvatarBloom.shader` masks only the bloom input.
When game TAA is active, the client uses matching projection jitter for opaque
and transparent draws on cameras that see avatars. TAA resolves scene color before
bloom, so the bloom filter also applies its current UV offset to coverage and uses
bilinear sampling. The offset resets at each camera cull, including non-TAA renders.

`AvatarRendering/AvatarDepth.shader` supplies depth and world-space normals for
opaque/cutout MToon surfaces at `CameraEvent.AfterGBuffer`. Without this step,
Amplify Occlusion's post-effect can shade a forward-only avatar using the geometry
behind it. The pass also clears background albedo/specular values in those pixels,
leaves the lighting target untouched and preserves cutout UV animation. Visible
color still comes from the original MToon forward passes. Blended materials and
non-MToon shaders keep their native depth behavior.

Both depth and bloom coverage use `AvatarRendering/AvatarUv.cginc` to follow
the material's UV animation. VRM 0.x MToon uses the mask's red channel and its
legacy property names; MToon10 uses blue. Alpha mode, culling and depth-write
properties are also mapped per shader generation rather than guessed from a
model filename. The native material and its texture/alpha/outline bindings are retained.

To rebuild the embedded bundles:

1. Open a separate Unity 2022.3.22f1 Built-in Render Pipeline project with Windows x64
   build support. Do not use a project containing private avatars for release builds.
2. Copy the contents of this directory into the project's `Assets` directory.
3. Execute `BuildAvatarRendering.Build` from an editor script, or use Unity's
   `-batchmode -nographics -projectPath <project> -executeMethod BuildAvatarRendering.Build -quit`.
4. Copy `../Build/avatar_rendering` and `../Build/avatar_bloom` into this repository's
   `Assets` directory and rebuild the plugin. Check the Unity log for
   `AVATAR_RENDERING_BUNDLE_OK` and shader compilation errors.

Both bundles are committed for players/builders who do not have the Unity editor.
Neither contains models, textures from avatars, scenes, or user data.
