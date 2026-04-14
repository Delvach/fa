# Player Shell Recovery Boundary V1

## Purpose

This lane restores the authored player shell families around the now-working
screen-only CUA player without re-expanding the `fa` repo back into the old
plugin-root monorepo layout.

## Current source authority

Repo-local authored Unity source for shell recovery is:

`C:\projects\fa\products\vam\assets\player\unity\ghost_training_export_clone`

Repo-local Unity bridge package for that lane is:

`C:\projects\fa\products\vam\assets\player\unity_editor_bridge\current`

The current shell lane must not depend on external legacy repos.

Historical recovery source may still exist elsewhere, but it is not current
authority for the new system.

## Shell families in scope

Recovered authored shell keys:

1. `ivone_phone`
2. `ivad_tablet`
3. `mcbrooke_laptop`
4. `modern_tv`
5. `retro_tv`

These are real authored shells, not speculative product prose.

## Script surfaces

Shell package/catalog seam:

1. `C:\projects\fa\products\vam\assets\player\scripts\Build-CuaPlayerHostPackage.ps1`
2. `C:\projects\fa\products\vam\assets\player\scripts\Build-CuaPlayerHostCatalog.ps1`
3. `C:\projects\fa\products\vam\assets\player\scripts\Export-GhostPlayerHostCatalog.ps1`

Direct CUA family seam:

1. `C:\projects\fa\products\vam\assets\player\scripts\Export-GhostPlayerHostCuaFamily.ps1`

VaM-valid raw shell seam:

1. the repo already contains 2018 raw shell build outputs under
   `C:\projects\fa\products\vam\assets\player\build\shell_assetbundle_exports_2018`
2. those bundles are the current known-good compatibility class for shell
   loading in VaM
3. the raw 2018 shell exporter implementation is restored in the repo at:
   - `C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerShellAssetBundles.ps1`
   - `C:\projects\fa\products\vam\assets\player\unity\player-screen-2018\Assets\Editor\FrameAngelPlayerShell2018Exporter.cs`
4. use that seam when an interactive host must actually load in VaM
5. current `modern_tv` interactive proof authority is the raw `0.6.15` seam:
   - host bundle:
     `F:\sim\vam\Custom\Assets\FrameAngel\Player\asset_dev_modern_tv.0.6.15.alpha.assetbundle`
   - preset:
     `F:\sim\vam\Custom\Atom\CustomUnityAsset\preset_dev_modern_tv.0.6.15.alpha.vap`
   - plugin:
     `F:\sim\vam\Custom\Plugins\plugin_player_dev.0.6.15.alpha.dll`
   - baseline direct-player raw asset:
     `F:\sim\vam\Custom\Assets\FrameAngel\Player\asset_dev_player.0.6.15.alpha.assetbundle`
6. that proof now exports from the composed host package root under
   `C:\projects\fa\products\vam\assets\player\build\host_catalog\theme_00\modern_tv\...`
   rather than from the shell-only package root, which is what carries the
   visible `meta_ui_video_player` control carrier into the VaM-valid raw host
   bundle

## Build output homes

All shell recovery outputs stay local to the asset lane:

1. `C:\projects\fa\products\vam\assets\player\build\host_shell_exports`
2. `C:\projects\fa\products\vam\assets\player\build\host_catalog`
3. `C:\projects\fa\products\vam\assets\player\build\host_catalog_runs`
4. `C:\projects\fa\products\vam\assets\player\build\host_packages`
5. `C:\projects\fa\products\vam\assets\player\build\cua_shell_family`
6. `C:\projects\fa\products\vam\assets\player\build\cua_shell_family_runs`
7. `C:\projects\fa\products\vam\assets\player\build\shell_assetbundle_exports_2018`

## Runtime truth

Current runtime in the clean player lane is generic enough for authored shells
that provide the canonical node contract:

1. `screen_surface`
2. `disconnect_surface`
3. `controls_anchor`
4. `bottom_anchor`

Optional hosted/control nodes:

1. `control_surface`
2. `control_collider`
3. `screen_glass`
4. `screen_aperture`

## Important limitation

Phone/tablet swipe behavior is not authored into the shell assets. It is a
runtime/control seam, not a mesh export seam.

## Recovery posture

Do not hand-rebuild these shells from screenshots.

Preferred order:

1. export authored shells from the recovery Unity project
2. keep the Unity source, bridge package, and outputs local to `fa`
3. when VaM load validity matters, route the shell back through the raw 2018
   export seam rather than assuming the 2022 export is compatible
4. when building a versioned release wrapper, preserve the current live
   `dev_cua_player.<version>.assetbundle` and `dev_plugin_player.<version>.dll`
   during cleanup; only stale versions should be removed
5. validate one shell family at a time
6. only after that decide whether any source geometry should be migrated out of
   the recovery Unity project into a new clean authoring root
