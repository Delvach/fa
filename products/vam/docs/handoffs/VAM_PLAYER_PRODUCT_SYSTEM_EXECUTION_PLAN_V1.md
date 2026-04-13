# VaM Player Product System Execution Plan V1

## Purpose

This is the concrete execution packet for the new player product system.

It exists so implementation can follow a prepared buildout path instead of
re-thinking structure during development.

The long-range scaling and scheduling model lives in
`products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`.

## Hard Rule

Do not add new feature development to the current prototype lane while these
packets are being built.

Use the prototype only as:

1. a witness
2. a regression reference
3. a source of reusable core seams

## Packet Order

### Packet 1: Freeze And Clean Inputs

Goal:

1. freeze the current player as prototype canon
2. clean package metadata and naming defaults
3. stop ambiguous package identity drift

Files to verify or normalize:

1. `products/vam/assets/player/player.version.json`
2. `products/vam/assets/player/config/var.package.metadata.json`
3. `products/vam/plugins/ui_scroller/config/var.package.metadata.json`
4. `products/vam/config/player_product_matrix.v1.json`

Exit condition:

1. naming and metadata are trustworthy inputs for the new lane

### Packet 1.5: Meta UI Toolkit Foundation

Goal:

1. restore Meta UI toolkit as a trustworthy modular building block
2. keep it parity-bound to Volodeck and the same exposed player/control contracts
3. prevent Meta UI from becoming a special architecture root

Authority files:

1. `products/vam/assets/player/docs/handoffs/PLAYER_VOLODECK_PARITY_BOUNDARY_V1.md`
2. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`

Exit condition:

1. Meta UI toolkit is ready to plug into the new family as a composable module
2. the toolkit lane does not contradict the surface x edition x package profile grammar

### Packet 2: Product Matrix Canon

Goal:

1. lock the fifteen-SKU matrix as a repo artifact
2. separate surface keys from edition keys
3. separate public package identity from runtime build identity

Primary files:

1. `products/vam/config/player_product_matrix.v1.json`
2. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_BLUEPRINT_V1.md`

Derived files to add next:

1. `products/vam/assets/player/config/edition_profiles/free.json`
2. `products/vam/assets/player/config/edition_profiles/pro.json`
3. `products/vam/assets/player/config/edition_profiles/pro_demo.json`
4. `products/vam/assets/player/config/surface_profiles/player.json`
5. `products/vam/assets/player/config/surface_profiles/modern_tv_player.json`
6. `products/vam/assets/player/config/surface_profiles/laptop_player.json`
7. `products/vam/assets/player/config/surface_profiles/phone_player.json`
8. `products/vam/assets/player/config/surface_profiles/tablet_player.json`

Exit condition:

1. every public SKU can be described without touching code

### Packet 3: Shell Export Authority

Goal:

1. make shell export the authoritative surface-production seam
2. validate that each planned surface has a real export/profile path

Authority scripts:

1. `products/vam/assets/player/scripts/Export-GhostPlayerHostCuaFamily.ps1`
2. `products/vam/assets/player/scripts/Export-GhostPlayerHostCatalog.ps1`
3. `products/vam/assets/player/scripts/Build-CuaPlayerHostCatalog.ps1`
4. `products/vam/assets/player/scripts/Build-CuaPlayerHostPackage.ps1`

Authority docs:

1. `products/vam/assets/player/docs/handoffs/PLAYER_SHELL_RECOVERY_BOUNDARY_V1.md`
2. `products/vam/assets/player/docs/handoffs/PLAYER_TIER_BOUNDARY_AND_WITNESS_V1.md`
3. `products/vam/assets/player/docs/handoffs/PLAYER_VOLODECK_PARITY_BOUNDARY_V1.md`

Files to add:

1. `products/vam/assets/player/config/host_profiles/modern_tv_player.json`
2. `products/vam/assets/player/config/host_profiles/laptop_player.json`
3. `products/vam/assets/player/config/host_profiles/phone_player.json`
4. `products/vam/assets/player/config/host_profiles/tablet_player.json`

Exit condition:

1. every non-basic planned surface has a checked-in host profile and export path
2. the Volodeck witness path is repo-local and does not depend on legacy repos

### Packet 4: Code Build Profiles

Goal:

1. stand up `free_core` and `pro_core`
2. keep `pro_demo` as packaging/profile inheritance, not a third runtime fork

Current seams to reuse:

1. `products/vam/plugins/player/vs/fa_cua_player/fa_cua_player.csproj`
2. `products/vam/plugins/player/src/cua-runtime`
3. `products/vam/plugins/player/src/scene-runtime`
4. `products/vam/plugins/player/src/shared-runtime`
5. `products/vam/plugins/player/src/innerpiece-core`

Files to add:

1. `products/vam/plugins/player/vs/fa_player_free/fa_player_free.csproj`
2. `products/vam/plugins/player/vs/fa_player_pro/fa_player_pro.csproj`
3. `products/vam/plugins/player/src/shared-runtime/BuildEditionInfo.cs`
4. `products/vam/plugins/player/src/shared-runtime/EditionFeatureCatalog.cs`

Rule:

1. if a feature is not in the free edition, compile it out or stub it cleanly
2. do not leave premium logic dormant in the free runtime

Exit condition:

1. two real runtime build identities exist and compile cleanly

### Packet 5: Packaging Profiles

Goal:

1. package any product by selecting matrix inputs, not by remembering script args

Current seams to reuse:

1. `products/vam/assets/player/scripts/Build-PlayerScreenCoreFoundation.ps1`
2. `products/vam/assets/player/scripts/Build-CuaPlayerVarPackage.ps1`
3. `shared/scripts/vam-packaging/Resolve-FrameAngelVarPackageMetadata.ps1`

Files to add:

1. `products/vam/assets/player/config/package_profiles/player_free.json`
2. `products/vam/assets/player/config/package_profiles/player_pro.json`
3. `products/vam/assets/player/config/package_profiles/player_pro_demo.json`
4. equivalent package profiles for all remaining surface and edition pairs
5. `products/vam/assets/player/scripts/Build-PlayerProductPackage.ps1`
6. `products/vam/assets/player/scripts/Build-PlayerProductFamily.ps1`

The release packaging matrix should follow the version/capability schedule in
`products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`
so the family can produce multiple variants on a skewed cadence without inventing
new release logic for each surface.

Exit condition:

1. any SKU can be built from one product id

### Packet 5.5: Version Capability Schedule

Goal:

1. decide version bundles before implementation starts
2. bind those bundles to the product matrix
3. allow skewed release cadence without ad hoc reasoning

Authority file:

1. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`
2. `products/vam/config/player_version_capability_schedule.v1.json`

Exit condition:

1. every planned version has a table row before feature work starts
2. the JSON schedule artifact is updated in the same slice as any schedule change

### Packet 6: Parallel Witness Builds

Goal:

1. prove the matrix is real before new features land

Required witness set:

1. Player free/pro/pro_demo
2. Modern TV Player free/pro/pro_demo
3. Laptop Player free/pro/pro_demo
4. Phone Player free/pro/pro_demo
5. Tablet Player free/pro/pro_demo

Validation rule:

1. all products must build from shared inputs
2. no product may require one-off manual reasoning
3. any missing piece becomes an architecture task, not a feature detour

Exit condition:

1. the full matrix can be built and deployed in a deterministic way

### Packet 7: Feature Continuation

Goal:

1. move the few remaining desired player features into the new modular lane only

Rule:

1. no feature work until packets 1 through 6 are stable

## Scroller Parallel Track

The scroller track should run in parallel only as a brand/support lane.

Authority doc:

1. `products/vam/plugins/ui_scroller/docs/JOYSTICK_SCROLLER_PUBLIC_UPDATE_LADDER_V1.md`

Rule:

1. do not let scroller updates become a distraction from packets 1 through 6

## Success Condition

This plan is complete when the next developer can implement the new family by:

1. selecting a packet
2. following the file list
3. building the next artifact

without inventing structure in the middle of the task.
