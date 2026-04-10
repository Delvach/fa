# Player Shell Recovery Boundary V1

## Purpose

This lane restores the authored player shell families around the now-working
screen-only CUA player without re-expanding the `fa` repo back into the old
plugin-root monorepo layout.

## Current source authority

Temporary authored Unity source for shell recovery is:

`G:\recovery\C-drive\projects\15-training\unity\ghost_training_export_clone`

The clean repo landing zone is:

`C:\projects\fa\products\vam\assets\player`

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

## Build output homes

All shell recovery outputs stay local to the asset lane:

1. `C:\projects\fa\products\vam\assets\player\build\host_shell_exports`
2. `C:\projects\fa\products\vam\assets\player\build\host_catalog`
3. `C:\projects\fa\products\vam\assets\player\build\host_catalog_runs`
4. `C:\projects\fa\products\vam\assets\player\build\host_packages`
5. `C:\projects\fa\products\vam\assets\player\build\cua_shell_family`
6. `C:\projects\fa\products\vam\assets\player\build\cua_shell_family_runs`

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
2. keep outputs local to `fa`
3. validate one shell family at a time
4. only after that decide whether any source geometry should be migrated out of
   the recovery Unity project into a new clean authoring root
