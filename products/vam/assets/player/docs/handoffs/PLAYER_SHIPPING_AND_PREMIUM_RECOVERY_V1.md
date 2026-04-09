# Player Shipping And Premium Recovery V1

## Purpose

Capture the bounded recovered seams for:

1. `.var` packaging
2. plugin obfuscation
3. premium/freemium compile-surface architecture

This packet is meant to keep those shipping decisions out of thread memory while the
core player lane continues to recover in `C:\projects\fa`.

## Current Canon In `fa`

The current authority seam in this repo is still the raw versioned release pair:

- `Custom/Assets/FrameAngel/Player/fa_player_asset.<version>.assetbundle`
- `Custom/Plugins/fa_cua_player.<version>.dll`

The bounded release wrapper is:

- `C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerScreenCoreFoundation.ps1`

The local phase-1 scene/subscene helper is:

- `C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerDemoScene.ps1`

Those current scene/subscene outputs are raw VaM-relative witnesses, not package-safe
shipping artifacts.

## `.var` Packaging Recovery

### Verified historical packager

The verified historical `.var` packager lives in the monorepo truth root:

- `C:\projects\frameangel\products\vam\assets\player\scripts\Build-CuaPlayerVarPackage.ps1`

This is the recovered shipping seam to port forward, not the many recovery worktree
variants.

### Historical staged layout

The recovered packager stages:

- root `meta.json`
- root `frameangel_player_var_manifest.json`
- plugin DLL under `Custom/Scripts/<dll>`
- direct-CUA assetbundle under `Custom/Assets/FrameAngel/<bundle>`
- CUA preset under `Custom/Atom/CustomUnityAsset/<preset>.vap`
- fallback host package under `Custom/PluginData/FrameAngel/<resourceId>/...`

The generated package filename shape is:

- `<Creator>.<Package>.<PublicRelease>.var`

and the old script can optionally distribute directly to:

- `F:\sim\vam\AddonPackages`

### Important limitation

The current `fa` demo scene/subscene builder does **not** yet rewrite all references
into `.var`-safe packaged paths.

Current raw scene/subscene outputs still point at direct VaM-relative paths such as:

- `Custom/Assets/FrameAngel/Player/fa_player_asset.<version>.assetbundle`
- `Custom/Plugins/fa_cua_player.<version>.dll`
- `Custom/SubScene/FrameAngel/controls/player_controls.json`

That is good for local testing, but not enough for final single-`.var` shipping.

### What still has to be standardized for single-`.var` shipping

To ship the desired single package containing:

- assetbundle
- plugin DLL
- subscene
- scene that uses the subscene
- demo media

the packaging lane must also rewrite:

1. scene plugin references
2. scene/subscene asset references
3. subscene `storePath` references
4. demo image/video references

into one consistent packaged layout.

The user-provided scene/subscene structure can remain the manual source of truth, but
the final ship pipeline must rewrite all cross-file references relative to the package
layout instead of zipping the current raw `.json` files unchanged.

## Obfuscation Recovery

### Verified historical seam

The historical plugin obfuscation seam is:

- `C:\projects\frameangel\products\vam\plugins\player\scripts\Obfuscate-Plugin.ps1`

Backed by:

- `C:\projects\frameangel\products\vam\plugins\player\build\obfuscation.defaults.json`

The historical tool used was Obfuscar via:

- `Obfuscar.GlobalTool`

### Historical placement in build flow

The older lane built first, then obfuscated on hardened channels, then applied release
protection/attestation, then deployed.

### Recommended narrow port into `fa`

If obfuscation is reintroduced in `fa`, keep it bounded:

1. build raw `fa_cua_player.<version>.dll`
2. obfuscate that DLL into a staged output
3. keep the **same versioned filename shape**
4. validate the release pair and hashes

Do **not** import the whole old build lane just to recover obfuscation.

The right insertion point is after:

- `C:\projects\fa\products\vam\assets\player\scripts\Build-CuaPlayerResource.ps1`

and before final release validation in:

- `C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerScreenCoreFoundation.ps1`

## Premium / Freemium Recovery

### What survives today

No surviving Patreon/auth-code/unlock implementation was found in the current `fa` repo
or the tightly scoped player truth roots.

What **does** survive is the compile-surface architecture needed to build that cleanly:

- `FRAMEANGEL_CUA_PLAYER` in
  - `C:\projects\fa\products\vam\plugins\player\vs\fa_cua_player\fa_cua_player.csproj`
- `FRAMEANGEL_TEST_SURFACES` compile guards in
  - `C:\projects\fa\products\vam\plugins\player\src\scene-runtime\FASyncRuntime.cs`
- explicit source inclusion in the project file
- stub/sidecar pattern such as
  - `C:\projects\fa\products\vam\plugins\player\src\cua-runtime\FASyncRuntime.CuaTextInputBridge.cs`

### Recommended premium architecture

The preferred approach is:

1. build the full feature set as bounded modules
2. keep baseline/freemium builds as the core compile surface
3. include premium modules by explicit compile defines or conditional source inclusion
4. ship no-op stubs in the baseline build where the contract must remain stable
5. compile premium modules **out entirely** when they are not part of the target build

That matches the user’s desired direction better than runtime-disabled code or a
network-first licensing layer.

### Important guardrail

If a future auth-code layer is ever added, keep it local/offline and compatible with the
existing VaM forbidden-usage guardrails. Do not assume a live network licensing check.

## Working Recommendation

Use this order going forward:

1. finish core player recovery in `fa`
2. import the old `.var` packager as a bounded shipping seam
3. add scene/subscene/media reference rewriting for packaged output
4. reintroduce obfuscation as an opt-in hardened step
5. recover premium features as compile-time modules, not runtime-disabled baggage
