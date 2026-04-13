# Player Meta UI Packet 1.5 Running Log V1

## Purpose

This is the live handoff log for Packet `1.5`.

Use it when a thread freezes, restarts, or needs to hand off the Meta UI
toolkit foundation work without relying on thread memory.

## Current authority

Packet `1.5` is governed by:

1. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_BLUEPRINT_V1.md`
2. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_EXECUTION_PLAN_V1.md`
3. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`
4. `products/vam/config/player_version_capability_schedule.v1.json`
5. `products/vam/assets/player/docs/handoffs/PLAYER_VOLODECK_PARITY_BOUNDARY_V1.md`

## Working rule

Meta UI toolkit is a modular building block for the new lane.

It is not:

1. a rewrite of the historical first-release boundary
2. a separate architecture root
3. a Unity-only proof lane with special controls

It must stay parity-bound to Volodeck and the same player/control contracts that
VaM will use.

## Live seams

Toolkit export:

1. `products/vam/assets/player/scripts/meta-toolkit/Build-MetaToolkitThemeCatalog.ps1`
2. `products/vam/assets/player/scripts/meta-toolkit/Sync-MetaToolkitThemeCatalog.ps1`

Host/shell parity assembly:

1. `products/vam/assets/player/scripts/Export-GhostPlayerHostCatalog.ps1`
2. `products/vam/assets/player/scripts/Build-CuaPlayerHostCatalog.ps1`
3. `products/vam/assets/player/scripts/Build-CuaPlayerHostPackage.ps1`

Packet `1.5` deterministic wrapper:

1. `products/vam/assets/player/scripts/Build-PlayerMetaUiPacket15Foundation.ps1`

Defaults:

1. `products/vam/assets/player/config/meta_ui_packet_1_5.defaults.json`

## What was completed in this slice

1. Locked the modular product-system canon so Meta UI is explicitly Packet `1.5`.
2. Added a machine-readable family schedule at `products/vam/config/player_version_capability_schedule.v1.json`.
3. Added Packet `1.5` defaults in `products/vam/assets/player/config/meta_ui_packet_1_5.defaults.json`.
4. Added `Build-PlayerMetaUiPacket15Foundation.ps1` to turn the scattered toolkit export and host catalog seams into one deterministic repo-local entry point.
5. Kept the work bounded to repo-local Meta UI, Volodeck, and host catalog seams.
6. Ran the first narrow proof successfully with `modern_tv`, `theme_00`, no preview, and no deploy.
7. Hardened the underlying host-catalog scripts so they can resolve Packet `1.5` defaults instead of assuming one fixed proof surface by default.
8. Re-ran the same narrow proof after that hardening and got a second clean receipt.
9. Refreshed the live standalone Meta proof set into VaM raw `Custom` paths so there are actual interactive Meta presets available in VaM right now.
10. Corrected the Meta video player proof export seam so the live proof script now builds from the authored Volodeck scene in `ghost_training_export_clone` instead of the older snapshot-only `player-screen-2018` exporter.
11. Added a deterministic Volodeck witness wrapper for the authored Meta video-player proof so the lane now emits both a contextual scene preview and a tighter surface preview before operator testing.
12. Tightened the wide witness framing after the first scene preview proved too far out to be trustworthy, and recorded that lesson in the Volodeck guardrails canon.

## What the new wrapper does

`Build-PlayerMetaUiPacket15Foundation.ps1` now:

1. reads repo-local Packet `1.5` defaults
2. exports the Meta UI toolkit catalog for a selected theme
3. feeds that toolkit summary into the Volodeck host catalog export path
4. writes a receipt under `products/vam/assets/player/build/meta_ui_packet_1_5_runs`

This gives the lane one deterministic foundation entry point instead of making
future threads rediscover the toolkit export and shell catalog sequence.

## Expected outputs

Toolkit catalog output:

1. `products/vam/assets/player/build/meta_toolkit_catalog/theme_<nn>/`

Host catalog output:

1. `products/vam/assets/player/build/host_catalog/theme_<nn>/`

Packet `1.5` run receipt:

1. `products/vam/assets/player/build/meta_ui_packet_1_5_runs/meta_ui_packet_1_5_foundation_<timestamp>.json`

First successful narrow proof in this slice:

1. `products/vam/assets/player/build/meta_ui_packet_1_5_runs/meta_ui_packet_1_5_foundation_20260413T171940Z.json`
2. toolkit summary:
   `products/vam/assets/player/build/meta_toolkit_catalog/theme_00/ghost_meta_ui_toolkit_export_summary_theme_00.json`
3. host catalog summary:
   `products/vam/assets/player/build/host_catalog/theme_00/ghost_player_host_catalog_summary.json`

Latest successful narrow proof after host-catalog hardening:

1. `products/vam/assets/player/build/meta_ui_packet_1_5_runs/meta_ui_packet_1_5_foundation_20260413T172437Z.json`

## Current Volodeck visual witness for the authored Meta video-player proof

Current harness command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-MetaVideoPlayerProofVolodeckWitness.ps1 -RepoRoot C:\projects\fa
```

Current outputs:

1. contextual scene preview:
   `products/vam/assets/player/build/meta_video_player_proof_volodeck_witness/video_player_proof_scene_preview.png`
2. tighter surface preview:
   `products/vam/assets/player/build/meta_video_player_proof_volodeck_witness/video_player_proof_surface_preview.png`
3. surface capture metadata:
   `products/vam/assets/player/build/meta_video_player_proof_volodeck_witness/video_player_proof_surface_preview.meta.json`
4. receipt:
   `products/vam/assets/player/build/meta_video_player_proof_volodeck_witness/meta_video_player_proof_volodeck_witness_receipt.json`

Working interpretation:

1. the first wide preview in this slice was too far out to be an honest visual proof
2. the harness was then tightened so the contextual shot is now actually useful
3. the tighter surface shot is currently the better judge for icon crispness and layout truth
4. do not send the operator to test from receipts alone; use these witness images first

## Current interactive Meta witness set in VaM

The following interactive proof assets and presets are now freshly deployed:

1. video player proof
   - assetbundle:
     `F:\sim\vam\Custom\Assets\FrameAngel\Meta\fa_meta_video_player_proof.assetbundle`
   - preset:
     `F:\sim\vam\Custom\Atom\CustomUnityAsset\Preset_FA Meta Video Player Proof.vap`
   - receipt:
     `products/vam/assets/player/build/meta_proof_cua/meta_video_player_proof_cua_build_receipt.json`
2. video player snapshot proof
   - assetbundle:
     `F:\sim\vam\Custom\Assets\FrameAngel\Meta\fa_meta_video_player_snapshot.assetbundle`
   - preset:
     `F:\sim\vam\Custom\Atom\CustomUnityAsset\Preset_FA Meta Video Player Snapshot.vap`
   - receipt:
     `products/vam/assets/player/build/meta_snapshot_cua/meta_video_player_snapshot_cua_build_receipt.json`
3. search bar proof
   - assetbundle:
     `F:\sim\vam\Custom\Assets\FrameAngel\Meta\fa_meta_search_bar_proof.assetbundle`
   - preset:
     `F:\sim\vam\Custom\Atom\CustomUnityAsset\Preset_FA Meta Search Bar Proof.vap`
   - receipt:
     `products/vam/assets/player/build/meta_search_bar_cua/fa_meta_search_bar_proof_build_receipt.json`
4. grid menu 2x4 proof
   - assetbundle:
     `F:\sim\vam\Custom\Assets\FrameAngel\Meta\fa_meta_grid_menu_2x4_proof.assetbundle`
   - preset:
     `F:\sim\vam\Custom\Atom\CustomUnityAsset\Preset_FA Meta Grid Menu 2x4 Proof.vap`
   - receipt:
     `products/vam/assets/player/build/meta_grid_menu_2x4_cua/fa_meta_grid_menu_2x4_proof_build_receipt.json`

## Current operator witness path

To see actual interactive Meta components in VaM right now:

1. add a `CustomUnityAsset` atom
2. load one of the deployed `Preset_FA Meta ... Proof.vap` presets above
3. use the proof preset directly as the witness, not the Packet `1.5` host catalog outputs

Important:

1. the Packet `1.5` host catalog work is the modular shell/family foundation
2. the standalone proof presets above are the current direct VaM interaction witness
3. those are different seams and both are currently useful

## Current interactive hosted-player proof artifact

The first player-backed hosted Meta proof is now deterministic and deployed in
the raw `Custom/...` dev seam:

1. builder:
   `products/vam/assets/player/scripts/Build-MetaInteractiveHostedPlayerProof.ps1`
2. current command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-MetaInteractiveHostedPlayerProof.ps1 -RepoRoot C:\projects\fa -ShellKey modern_tv -PlayerPluginMode raw
```

Current deployed outputs:

1. host bundle:
   `F:\sim\vam\Custom\Assets\FrameAngel\Player\fa_cua_player_modern_tv_v1.assetbundle`
2. player runtime plugin:
   `F:\sim\vam\Custom\Plugins\dev_plugin_player.0.6.10.dll`
3. interactive preset:
   `F:\sim\vam\Custom\Atom\CustomUnityAsset\Preset_FA CUA Player Modern TV Interactive Proof.vap`
4. receipt:
   `products/vam/assets/player/build/meta_interactive_host_proof/modern_tv/receipts/meta_interactive_hosted_player_proof_receipt.json`
5. markdown receipt:
   `products/vam/assets/player/build/meta_interactive_host_proof/modern_tv/receipts/meta_interactive_hosted_player_proof_receipt.md`

Current proof interpretation:

1. this artifact is player-backed and no longer ships an empty `PluginManager`
2. the dev interaction seam now uses raw `Custom/Plugins/dev_plugin_player.<version>.dll`
   again, which matches the existing manual authority seam in the player release
   validator
3. the `.var` package remains the release/output reference, not the live proof
   dependency for this interactive Meta host artifact
4. current shell/control confidence is now:
   - shell orientation and stance from Volodeck shell export preview
   - control visual fidelity from the authored Meta video-player Volodeck proof
   - interaction contract/build truth from the player-backed raw preset above
5. current remaining gap is still live session proof:
   - Halo is offline
   - Volodeck is not yet treated as an exact emulator for every VaM-internal
     interaction behavior

Current supporting visual preflight sources:

1. host shell preview:
   `products/vam/assets/player/build/host_shell_exports/modern_tv/faipe_fa_cua_player_modern_tv_7b8977e1e4de/preview/thumbnail.png`
2. authored control surface preview:
   `products/vam/assets/player/build/meta_video_player_proof_volodeck_witness/video_player_proof_surface_preview.png`

## Current VaM validity findings

The current hosted-player proof split is now explicit:

1. `Build-MetaInteractiveHostedPlayerProof.ps1` currently exports the host shell
   through the 2022 Volodeck host-package path
2. that path is producing `fa_cua_player_modern_tv_v1.assetbundle` with a
   `2022.3.62f3` bundle header
3. VaM rejects that host bundle as `Not valid`
4. the repo already contains a VaM-valid raw modern TV shell bundle under
   `products/vam/assets/player/build/shell_assetbundle_exports_2018/modern_tv/assetbundles/fa_cua_player_modern_tv.assetbundle`
5. that raw shell bundle has a `2018.1.9f2` bundle header and matches the
   compatibility class of the known-good player assetbundle seam

Working interpretation:

1. the 2022 host export is currently a Volodeck/package witness seam, not a
   VaM-valid hosted-player load seam
2. the correct next recovery target for true VaM interaction is the repo-local
   2018 raw shell export seam
3. do not treat the current 2022 hosted-player preset as a successful VaM proof
   until the shell export path is switched back to a VaM-valid bundle class

Current restored raw hosted-player artifact:

1. `Build-PlayerShellAssetBundles.ps1` and
   `FrameAngelPlayerShell2018Exporter.cs` were restored from repo history as the
   raw 2018 shell seam
2. `Build-MetaInteractiveHostedPlayerProof.ps1 -ShellKey modern_tv -PlayerPluginMode raw`
   now routes through that seam
3. current deployed interactive proof preset:
   `F:\sim\vam\Custom\Atom\CustomUnityAsset\Preset_FA CUA Player Modern TV Interactive Proof.vap`
4. current deployed host bundle:
   `F:\sim\vam\Custom\Assets\FrameAngel\Player\fa_cua_player_modern_tv.assetbundle`
5. current deployed host bundle class:
   `2018.1.9f2`
6. stale incompatible host bundle removed:
   `F:\sim\vam\Custom\Assets\FrameAngel\Player\fa_cua_player_modern_tv_v1.assetbundle`

## Current visual fidelity seam

The direct Meta video player proof had drifted:

1. `Build-MetaVideoPlayerProofCua.ps1` was still pointing at `FrameAngelMetaVideoPlayer2018Exporter.BuildAndDeployBatch`
2. that exporter builds from the `player-screen-2018` project and explicitly flattens the control surface to the `control_surface_canvas_snapshot` material
3. that means the bundle could look broadly correct while still shipping rasterized control icons/details

Current correction:

1. `Build-MetaVideoPlayerProofCua.ps1` now points at `GhostMetaVideoPlayerProofCustomUnityAssetExporter.ExportMetaVideoPlayerProofBatch`
2. that exporter builds from the authored Volodeck project at `products/vam/assets/player/unity/ghost_training_export_clone`
3. the proof bundle now comes from the authored `GhostMetaUiSetVideoPlayerProof` scene object instead of a snapshot quad

Working interpretation:

1. snapshot-based exports are still valid for snapshot witnesses
2. they are not trustworthy as the main interactive visual-fidelity proof path
3. use the authored Volodeck proof path when judging whether controls/icons are crisp enough to promote
4. `Build-MetaControlSurfaceProofCua.ps1` still routes through the older snapshot exporter and should not be treated as the primary authored video-player proof witness until it is explicitly upgraded
5. the authored Volodeck proof path now has its own dedicated witness wrapper so future threads do not need to improvise framing

## Current standalone Meta proof boundary

The direct search-bar and grid-menu proofs are more limited than the filenames
suggest:

1. `Build-MetaSearchBarProofCua.ps1` and `Build-MetaGridMenu2x4ProofCua.ps1`
   both route through `Build-MetaControlSurfaceProofCua.ps1`
2. that wrapper uses `FrameAngelMetaVideoPlayer2018Exporter.BuildAndDeployBatch`
3. that exporter explicitly flattens the selected Meta surface to a
   `control_surface_canvas_snapshot` texture on a quad
4. those proofs are therefore snapshot carriers, not full authored interactive
   Meta widgets

Current consequence:

1. `fa_meta_search_bar_proof.assetbundle` and
   `fa_meta_grid_menu_2x4_proof.assetbundle` loading in VaM does not prove full
   interaction
2. magenta on those proofs is a snapshot material/shader seam, not evidence
   that the underlying authored Meta widget interaction contract is working
3. the standalone Meta proof set should currently be classified as:
   - video player proof: authored visual witness, live VaM validity still
     pending by export lane
   - search bar proof: snapshot carrier
   - grid menu 2x4 proof: snapshot carrier

## Provisional operator memory to verify

This is not yet promoted to fully verified witness truth, but it should be
preserved as the current remembered state of the older Meta proof lane:

1. at least one proof surface likely had basic video `play` / `pause`
2. that same surface may also have had the screen/display working
3. hover state was not behaving like a finished interactive surface
4. the slider did not update truthfully
5. the lane could process a few clicks, but it was not yet real full-state
   interactive parity

Working interpretation:

1. some Meta proofs likely reached partial action wiring
2. they did not yet reach full state readback and visual interaction parity
3. the next interaction pass should classify each proof as:
   - display only
   - partial interaction
   - full interaction

## Current defaults

Theme:

1. `theme_00`

Default control surface:

1. `meta_patterns_contentuiexample_videoplayer_e7cfc411`

Default shell set:

1. `modern_tv`
2. `mcbrooke_laptop`
3. `ivone_phone`
4. `ivad_tablet`
5. `retro_tv`

## Cleanup checkpoint 2026-04-13

This lane accumulated stale background sidecars and witness-path ambiguity over
multiple long sessions. The cleanup action taken on `2026-04-13` is now part of
handoff truth:

1. completed background agents were closed after their useful findings were
   harvested into repo docs
2. the active branch remained `feature/docs-canon-release-strategy`
3. `AGENTS.md` remained the only unrelated local dirt
4. the raw 2018 shell export remained the active VaM-valid host authority
5. incompatible or superseded hosted proof outputs must stay demoted in docs so
   filename drift does not send the operator back to them

Operational rule from this checkpoint:

1. do not accumulate completed agents across sessions
2. when a sidecar result becomes repo truth, close the sidecar
3. if the thread freezes, reopen repo truth before reopening exploratory work

## Resume instructions if the thread freezes

1. Open this file first.
2. Confirm branch and working tree state.
3. Confirm `AGENTS.md` is still the only unrelated dirty file.
4. Close stale completed sidecars before resuming implementation.
5. For Meta proof visual-fidelity work, verify that `Build-MetaVideoPlayerProofCua.ps1` still points at `GhostMetaVideoPlayerProofCustomUnityAssetExporter.ExportMetaVideoPlayerProofBatch` in `ghost_training_export_clone`.
6. Run `Build-MetaVideoPlayerProofVolodeckWitness.ps1` and inspect both generated images before making any quality claims.
7. Use `PLAYER_VOLODECK_VISUAL_AND_INTERACTION_GUARDRAILS_V1.md` to judge whether the witness is actually informative.
8. Syntax-check `Build-PlayerMetaUiPacket15Foundation.ps1`.
9. Inspect the successful narrow proof receipt above before changing inputs.
10. Run the wrapper with a narrow shell set first, preferably `modern_tv`.
11. Inspect the emitted receipt under `build/meta_ui_packet_1_5_runs`.
12. Inspect the bundle header class before calling any hosted-player artifact
    VaM-valid:
    - `2022.3.62f3` means Volodeck/package witness only
    - `2018.1.9f2` means candidate VaM-valid raw shell seam
13. Do not send the current `fa_cua_player_modern_tv_v1.assetbundle` hosted
    proof back to the operator until the host shell export has been switched to
    the 2018-compatible raw seam.
14. Treat the search-bar and grid-menu proofs as snapshot witnesses only until
    they are rebuilt from a true interactive carrier.
15. Only after the narrow proof is trustworthy, widen the shell set.

## Recommended first proof command

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerMetaUiPacket15Foundation.ps1 -RepoRoot C:\projects\fa -ShellKeys modern_tv -NoPreview
```

## Recommended first interactive proof command

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-MetaInteractiveHostedPlayerProof.ps1 -RepoRoot C:\projects\fa -ShellKey modern_tv -PlayerPluginMode raw
```

## Next implementation targets after the first proof

1. verify the wrapper receipt and host catalog outputs
2. note that the current shell export wrapper still emits the whole shell family before host catalog filtering; decide whether to keep that as-is or narrow it later
3. remove or further quarantine remaining legacy bridge repair strings if they still matter
4. decide whether to keep `theme_00` as only a fixture in the defaults file or widen Packet `1.5` into multiple toolkit theme/profile rows
5. decide whether the next Meta UI proof surface after video player should be search bar or grid menu
6. update the version/capability schedule row for `v1.5` once the actual toolkit feature set is chosen
7. if needed, build a deterministic VaM witness scene for the standalone Meta proof set so operator testing does not start from manual preset loading every time
