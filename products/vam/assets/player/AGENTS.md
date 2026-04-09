# FrameAngel Player Assets Contract

This subtree is the clean landing zone for the CUA player asset lane.

Use these rules for work under:

- `C:\projects\fa\products\vam\assets\player`

## Purpose

This lane owns:

1. the minimal player assetbundle build wrapper
2. the minimal versioned release wrapper
3. forbidden-term validation for the player runtime
4. the local Unity authoring/exporter project for the bare player screen core under `unity/player-screen-2018`
5. the first-release boundary for the in-VaM screen plus VaM control seam
6. the tracked per-version changelog source under `changelog/<version>.json`

It does **not** own the wider toolkit, probe ladder, recovery ladder, or
general player debugging surface.

## Release phases

Current phase is:

1. first usable release = authored player screen plus exposed player methods that VaM buttons and sliders can call directly
2. the deterministic default-scene or Volodeck witness seam should exercise those same exposed methods, not hidden harness-only setup
3. Meta UI control surfaces are later-phase integration work, not first-release authority

Do not quietly slide this lane from phase 1 into Meta-surface ownership.

## Current migration truth

The asset lane in this repo now keeps only:

1. `Build-CuaPlayerResource.ps1`
2. `Build-PlayerAssetBundle.ps1`
3. `Build-PlayerDemoScene.ps1`
4. `Build-PlayerScreenCoreFoundation.ps1`
5. `Validate-PlayerScreenCoreRelease.ps1`
6. `Validate-VamForbiddenUsage.ps1`
7. `vam-forbidden-terms.json`

The plugin compile backend is still intentionally plugin-rooted under:

1. `C:\projects\fa\products\vam\plugins\player\vs\fa_cua_player`
2. `C:\projects\fa\products\vam\plugins\player\src`

That fallback is allowed because it is the actual compile surface for the new
repo, not a deprecated external lane.

## Anti-pollution rule

Do not widen this lane by re-importing:

1. meta-toolkit surfaces
2. probe/test ladders that are not part of the release process
3. recovery or archaeology scripts
4. hostile-review, obfuscation, or catalog packaging layers unless explicitly requested

Keep build outputs local to:

1. `C:\projects\fa\products\vam\assets\player\build`

Keep tracked Unity source local to:

1. `C:\projects\fa\products\vam\assets\player\unity\player-screen-2018`

## Documentation rule

Do not treat generated `build/` receipts as a replacement for code truth, but
this repo does not need the old handoff/doc sprawl imported just to build.

## Version changelog rule

Every versioned player build in this repo must have:

1. a tracked changelog source file at `C:\projects\fa\products\vam\assets\player\changelog\<version>.json`
2. release-root emitted copies at `foundation_release_changelog.json` and `foundation_release_changelog.md`
3. reasoning for why the version exists, not just what changed

## Player truth lock

Carry forward the player-lane truth:

1. versioned filenames are the authority seam
2. deploy to the active VaM directories only through the minimal wrappers
3. fail fast on forbidden runtime usage
4. do not reintroduce preset/bootstrap drift into the bare assetbundle release
5. do not treat Meta UI components as part of first-release success
6. the deterministic VaM button-and-slider witness scene is part of the bounded phase-1 seam, not a sidecar archaeology script

## Deterministic witness scene

`Build-PlayerDemoScene.ps1` is the local bounded builder for the phase-1 witness
scene.

Current rule:

1. it must target the versioned raw assetbundle plus matching versioned plugin
2. it must use the `buttons_setup_scene.json` template by default
3. it exists to prove the exposed player method surface through ordinary VaM buttons and sliders
4. it should not grow into a Meta/toolkit/probe-ladder import path

## Migration slice rule

Every migration slice here should say:

1. what minimal release-path file was added or changed
2. whether it changed live deploy behavior
3. whether it changed versioned artifact names
4. whether it changed forbidden-usage enforcement
