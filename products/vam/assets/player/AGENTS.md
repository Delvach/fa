# FrameAngel Player Asset Subtree Contract

This subtree inherits the repo-root authority from:

- `C:\projects\fa\AGENTS.md`

If this file conflicts with the repo-root canon or the current player handoff
docs, the repo-root canon wins.

## Purpose

This subtree owns the player asset-side build and witness lane:

1. deterministic player assetbundle and release wrappers
2. deterministic player scene generation
3. tracked per-version player changelog files
4. local Unity authoring/exporter projects under `unity/`
5. player build receipts under `build/`

It does not get to redefine repo-wide deployment truth, versioning truth, or
Meta/UI product strategy on its own.

## Current lane truth

1. the current active player version and live raw authority seam are defined by
   the repo-root canon in `C:\projects\fa\AGENTS.md`
2. the active live proof seam for current hosted interaction work is raw
   `dev_deploy`, not packaged prerelease authority
3. canonical raw `dev_deploy` filenames in this subtree follow
   `VAM_DEPLOYMENT_AND_NAMING_CANON_V1.md`:
   - `asset_dev_player.<semver>.<iteration>.assetbundle`
   - `plugin_player_dev.<semver>.<iteration>.dll`
   - `scene_dev_player.<semver>.<iteration>.json`
4. older versioned raw `asset_dev_player.*` and `plugin_player_dev.*` files are
   comparison witnesses and must not be pruned by later builds
5. built `.var` outputs under `build/var_packages` are build artifacts unless
   they were actually distributed and promoted as the live authority seam
6. shells are established enough for operator-led Unity shell work
7. true interactive Meta runtime surfaces remain an active assistant-owned lane
8. the deterministic scene generator is still part of the current player lane,
   and the operator plans to provide an updated base scene layout before that
   generator is revised around new control placement

## Local guardrails

1. keep tracked Unity source local to this subtree
2. keep build outputs local to this subtree
3. do not reintroduce external repo dependencies or deprecated legacy roots
4. do not silently widen build receipts into product truth
5. do not assume old template names or old phase boundaries are still canon;
   verify against current repo-root docs first
