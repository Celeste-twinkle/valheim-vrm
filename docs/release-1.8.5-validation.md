# 1.8.5 import brightness limit validation

Windows x64 / D3D11, Valheim 1.0.12 (Steam build 25253764), Unity 6000.0.75f1,
BepInEx 5.4.23.3. Tests run in the ignored isolated game installation.

`tests/AvatarBrightnessEngineTests` uses Unity's actual material API and the
production VRM importer. Scalar/color cases cover zero, low, just-below, exact,
just-above and HDR values through 20. Values at or below each limit remain exact;
larger colors are scaled proportionally in linear RGB space. Alpha is retained,
and 1,000 repeated applications do not accumulate darkening. Base and shade
limits operate independently; emission, render queue and unsupported shaders
retain their previous values.

Production imports of calibrated Shinano and KUMALY models preserve all 70 tested
base/shade color properties exactly. The older high-brightness KUMALY import
reduces 40 properties to the corresponding linear limits (0.45 / 0.2025).
Each model is checked after happy-expression activation/reset and repeated
scene-lighting off/on cycles. Original input byte arrays and source-file hashes
remain unchanged. Private avatar fixtures are not included in public releases.

Unchanged-material assertions compare Unity's stored colors before and after
limiting, accounting for native Color round-trip precision. Imported materials
are compared with the original UniVRM descriptor under the same API.

The existing sync engine and pure protocol tests cover independent player
identities, request ordering, unmodded clients and missing/different avatar files.
The server protocol is unchanged; the server package is rebuilt with a matching
version number. Test plugins are removed from the isolated game after validation
and are not distributed.

This is a limit on imported VRM 1.0 MToon base/shade factors. Emission, animated
color overrides, lighting and post-processing can still produce bright pixels.
VRM0/Standard/other shaders retain their existing behavior. These controlled
Windows tests do not establish full-world WAN, Steam/PlayFab dedicated-server,
Linux, macOS or Vulkan behavior.
