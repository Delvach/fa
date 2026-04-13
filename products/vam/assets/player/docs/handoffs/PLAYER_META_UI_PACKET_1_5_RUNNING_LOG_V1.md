# Player Meta UI Packet 1.5 Running Log V1

## Purpose

This is the live handoff log for Packet `1.5`.

Use it when a thread freezes, restarts, or needs to hand off the Meta UI
toolkit foundation work without relying on thread memory.

## Current authority

Packet `1.5` is governed by:

1. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_BLUEPRINT_V1.md`
2. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_EXECUTION_PLAN_V1.md`
3. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`
4. `products/vam/config/player_version_capability_schedule.v1.json`
5. `products/vam/assets/player/docs/handoffs/PLAYER_VOLODECK_PARITY_BOUNDARY_V1.md`

## Working rule

Meta UI toolkit is a modular building block for the new lane.

It is not:

1. a rewrite of the historical first-release boundary
2. a separate architecture root
3. a Unity-only proof lane with special controls

It must stay parity-bound to Volodeck and the same player/control contracts that
VaM will use.

## Live seams

Toolkit export:

1. `products/vam/assets/player/scripts/meta-toolkit/Build-MetaToolkitThemeCatalog.ps1`
2. `products/vam/assets/player/scripts/meta-toolkit/Sync-MetaToolkitThemeCatalog.ps1`

Host/shell parity assembly:

1. `products/vam/assets/player/scripts/Export-GhostPlayerHostCatalog.ps1`
2. `products/vam/assets/player/scripts/Build-CuaPlayerHostCatalog.ps1`
3. `products/vam/assets/player/scripts/Build-CuaPlayerHostPackage.ps1`

Packet `1.5` deterministic wrapper:

1. `products/vam/assets/player/scripts/Build-PlayerMetaUiPacket15Foundation.ps1`

Defaults:

1. `products/vam/assets/player/config/meta_ui_packet_1_5.defaults.json`

## What was completed in this slice

1. Locked the modular product-system canon so Meta UI is explicitly Packet `1.5`.
2. Added a machine-readable family schedule at `products/vam/config/player_version_capability_schedule.v1.json`.
3. Added Packet `1.5` defaults in `products/vam/assets/player/config/meta_ui_packet_1_5.defaults.json`.
4. Added `Build-PlayerMetaUiPacket15Foundation.ps1` to turn the scattered toolkit export and host catalog seams into one deterministic repo-local entry point.
5. Kept the work bounded to repo-local Meta UI, Volodeck, and host catalog seams.
6. Ran the first narrow proof successfully with `modern_tv`, `theme_00`, no preview, and no deploy.

## What the new wrapper does

`Build-PlayerMetaUiPacket15Foundation.ps1` now:

1. reads repo-local Packet `1.5` defaults
2. exports the Meta UI toolkit catalog for a selected theme
3. feeds that toolkit summary into the Volodeck host catalog export path
4. writes a receipt under `products/vam/assets/player/build/meta_ui_packet_1_5_runs`

This gives the lane one deterministic foundation entry point instead of making
future threads rediscover the toolkit export and shell catalog sequence.

## Expected outputs

Toolkit catalog output:

1. `products/vam/assets/player/build/meta_toolkit_catalog/theme_<nn>/`

Host catalog output:

1. `products/vam/assets/player/build/host_catalog/theme_<nn>/`

Packet `1.5` run receipt:

1. `products/vam/assets/player/build/meta_ui_packet_1_5_runs/meta_ui_packet_1_5_foundation_<timestamp>.json`

First successful narrow proof in this slice:

1. `products/vam/assets/player/build/meta_ui_packet_1_5_runs/meta_ui_packet_1_5_foundation_20260413T171940Z.json`
2. toolkit summary:
   `products/vam/assets/player/build/meta_toolkit_catalog/theme_00/ghost_meta_ui_toolkit_export_summary_theme_00.json`
3. host catalog summary:
   `products/vam/assets/player/build/host_catalog/theme_00/ghost_player_host_catalog_summary.json`

## Current defaults

Theme:

1. `theme_00`

Default control surface:

1. `meta_patterns_contentuiexample_videoplayer_e7cfc411`

Default shell set:

1. `modern_tv`
2. `mcbrooke_laptop`
3. `ivone_phone`
4. `ivad_tablet`
5. `retro_tv`

## Resume instructions if the thread freezes

1. Open this file first.
2. Confirm branch and working tree state.
3. Confirm `AGENTS.md` is still the only unrelated dirty file.
4. Syntax-check `Build-PlayerMetaUiPacket15Foundation.ps1`.
5. Inspect the successful narrow proof receipt above before changing inputs.
6. Run the wrapper with a narrow shell set first, preferably `modern_tv`.
7. Inspect the emitted receipt under `build/meta_ui_packet_1_5_runs`.
8. Only after the narrow proof is trustworthy, widen the shell set.

## Recommended first proof command

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerMetaUiPacket15Foundation.ps1 -RepoRoot C:\projects\fa -ShellKeys modern_tv -NoPreview
```

## Next implementation targets after the first proof

1. verify the wrapper receipt and host catalog outputs
2. note that the current shell export wrapper still emits the whole shell family before host catalog filtering; decide whether to keep that as-is or narrow it later
3. remove or further quarantine remaining legacy bridge repair strings if they still matter
4. decide whether the next Meta UI proof surface after video player should be search bar or grid menu
5. update the version/capability schedule row for `v1.5` once the actual toolkit feature set is chosen
