# VaM Deployment And Naming Canon V1

## Purpose

This note defines the canonical naming and deployment rules for versioned VaM
artifacts in this repo.

The goal is to stop treating typos, stale filenames, or same-version redeploys
as silent design decisions.

This canon exists because VaM caches package and artifact identity aggressively
enough that ambiguous naming creates false regressions and bad test witnesses.

## Current deployment labels

Use these labels literally:

1. `dev_deploy`
   - versioned raw `Custom/...` authority
   - fastest interactive/debug seam
2. `prerelease_deploy`
   - versioned `.var` authority using `Dev` externally and `dev` internally
   - operator prerelease testing seam
3. `release_deploy`
   - versioned `.var` authority using `Prod` externally and `prod` internally
   - final public release seam

## Hard version rules

For changed code:

1. bump the semantic version
2. do not reuse a previously tested semantic version for new code
3. update all version-boundary files and outputs for that semantic version

For same-code redeploys:

1. keep the semantic version
2. bump the deploy iteration
3. if the seam is `.var`, also bump the outer package rung

Working interpretation:

1. `0.6.11` cannot be reused for changed code once it already exists as a tested
   version boundary
2. if the operator only wants the current code redeployed for witness cleanup,
   keep `0.6.11` and move from `alpha` to `beta`, or from `.11.var` to `.12.var`
   depending on the seam

## Hard caching rule

For any one version boundary:

1. choose exactly one live authority seam:
   - `.var`
   - or raw `Custom/...`
2. do not keep the same version live in both seams at once
3. do not assume deleting a `.var` clears VaM memory of it
4. do not assume a same-version raw `Custom/...` artifact is harmless after VaM
   has already seen the `.var`

If there is doubt about which copy VaM is loading, the test result is not
trustworthy.

For raw `dev_deploy` slices that still need package inventory:

1. build package inventory with `-SkipVarDistribute`
2. do not use `-PackageOnlyDeploy` for a raw-only slice
3. package reports may exist as build inventory without creating live `.var`
   authority in `AddonPackages`

## Canonical extension correction

These are correction rules, not optional style:

1. `.vap` is canonical for presets
2. `.assetbundle` is canonical for Unity assetbundles
3. `.dll` is canonical for plugins
4. `.json` is canonical for scenes/receipts/configs in this lane

Treat these as typo recovery:

1. `.vab` means `.vap`
2. `.asset` means `.assetbundle` when the lane is clearly talking about the
   built Unity bundle artifact
3. `.assetbnudle` or similar misspellings mean `.assetbundle`

Do not promote those typos into canon.

## Ambiguity rule

Operator inconsistency is not automatic canon.

If the operator asks for:

1. an already-used tested semantic version
2. a filename token that breaks the established pattern
3. a bad extension
4. a seam mix that violates the live-authority rule

then recover the likely intent first.

Examples:

1. if `0.6.23` already exists and the operator asks for `0.6.23` again, stop
   and ask whether they meant a redeploy of `0.6.23` or a new semantic version
2. if the operator writes `.vab`, use `.vap`
3. if the operator writes a prerelease plugin as `plugin_player_dev...dll`,
   recover it to `plugin_player_stage...dll`

## Subject token rule

`SUBJECT` is a lowercase slug that identifies the resource family or host.

Allowed examples:

1. `moderntv`
2. `modern_tv`
3. `tv`
4. `crt_tv`

Working preference:

1. use lowercase
2. use underscores when they improve readability
3. do not use spaces

## Dev naming pattern

Use `dev_deploy` names for raw `Custom/...` testing:

1. `scene_dev_SUBJECT.<semver>.<iteration>.json`
2. `Preset_dev_SUBJECT.<semver>.<iteration>.vap`
3. `asset_dev_SUBJECT.<semver>.<iteration>.assetbundle`
4. `plugin_player_dev.<semver>.<iteration>.dll`

Example:

1. `scene_dev_moderntv.0.6.11.alpha.json`
2. `Preset_dev_moderntv.0.6.11.alpha.vap`
3. `asset_dev_moderntv.0.6.11.alpha.assetbundle`
4. `plugin_player_dev.0.6.11.alpha.dll`

Same code, redeploy:

1. `scene_dev_moderntv.0.6.11.beta.json`
2. `Preset_dev_moderntv.0.6.11.beta.vap`
3. `asset_dev_moderntv.0.6.11.beta.assetbundle`
4. `plugin_player_dev.0.6.11.beta.dll`

Changed code:

1. `scene_dev_moderntv.0.6.12.alpha.json`
2. `Preset_dev_moderntv.0.6.12.alpha.vap`
3. `asset_dev_moderntv.0.6.12.alpha.assetbundle`
4. `plugin_player_dev.0.6.12.alpha.dll`

## Prerelease naming pattern

Use `prerelease_deploy` names for `.var` testing with the `Dev` channel:

1. `scene_stage_SUBJECT.<semver>.<iteration>.json`
2. `Preset_stage_SUBJECT.<semver>.<iteration>.vap`
3. `asset_stage_SUBJECT.<semver>.<iteration>.assetbundle`
4. `plugin_player_stage.<semver>.<iteration>.dll`
5. `FrameAngel.PlayerDev.<n>.var`

Example:

1. `scene_stage_moderntv.0.6.11.alpha.json`
2. `Preset_stage_moderntv.0.6.11.alpha.vap`
3. `asset_stage_moderntv.0.6.11.alpha.assetbundle`
4. `plugin_player_stage.0.6.11.alpha.dll`
5. `FrameAngel.PlayerDev.11.var`

Same code, redeploy:

1. `scene_stage_moderntv.0.6.11.beta.json`
2. `Preset_stage_moderntv.0.6.11.beta.vap`
3. `asset_stage_moderntv.0.6.11.beta.assetbundle`
4. `plugin_player_stage.0.6.11.beta.dll`
5. `FrameAngel.PlayerDev.12.var`

## Release naming pattern

Use `release_deploy` names for public release bundles.

General pattern:

1. `demo_SUBJECT.<n>.json`
2. `SUBJECT.<n>.vap`
3. `SUBJECT.<n>.assetbundle`
4. `SUBJECT.<n>.dll`
5. `FrameAngel.<ProductName>.<n>.var`

Keep folder depth minimal in the staged `.var` structure. Shorter is better as
long as the staged paths remain unique.

## Release family prefix examples

Current intended shorthand families:

1. `fap`
   - FrameAngel Player
2. `fapp`
   - FrameAngel Player Pro
3. `fappd`
   - FrameAngel Player Pro Demo

### TV bundle examples

Player TV:

1. `fap_tv_demo.1.json`
2. `fap_modern_tv.1.vap`
3. `fap_modern_tv.1.assetbundle`
4. `fap_crt_tv.1.vap`
5. `fap_crt_tv.1.assetbundle`
6. `fap_tv_player.1.dll`
7. `FrameAngel.PlayerTV.1.var`

Player Pro TV:

1. `fapp_tv_demo.1.json`
2. `fapp_modern_tv.1.vap`
3. `fapp_modern_tv.1.assetbundle`
4. `fapp_crt_tv.1.vap`
5. `fapp_crt_tv.1.assetbundle`
6. `fapp_tv_player.1.dll`
7. `FrameAngel.PlayerProTV.1.var`

Player Pro Demo TV:

1. `fappd_tv_demo.1.json`
2. `fappd_modern_tv.1.vap`
3. `fappd_modern_tv.1.assetbundle`
4. `fappd_crt_tv.1.vap`
5. `fappd_crt_tv.1.assetbundle`
6. `fappd_tv_player.1.dll`
7. `FrameAngel.PlayerProDemoTV.1.var`

## Current sanity-check rule

If the operator writes a pattern example and misses one token, treat the pattern
intent as stronger than the isolated typo.

That means:

1. recover obvious extension mistakes
2. recover obvious channel-token mistakes
3. stop and confirm only when the mistake changes version authority or release
   meaning

## Current references

Use this canon with:

1. `C:\projects\fa\AGENTS.md`
2. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_DEV_SCENE_AND_DEPLOY_PROCESS_V1.md`
3. `C:\projects\fa\products\vam\docs\handoffs\VAM_VAR_PACKAGE_PROCESS_AND_METADATA_V1.md`
