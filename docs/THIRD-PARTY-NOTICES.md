# Third-party components

ValheimVRM derives from the work of yoship1639, aMidnightNova and nyaarium.
The repository's `LICENSE` applies to this fork's source changes.

This Windows package includes these existing runtime components:

- UniVRM / UniGLTF / MToon, MIT: [v0.131.2 source](https://github.com/vrm-c/UniVRM/tree/v0.131.2).
  See `UniVRM-LICENSE.txt`, `UniGLTF-LICENSE.txt`, and `MToon-LICENSE.txt`.
- UnityAsyncImageLoader, MIT: [upstream source](https://github.com/aMidnightNova/UnityAsyncImageLoader).
  See `UnityAsyncImageLoader-LICENSE.txt`.
- FreeImage, including its third-party codecs: [source and project](https://freeimage.sourceforge.io/).
  See the complete `FreeImage-license.md` distributed with the native library.
  The existing upstream native DLL is retained unchanged. Its exact binary matches
  the ydm/FreeImage 0.1.6 Windows x64 distribution; a source archive is attached
  to this Release as `FreeImage-ydm-0.1.6-source.zip`.

The `shaders/AvatarRendering/MToon10` sources derive from UniVRM 0.131.2's MToon10
shader. Changes add an optional flat-color path and a variant that skips received
shadow maps. The default rendering path uses the unchanged `UniVRM.shaders` bundle.
The compatibility depth pass matches MToon's UV animation and alpha coverage.
The bloom coverage/filter shader, compatibility depth pass and render-options
bundle are embedded in the plugin.

The game, BepInEx, Unity engine DLLs, Unity.Burst, Unity.Mathematics, Newtonsoft.Json,
user avatars and user settings are not bundled. BepInEx must be installed separately;
the remaining game-provided dependencies are referenced from the installed game.
