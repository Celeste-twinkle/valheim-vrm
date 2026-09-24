# ValheimVRM 2.0.1 validation

Validation date: 2026-09-24 (Asia/Shanghai).

Environment: Windows x64, Valheim 1.0.12, Unity 6000.0.75f1, BepInEx
5.4.23.3 and D3D11. The engine probe ran in an isolated game copy under the
repository's ignored `artifacts` directory.

## Native wearable regression

- Inspected the current `assembly_valheim.dll` and confirmed that `VisEquipment`
  has a separate `m_trinketItemInstances` list in addition to the older utility,
  armor, helmet, hair, hand and back slots.
- Ran the production visibility policy inside Valheim against the real
  `VisEquipment` type. A trinket whose renderer child began inactive was hidden,
  proving that later-activated variants are covered.
- Verified held and sheathed/back item renderers remained enabled. Existing
  optional native chest visibility remained independent, and repeated equipment
  application did not restore the trinket.
- The same policy runs immediately during VRM attachment and after every native
  equipment LOD refresh. Unknown future item-instance fields remain visible only
  when they are hand or back attachment slots.

The focused engine run completed without `error.txt`; its `results.txt` records
the native equipment and settings pass markers.

## Catalog and build regression

- The catalog test created 25 mixed-case/Unicode VRMs, deleted a selected file,
  refreshed the same catalog instance and confirmed the deleted name and path
  were removed. A second unchanged refresh reported no change, and the missing
  selection fell back safely.
- The F8 picker filters its current snapshot against `File.Exists` every draw and
  rescans the top-level model directory once per second while open. Subfolders
  and non-VRM files remain excluded.
- Client and server Release builds completed with zero warnings/errors. Existing
  protocol tests remain unchanged because this patch adds no packet fields or
  admission checks.
