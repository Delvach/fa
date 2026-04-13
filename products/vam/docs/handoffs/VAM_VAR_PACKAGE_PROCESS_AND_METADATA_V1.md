# VaM Var Package Process And Metadata V1

## Purpose

This note records the repo-local process for building FrameAngel `.var` packages and the shared metadata contract now used by both:

- player
- joystick scroller

The goal is to stop hand-editing `meta.json`, stop rediscovering Package Builder conventions, and keep package metadata reusable without letting package identity drift away from the actual built artifact.

## Shared Metadata Canon

The shared resolver lives at:

- `shared/scripts/vam-packaging/Resolve-FrameAngelVarPackageMetadata.ps1`

Supported metadata fields:

- `licenseType`
- `creatorName`
- `packageName`
- `description`
- `credits`
- `instructions`
- `promotionalLink`

There is not currently a separate `copyright` field in the sampled `.var` packages we checked. Attribution or copyright-style text should go in:

- `credits`

`licenseType` uses compact human-readable labels, not long prose. Current known examples:

- `FC`
- `CC BY`
- `CC BY-SA`

## Identity Rule

Package identity remains authoritative.

That means:

- player package identity comes from the player packager inputs
- joystick scroller package identity comes from the catalog identity chosen by channel

The metadata JSON is for reusable defaults and operator text. It must not silently invent a different creator or package name than the actual built `.var`.

## Versioned Filename Rule

Every distributable artifact must carry a visible version in its filename.

That includes:

- `.var`
- packaged assetbundle names
- packaged plugin names
- presets
- scenes

Reason:

1. VaM caches package and asset identity in ways that survive simple file
   removal during a session
2. the operator needs to see exactly which artifact is being loaded
3. same-name or ambiguous artifacts make debugging and witness trust worse

## Seam Exclusivity Rule

For any one version boundary, choose one live authority seam only:

1. `.var`
2. or raw `Custom/...`

Do not keep the same version live in both seams at once.

Do not rely on:

1. removing a `.var` to clear VaM memory of it
2. same-version `Custom/...` assets being ignored after VaM has already seen the
   `.var`

If a slice is being tested package-first, the `.var` is the only live test
authority.

If a slice is being tested raw in `Custom/...`, the raw artifact set is the only
live test authority.

If the lane needs to switch seams, switch the version boundary too.

## Channel Naming Rule

Current standard:

Pre-release:

- external package naming uses `Dev`
- internal asset/plugin naming uses `dev`

Final release:

- external package naming uses `Prod`
- internal asset/plugin naming uses `prod`

Do not mix those channels inside one built artifact or one test boundary.

## Repo-Local Metadata Files

Player metadata defaults:

- `products/vam/assets/player/config/var.package.metadata.json`

Joystick scroller metadata defaults:

- `products/vam/plugins/ui_scroller/config/var.package.metadata.json`

These JSON files can safely prefill:

- `licenseType`
- `creatorName`
- `packageName`
- `description`
- `credits`
- `instructions`
- `promotionalLink`

The builders automatically omit empty optional fields when writing `meta.json`.

## Player Process

Top-level wrapper:

- `products/vam/assets/player/scripts/Build-PlayerScreenCoreFoundation.ps1`

Var packager:

- `products/vam/assets/player/scripts/Build-CuaPlayerVarPackage.ps1`

Default metadata path:

- `products/vam/assets/player/config/var.package.metadata.json`

Optional override parameter:

- `-VarPackageMetadataPath` on `Build-PlayerScreenCoreFoundation.ps1`
- `-MetadataPath` on `Build-CuaPlayerVarPackage.ps1`

The player var packager now writes the resolved metadata into:

- `meta.json`
- `frameangel_player_var_manifest.json`
- `player_var_package_report_latest.json`

## Joystick Scroller Process

Builder and packager:

- `products/vam/plugins/ui_scroller/scripts/Build-UiScroller.ps1`

Default metadata path:

- `products/vam/plugins/ui_scroller/config/var.package.metadata.json`

Optional override parameter:

- `-PackageMetadataPath`

The scroller builder now writes the resolved metadata into:

- `meta.json`
- `frameangel_joystick_scroller_var_manifest.json`
- `joystick_scroller_var_package_report.json`

Package identity still comes from:

- `products/vam/plugins/ui_scroller/config/joystick_scroller.catalog.json`

So metadata defaults can be shared without weakening the dev vs release identity contract.

## Practical Editing Rule

If the operator wants to change reusable package metadata:

1. edit the lane-local `var.package.metadata.json`
2. rebuild with the normal script
3. inspect the staged `meta.json` and package report

Do not hand-edit staged `meta.json` files in build output folders.

## Current Package Builder Parity

This repo-local process now covers the fields we actually need most often:

- `licenseType`
- `creatorName`
- `packageName`
- `description`
- `credits`
- `instructions`
- `promotionalLink`

If later we decide to support more Package Builder metadata keys, add them first to:

- `Resolve-FrameAngelVarPackageMetadata.ps1`

and then let both packagers inherit the change from the same seam.
