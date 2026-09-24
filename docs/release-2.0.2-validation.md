# ValheimVRM 2.0.2 validation

## Scope

2.0.2 removes automatic model-directory polling introduced in 2.0.1. The catalog is built once during plugin startup and remains an in-memory snapshot until the player explicitly presses F8 → **Refresh list**.

## Scan policy

- `OutfitSwitcher.Awake()` calls `RefreshModels()` once after the catalog is created.
- The F8 **Refresh list** button is the only post-startup call site.
- Opening the F8 panel does not scan.
- `Update()`, `OnGUI()` and `DrawWindow()` contain no timed directory scan and no per-frame `File.Exists` filter.
- Selecting a cached entry still validates that entry before importing it. A removed file therefore fails safely and leaves the prior appearance intact until the player refreshes the catalog.

## Catalog regression

`AvatarCatalogTests` creates 25 top-level VRM fixtures, including Unicode and mixed-case extensions. It verifies that deletion does not mutate the cached names before an explicit refresh, then confirms that one manual `RefreshAndReportChanges()` call removes the stale entry. A second call over an unchanged directory reports no change. Subdirectories and non-VRM files remain excluded.

## Build and compatibility

- Client Release build: zero warnings and zero errors.
- Dedicated-server package build: zero warnings and zero errors.
- Catalog and synchronization regression executables pass.
- The server protocol and configuration schemas are unchanged from 2.0.1. The optional server continues to accept older, newer and unmodded clients.

Validated on Valheim 1.0.12, Windows x64, Unity 6000.0.75f1, BepInEx 5.4.23.3 and D3D11.
