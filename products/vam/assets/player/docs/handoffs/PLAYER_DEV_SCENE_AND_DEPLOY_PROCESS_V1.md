# Player Dev Scene And Deploy Process V1

## Purpose

This note records the deterministic current process for building, packaging, and testing the DevPlayer lane from the repo-local PowerShell wrappers.

It exists because the lane already depends on these conventions in practice:

- repo-local scene template generation
- package-first deployment into `F:\sim\vam\AddonPackages`
- numeric outer `FrameAngel.DevPlayer.<n>.var` identity
- semantic internal `0.6.x` player versioning
- current `controls_example` scene control wiring

This is the operator-facing truth for the current dev lane unless a later repo-local handoff supersedes it.

## Current Naming

There are two version layers and they are intentionally different.

Semantic player version:

- `products/vam/assets/player/player.version.json`
- internal packaged asset: `dev_cua_player.<version>.assetbundle`
- internal packaged plugin: `dev_plugin_player.<version>.dll`

Outer dev package identity:

- `FrameAngel.DevPlayer.<n>.var`
- `<n>` is the next numeric rung in `F:\sim\vam\AddonPackages`
- this numeric rung does not need to match the semantic player version

Example:

- semantic player version: `0.6.6`
- distributed package: `FrameAngel.DevPlayer.5.var`
- packaged asset URL: `FrameAngel.DevPlayer.5:/Custom/Assets/FrameAngel/Player/dev_cua_player.0.6.6.assetbundle`
- packaged plugin URL: `FrameAngel.DevPlayer.5:/Custom/Scripts/dev_plugin_player.0.6.6.dll`

## Deterministic Scene Pipeline

The packaged player scene is not authored in Unity.

It is generated from a repo-local template by PowerShell, then copied into the staged `.var`.

Call chain:

1. `products/vam/assets/player/scripts/Build-PlayerScreenCoreFoundation.ps1`
2. `products/vam/assets/player/scripts/Build-CuaPlayerVarPackage.ps1`
3. `products/vam/assets/player/scripts/Build-PlayerDemoScene.ps1`

Default template:

- `products/vam/assets/player/scene_templates/controls_example.json`
- preview image beside it:
  `products/vam/assets/player/scene_templates/controls_example.jpg`

The scene builder does these deterministic rewrites:

- finds the player screen atom, preferring `screen_cua`
- rewrites the scene atom `asset` storable to the passed asset URL
- rewrites `PluginManager` so `plugin#0` points at the passed plugin path
- rewrites `plugin#0_FASyncRuntime` values for:
  - `Player Media Path`

For packaged builds, those URLs are package-scoped `Creator.Package.Tag:/Custom/...` paths.

## Current Controls Example Contract

The current repo-local scene template is not the older managed button layout.

The current inline scene control IDs are:

- `button_previous`
- `button_toggle_play`
- `button_load`
- `button_next`
- `slider_progress`
- `display_curr`
- `display_total`
- `checkbox_shuffle`

The `fap` atom is placement-only. It is a positioning parent, not a runtime dependency.

As of `0.6.5+`, `Build-PlayerDemoScene.ps1` detects this current layout and wires the existing scene atoms in place instead of expecting older IDs like:

- `play_pause_button`
- `previous_button`
- `reload_button`
- `next_button`
- `scrub_slider`

Current packaged scene trigger wiring:

- `button_previous` -> `Player Previous`
- `button_toggle_play` -> `Player Play Pause`
- `button_load` -> `Player Load Media`
- `button_next` -> `Player Next`
- `slider_progress` -> `scrub_normalized`
- `checkbox_shuffle` -> `Player Random On`

The current and total text displays are still part of the scene template contract, but this handoff is specifically about the deterministic interactive control seam.

## Package-First Deployment Rule

For the current DevPlayer lane, the live test authority is the `.var` in:

- `F:\sim\vam\AddonPackages`

Do not treat raw `Custom\Assets` or raw `Custom\Plugins` copies as the test authority when the lane is package-first.

Current deterministic package-first build pattern:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File products/vam/assets/player/scripts/Build-PlayerScreenCoreFoundation.ps1 `
  -RepoRoot C:\projects\fa `
  -Version <semantic-version> `
  -BuildVarPackage `
  -PackageOnlyDeploy `
  -IncludeVarScene `
  -VarCreatorName FrameAngel `
  -VarPackageName DevPlayer `
  -VarSceneIncludeManagedControls 1
```

Expected outputs:

- release receipt under
  `products/vam/assets/player/build/releases/<version>/`
- package report under
  `products/vam/assets/player/build/var_packages/<version>/direct_cua/`
- distributed package under
  `F:\sim\vam\AddonPackages\FrameAngel.DevPlayer.<n>.var`

## Versioned Release Boundary Rules

Every testable player release slice must include:

- code seam
- semantic version stamp
- matching changelog file
- git commit
- build receipt
- distributed `.var`

Current versioned files:

- `products/vam/assets/player/player.version.json`
- `products/vam/plugins/player/src/shared-runtime/BuildRuntimeInfo.cs`
- `products/vam/assets/player/changelog/<version>.json`

## Current Practical Checklist

When shipping the next DevPlayer rung:

1. make the bounded code change
2. update `player.version.json`
3. update `BuildRuntimeInfo.cs`
4. add `products/vam/assets/player/changelog/<version>.json`
5. build with `Build-PlayerScreenCoreFoundation.ps1` from `C:\projects\fa`
6. confirm:
   - `foundation_release_validation.json`
   - `player_var_package_report_latest.json`
   - distributed `FrameAngel.DevPlayer.<n>.var`
7. commit the release boundary

## Known Continuity Notes

- The repo currently contains real handoff docs under `products/vam/assets/player/docs/handoffs/`, but some canon doc paths referenced in `AGENTS.md` are not present verbatim in the working tree.
- The current dev lane should rely on live code, release receipts, package reports, and this repo-local process note over assumed older thread memory.
