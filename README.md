# fa

FrameAngel repo-local product authority.

This repo now carries both:

1. the historical base-player recovery trail
2. the active modular product-system canon for the next lane

Use the router docs below instead of treating this README as the architecture
spec.

## Active canon router

Modular player family strategy:

1. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_BLUEPRINT_V1.md`
2. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_EXECUTION_PLAN_V1.md`
3. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`
4. `products/vam/config/player_product_matrix.v1.json`
5. `products/vam/config/player_version_capability_schedule.v1.json`

Shared package metadata/process:

1. `products/vam/docs/handoffs/VAM_VAR_PACKAGE_PROCESS_AND_METADATA_V1.md`

Historical base-player boundary:

1. `products/vam/assets/player/docs/handoffs/PLAYER_FIRST_RELEASE_BOUNDARY_V1.md`
2. `products/vam/assets/player/docs/handoffs/PLAYER_CORE_CANON_0_2_1_V1.md`

Current DevPlayer witness/process lane:

1. `products/vam/assets/player/docs/handoffs/PLAYER_DEV_SCENE_AND_DEPLOY_PROCESS_V1.md`

Volodeck parity witness lane:

1. `products/vam/assets/player/docs/handoffs/PLAYER_VOLODECK_PARITY_BOUNDARY_V1.md`
2. `products/vam/assets/player/docs/handoffs/PLAYER_META_UI_PACKET_1_5_RUNNING_LOG_V1.md`

Repo hygiene lane:

1. `products/vam/docs/handoffs/REPO_AGENT_AND_PROCESS_HYGIENE_V1.md`

Joystick scroller brand/support lane:

1. `products/vam/plugins/ui_scroller/docs/UI_SCROLLER_DEPLOY_AND_PACKAGE_SEAM_V1.md`
2. `products/vam/plugins/ui_scroller/docs/JOYSTICK_SCROLLER_PUBLIC_UPDATE_LADDER_V1.md`

## Current repo rule

The active long-term grammar is:

1. surface
2. edition
3. package profile
4. version/capability schedule

Meta UI toolkit is the next prerequisite for the new modular lane once the
current architecture is solid. That does not retroactively widen the historical
first-release boundary for the base player.

## Core entrypoints

Player release wrapper:

1. `products/vam/assets/player/scripts/Build-PlayerScreenCoreFoundation.ps1`

Player package builder:

1. `products/vam/assets/player/scripts/Build-CuaPlayerVarPackage.ps1`

Scene builder:

1. `products/vam/assets/player/scripts/Build-PlayerDemoScene.ps1`

Volodeck/shell export:

1. `products/vam/assets/player/scripts/Export-GhostPlayerHostCuaFamily.ps1`
2. `products/vam/assets/player/scripts/Export-GhostPlayerHostCatalog.ps1`

Shared package metadata resolver:

1. `shared/scripts/vam-packaging/Resolve-FrameAngelVarPackageMetadata.ps1`
