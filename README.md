# fa

Minimal clean-room start for the FrameAngel core CUA player.
This repo is the replacement home for the FrameAngel monorepo player code.

Current imported foundation:

- `products/vam/plugins/player/vs/fa_cua_player`
- `products/vam/plugins/player/src`
- `products/vam/assets/player`
- `shared/scripts/player-assets`

Current scope is intentionally narrow:

- the `fa_cua_player` Visual Studio project
- the exact runtime source files compiled by that project
- one minimal player assetbundle export lane
- one minimal versioned release wrapper
- the phase-1 release boundary: screen plus VaM controls
- no scene/session-only runtime surface
- no toolkit, recovery, catalog, or probe-ladder drift
- no `unknown_vs` compile root fallback in this repo

Current release order is intentional:

- first usable release is the authored screen with VaM buttons and sliders calling exposed player methods
- deterministic scene or Volodeck setup is a witness seam for that same in-game method surface
- the future richer control approach is custom projected scene controls wired to the same player methods
- Meta UI components are a later integration phase, not first-release authority

Version and commit discipline:

- the clean-room repo version line starts at `0.1.1`
- every versioned player change that is built for testing or release gets its own git commit
- commit messages should be verbose enough to capture the version, changed seam, and visible outcome

Core release entrypoint:

- `products/vam/assets/player/scripts/Build-PlayerScreenCoreFoundation.ps1`

Minimal scripts kept in this repo:

- `products/vam/assets/player/scripts/Build-CuaPlayerResource.ps1`
- `products/vam/assets/player/scripts/Build-PlayerAssetBundle.ps1`
- `products/vam/assets/player/scripts/Build-PlayerDemoScene.ps1`
- `products/vam/assets/player/scripts/Build-PlayerScreenCoreFoundation.ps1`
- `products/vam/assets/player/scripts/Validate-PlayerScreenCoreRelease.ps1`
- `products/vam/assets/player/scripts/Validate-VamForbiddenUsage.ps1`

Examples:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-CuaPlayerResource.ps1 -RepoRoot C:\projects\fa
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerScreenCoreFoundation.ps1 -RepoRoot C:\projects\fa
```

Local-only validation without touching live VaM directories:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerScreenCoreFoundation.ps1 -RepoRoot C:\projects\fa -SkipLiveDeploy
```

Deterministic phase-1 witness scene using VaM buttons and sliders:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerDemoScene.ps1 -RepoRoot C:\projects\fa -Version 0.1.1 -AllowExistingVersion
```

Current witness output pattern:

- `F:\sim\vam\Saves\scene\fa_scene.0.1.1.json`
- `C:\projects\fa\products\vam\assets\player\build\scene_builds\0.1.1\player_demo_scene_build_receipt.md`
