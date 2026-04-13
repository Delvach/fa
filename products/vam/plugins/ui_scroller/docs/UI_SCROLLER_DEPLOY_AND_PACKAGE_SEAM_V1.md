# UI Scroller Deploy And Package Seam V1

## Purpose

This note records the deterministic joystick scroller packaging and live deploy
truth in `C:\projects\fa` so the lane does not have to rely on thread memory.

This lane now shares package metadata/process rules with the broader FrameAngel
product system:

1. `products/vam/docs/handoffs/VAM_VAR_PACKAGE_PROCESS_AND_METADATA_V1.md`
2. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`
3. `products/vam/docs/handoffs/VAM_DEPLOYMENT_AND_NAMING_CANON_V1.md`

Future truth:

1. the joystick scroller should adopt the same `dev_deploy`,
   `prerelease_deploy`, and `release_deploy` authority model as the player lane
2. until a bounded prerelease candidate is ready, do not widen the scroller
   lane into demo-scene or Meta-widget work by assumption

## Current Seams

Loose raw testing seam:

- build the versioned DLL in the repo
- deploy only the loose DLL to `F:\sim\vam\Custom\Plugins`
- use this when the operator explicitly wants raw `Custom\Plugins` testing

Package-first seam:

- build the versioned DLL in the repo
- stage the packaged DLL inside the `.var` under `Custom/Scripts/<dll>`
- distribute the `.var` to `F:\sim\vam\AddonPackages`
- use this when the operator wants package-first testing

## Hard Rule

Do not distribute a joystick scroller `.var` to `AddonPackages` and also copy a
loose `fa_joystick_scroller*.dll` into live VaM `Custom` during the same run.

VaM reads both live `Custom` content and `AddonPackages`, which makes the test
authority ambiguous and invites cache confusion.

## Current Build Wrapper Behavior

`products/vam/plugins/ui_scroller/scripts/Build-UiScroller.ps1` now enforces
this split:

- `-DeployMode package` for package-first `.var` testing
- `-DeployMode raw` for loose `Custom\Plugins` testing

If a run would attempt both live seams at once, the script throws instead of
guessing.
