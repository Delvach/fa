# Player Remote Recovery Map

## Purpose

This file records the bounded remote git seams that are worth mining for player
feature recovery.

Use it when local repo archaeology is noisy and the operator has pointed at the
GitHub repos as a cleaner source of exact historical solutions.

## Repos checked

1. `https://github.com/Delvach/vam-plugin-suite`
2. `https://github.com/Delvach/vam_slideshow`

## Current answer

For the phase-1 player recovery target, use the two remote repos for different
jobs:

1. `vam_slideshow` is the original prototype seam
2. `vam-plugin-suite` is the later stabilization seam

Working rule:

1. recover the original control and transport shape from `vam_slideshow`
2. recover later bug fixes and hardening from `vam-plugin-suite`
3. do not treat local recovery worktree piles as authority when they only repeat drifted variants

Operator correction:

1. the local recovery tree under `G:\recovery\C-drive\projects\10-products\vam\...` did not help
2. directories like `vam-plugin-suite_variant_*`, `vam-plugin-suite_safe_restore`, `vam-plugin-suite-46c18f-clean`, and related recovery worktrees are not the primary truth surface for this recovery

## Prototype baseline from `vam_slideshow`

The original prototype repo still carries the cleanest baseline statement of the
player surface before the later recovery churn.

Useful prototype files:

1. `VaM_ImagePanelEmissive_Slideshow_Canonical_Spec_v1.txt`
2. `plugins/slideshow/src/BaseMediaPanelPlugin.Voice.cs`
3. `plugins/slideshow/src/BaseMediaPanelPlugin.Orchestration.cs`
4. `plugins/slideshow/src/BaseMediaPanelPlugin.FileList.cs`
5. `plugins/slideshow/src/MediaPlaylist.cs`
6. `plugins/slideshow/src/ScrubProfile.cs`
7. `plugins/slideshow/src/VideoSlideshowPoCPlugin.cs`
8. `plugins/slideshow/src/VideoSlideshowPoCPlugin.Runtime.cs`
9. `plugins/slideshow/src/VideoSlideshowPoCPlugin.RuntimeRig.cs`
10. `docs/controls/SLIDESHOW_VIDEO_PANEL_CONTROLS.md`
11. `docs/controls/SLIDESHOW_IMAGE_VIDEO_CORE_MOCKUPS_V1.md`
12. `docs/controls/PV_MEDIA_PANEL_TRANSPORT_RUNTIME_MAPPING.md`

What the prototype baseline already proves:

1. shared core transport of `Load`, `Previous`, `Play/Pause`, and `Next`
2. playlist or filelist as a bounded panel concern
3. video extras for seek skip, loop mode, A-B loop, aspect correction, volume, and mute
4. a concrete `MediaPlaylist` implementation with current item and move next or previous semantics
5. a `ScrubProfile` seam for banded joystick scrub rates
6. video end handling that distinguishes `none`, `single`, and playlist-style continuation

Important nuance:

1. the prototype baseline is useful for intended behavior and control shape
2. some prototype implementations are not yet the corrected versions we want
3. example: prototype `MediaPlaylist.MoveNext` and `MovePrevious` always return `true`, while the later stabilization seam fixes truthful edge behavior

## Prototype behavior notes worth preserving

These are the parts of the prototype that are still useful as behavior canon,
not as file-layout canon.

1. load behavior is exact-file-first:
   - the media dialog callback resolves the selected file
   - then loads the containing folder
   - then sets playlist selection by exact normalized path
2. playlist and file-browser behavior are real, not mock-only:
   - secure folder enumeration
   - cached folder file list
   - filtered and paged file browsing
   - exact file selection into the active playlist
3. manual next or previous navigation pauses first when appropriate, then moves the playlist, then reapplies the current media
4. base scrub is banded:
   - deadzone `0.06`
   - rate bands roughly `0.5 / 1 / 2 / 3 / 4` steps per second
   - immediate first step on engage or direction flip
5. video scrub is a separate seam from playlist stepping:
   - direct seek when the seek surface exists
   - this is exactly why later scrub-versus-next/previous repairs matter
6. prototype loop behavior already distinguished:
   - `none`
   - `single`
   - playlist continuation
7. prototype aspect behavior is narrower than the later player:
   - anamorphic correction pulse
   - `Fix Aspect`
   - not yet the later `fit`, `crop`, or `full_width` release contract
8. prototype resize is only a panel-scale nudge seam and should not be treated as the final screen-authoritative resize model

## Best later stabilization commits from `vam-plugin-suite`

### Playlist and play-order stability

1. `ab3097bea70e40e7250283ab94d347ebb966549d`
   - date: `2026-03-16 13:17 -0600`
   - title: `DEV-74 fix random/play-order reorder without reapplying current media`
   - repo path:
     - `plugins/frameangel_runtime/src/BaseMediaPanelPlugin.Orchestration.cs`
   - why it matters:
     - preserves current media while random or order changes
     - avoids fake pause or reload behavior when toggling random

2. `d64a07f3c7f3c49ed107a1763d0fec6b4ff09877`
   - date: `2026-03-16 15:33 -0600`
   - title: `DEV-76 fix loop-state feedback and truthful playlist step movement`
   - repo paths:
     - `plugins/frameangel_runtime/src/MediaPlaylist.cs`
     - `plugins/frameangel_runtime/src/VrFocusedOverlayBoard.cs`
   - why it matters:
     - next or previous should only report success when the playlist index truly changes
     - loop feedback should match actual enabled loop state

3. `b8a0b0208b4d1a3d906449a0753aa6a7c349560b`
   - date: `2026-03-18 13:53 -0600`
   - title: `DEV-77 restore end of media loop continuation`
   - repo path:
     - `plugins/fa_player/src/MediaPlayerPlugin.Runtime.cs`
   - why it matters:
     - restores correct behavior after video end for single-loop and playlist-loop continuation

### Scrub and seek integrity

4. `343c756f5bd5c09fc1cbeed16be6e6f2bad36581`
   - date: `2026-03-16 19:45 -0600`
   - title: `DEV-80 fix focused video scrub routing and stop failed seek from triggering playlist next/prev`
   - repo paths:
     - `plugins/fa_player/src/MediaPlayerPlugin.Focused.cs`
     - `plugins/fa_player/src/MediaPlayerPlugin.cs`
   - why it matters:
     - exact historical proof that scrub and playlist transport were incorrectly collapsing into each other
     - the fix explicitly stops failed seek from impersonating next or previous

5. `5ed253aef26b08e2b15c962e0c8c0cbaee2333cf`
   - date: `2026-03-16 19:21 -0600`
   - title: `DEV-80 restore older focused scrub mapping and remove shared-resolver transport detour`
   - repo paths:
     - likely same focused player runtime family as the later DEV-80 fixes
   - why it matters:
     - useful predecessor rung for the scrub repair ladder

6. `538e7fb7692136bba60498208275486b03ce9738`
   - date: `2026-03-16 20:58 -0600`
   - title: `DEV-80 guard focused scrub against transport fallback and vertical cross-axis bleed`
   - why it matters:
     - likely hardens the same scrub routing seam after the initial repair

### Resize and bottom-anchor stability

7. `51e5bc50649ae7698732c301ec6f87ebb53d5cd5`
   - date: `2026-03-16 15:47 -0600`
   - title: `DEV-85 add joystick-left screen resize with stable aspect and bottom anchor`
   - repo path:
     - `plugins/fa_player/src/MediaPlayerPlugin.Focused.cs`
   - why it matters:
     - screen-targeted resize
     - preserve aspect during resize
     - restore bottom anchor after each resize step

8. `b45aec32d00dd42aae885748ea76c5b9472a80cb`
   - date: `2026-03-16 19:52 -0600`
   - title: `DEV-89 preserve fixed display height across media swaps and restore bottom anchor from that reference`
   - repo paths:
     - `plugins/fa_player/src/MediaPlayerPlugin.cs`
     - `plugins/fa_player/src/MediaPlayerPlugin.Focused.cs`
   - why it matters:
     - exact historical answer for cumulative media-swap height drift
     - preserve the pre-load display height
     - reapply new aspect against that fixed height
     - restore bottom anchor after the fixed-height correction

### Load and transport readiness

9. `164338a73bd51dfeed55b81f1cb806ce44e9689c`
   - date: `2026-03-18 13:48 -0600`
   - title: `DEV-75 gate prev next overlay state on resolved media`
   - repo path:
     - `plugins/fa_player/src/MediaPlayerPlugin.Runtime.cs`
   - why it matters:
     - transport controls should not look active before media is actually resolved

10. `25986749bdb864b73238e31f3b997de48bbdbcac`
    - date: `2026-03-19 00:30 -0600`
    - title: `DEV-99 preposition native load HUD before dialog open`
    - repo path:
      - `plugins/frameangel_runtime/src/BaseMediaPanelPlugin.Voice.cs`
    - why it matters:
      - this is not the first phase-1 authority seam
      - it is still useful as a historical note for the native load browser path

## How prototype plus stabilization map into `fa`

The current `fa` runtime already has the target seams, but not yet the full
historical stability:

1. current player transport and playlist state live in:
   - `C:\projects\fa\products\vam\plugins\player\src\scene-runtime\FASyncRuntime.Player.cs`
2. attached-player exposed method surface lives in:
   - `C:\projects\fa\products\vam\plugins\player\src\scene-runtime\FASyncRuntime.PlayerPluginSurface.cs`
3. hosted player record and control-surface contract live in:
   - `C:\projects\fa\products\vam\plugins\player\src\scene-runtime\FASyncRuntime.PlayerHosted.cs`
4. current control-surface action translation lives in:
   - `C:\projects\fa\products\vam\plugins\player\src\scene-runtime\FASyncRuntime.Player.ControlSurface.cs`
5. current resize and anchor authority live in:
   - `C:\projects\fa\products\vam\plugins\player\src\scene-runtime\FASyncRuntime.Player.StandaloneResize.cs`
6. current aspect normalization and release-facing aspect vocabulary live in:
   - `C:\projects\fa\products\vam\plugins\player\src\shared-runtime\FrameAngelPlayerMediaParity.cs`

Best mapping rule:

1. prototype transport and control semantics should come from `vam_slideshow`
2. playlist, loop, random, next, previous, and load-history stability should be informed by the later `vam-plugin-suite` fixes
3. scrub fixes should land in the exposed player action path and the runtime seek logic, not in a hidden harness path
4. resize and bottom-anchor fixes should land in the player screen authority seam, not in Meta-only control code

## Working import rule

When recovering from these remote commits:

1. import the smallest exact behavior needed
2. do not import old overlay, voice, harp, or session scaffolding unless the player feature truly depends on it
3. keep commit intent, root cause, and touched-file scope in the handoff
4. prefer translating the behavior into the current `fa` player architecture rather than trying to recreate the old file layout
5. treat `vam_slideshow` as the prototype reference and `vam-plugin-suite` as the stabilization reference

## Current recommendation

If we continue feature recovery in order, the best next remote seams are:

1. `vam_slideshow` prototype files for baseline transport, playlist, load, scrub-profile, and loop semantics
2. `343c756f5bd5c09fc1cbeed16be6e6f2bad36581` for scrub versus next or previous integrity
3. `ab3097bea70e40e7250283ab94d347ebb966549d` and `d64a07f3c7f3c49ed107a1763d0fec6b4ff09877` for random, loop, and truthful playlist movement
4. `b8a0b0208b4d1a3d906449a0753aa6a7c349560b` for natural end continuation
5. `51e5bc50649ae7698732c301ec6f87ebb53d5cd5`, `f0f98a7a4988037b8561e4cdf00708502837928a`, and `b45aec32d00dd42aae885748ea76c5b9472a80cb` for resize and bottom-anchor closure across media swaps
