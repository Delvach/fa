# Player Volodeck Parity Boundary V1

## Purpose

This note defines the deterministic Volodeck witness seam for player shell work.

Volodeck exists to let shell/control work be exercised in Unity before VaM
testing, but only if parity stays honest.

## Current repo-local authority

Volodeck Unity project:

`C:\projects\fa\products\vam\assets\player\unity\ghost_training_export_clone`

Volodeck Unity bridge package:

`C:\projects\fa\products\vam\assets\player\unity_editor_bridge\current`

Current deterministic entry points:

1. `C:\projects\fa\products\vam\assets\player\scripts\Export-GhostPlayerHostCuaFamily.ps1`
2. `C:\projects\fa\products\vam\assets\player\scripts\Export-GhostPlayerHostCatalog.ps1`
3. `GhostMetaUiSetBootstrap.CreateStudySceneBatch`

## Hard rule

Volodeck is a parity witness, not a separate authority surface.

That means:

1. it should exercise the same exposed player methods and control contracts that
   VaM scenes use
2. it should not invent a special control path just for Unity proofing
3. it should not rely on external legacy repos for bridge/runtime setup

## Current integration role

Volodeck is the fastest pre-VaM witness seam for:

1. shell layout and placement
2. control anchoring and motion
3. visible playback/parity checks
4. exported host/package validation before operator testing

## Deterministic posture

Keep the following local to `C:\projects\fa`:

1. the Unity authoring project
2. the Unity bridge package
3. the export wrappers
4. the host/shell export receipts

If any of those drift outside the repo, treat that as architecture debt to
remove before calling Volodeck healthy.
