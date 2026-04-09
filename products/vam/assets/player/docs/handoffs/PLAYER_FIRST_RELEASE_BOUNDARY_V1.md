# Player First Release Boundary

## Current live canon milestone

The current file-backed answer after the `2026-04-09` runtime proof is:

1. `PLAYER_CORE_CANON_0_2_1_V1.md`
2. `PLAYER_MANUAL_TEST_CHECKPOINT_2026_04_09_0115_V1.md`

That milestone promotes the restored clean-room player seam to canon and locks
in two important answers:

1. aspect is now good enough to treat as solved for the base screen
2. native VaM resizing and existing VaM buttons are sufficient for phase 1

## Current answer

The first usable release for this repo is:

1. one authored CUA screen assetbundle
2. one matching `fa_cua_player.<version>.dll`
3. player features exercised through exposed player methods
4. VaM buttons and sliders as the first in-game control surface
5. the base `FAP` player lane, not the Meta toolkit lane and not Harp/File Browser upgrade lanes

## What counts as first-release success

Success for phase 1 is:

1. the authored screen loads
2. the matching plugin attaches
3. media can be loaded and controlled through the exposed player method surface
4. the same method surface can be driven from a deterministic default-scene or Volodeck witness setup
5. aspect remains correct under the current base-screen presentation seam
6. native VaM resizing remains acceptable for the base player lane

Future control approach, not phase 1:

1. custom buttons projected into the scene
2. those projected controls should still call the same exposed player methods
3. they do not replace the simpler phase-1 VaM button-and-slider baseline
4. see `PLAYER_HISTORICAL_FEATURE_WITNESS_V1.md` for the separate historical feature proof

## Witness anchors

Direct screenshot witnesses supplied by the operator:

1. `F:\sim\vam\Saves\screenshots\1772953314.jpg` on `2026-03-08 00:01:54 -07:00`
2. `F:\sim\vam\Saves\screenshots\1772999983.jpg` on `2026-03-08 13:59:43 -06:00`
3. `F:\sim\vam\Saves\screenshots\1773008593.jpg` on `2026-03-08 16:23:13 -06:00`

Important interpretation:

1. those screenshoted controls were custom projected controls in the scene
2. they are valid witness proof that richer player features already existed in-game
3. they do not define the phase-1 control layout
4. they do not change the phase-1 baseline, which remains VaM buttons and sliders calling exposed player methods
5. the feature-level interpretation lives in `PLAYER_HISTORICAL_FEATURE_WITNESS_V1.md`

Historical deterministic-scene witness, not yet imported into this repo:

1. `C:\projects\frameangel\products\vam\assets\player\build\scene_builds\0.0.94\player_demo_scene_build_receipt.md`
2. that receipt records a button-mapped scene built from `F:\sim\vam\Saves\scene\buttons_setup_scene.json` with explicit mappings for aspect buttons, previous/next, play/pause, seek/skip, volume buttons, resize buttons, scrub slider, and volume slider
3. that receipt is the stronger historical shape for phase-1 witness style than the richer projected scene controls

## What does not count as first-release authority

These are explicitly out of scope for phase 1:

1. Meta UI control surfaces
2. hidden harness-only spawn, bind, layout, or bootstrap helpers
3. preset-first bootstraps
4. asset-side plugin copies
5. gaze-on-screen focus capture and gaze-off-screen focus release as a product requirement
6. focused joystick capture for scrubbing videos and stepping images
7. custom file browser integration
8. treating fully-correct resize behavior as a first-release gate

Those may be real and previously working, but they are upgrade-tier or later-lane features, not first-release authority for the base player.

Resize needs special wording:

1. resize existed historically in the player lane
2. native resize now works well enough to treat it as canonical for the base screen
3. the repo no longer needs a custom resize solution before the first clean player release
4. deeper shell-specific resize behavior can remain outside the base milestone

## Phase ordering

Phase order is:

1. screen plus VaM controls first
2. deterministic witness scene proving the same exposed methods second
3. Meta UI component integration after the first release is stable

## Resource progression

Keep the authored resource family in this order:

1. basic screen
2. Meta screen
3. laptop shell
4. phone shell with swipe
5. tablet shell with swipe
6. TV shells, including retro and modern

Only the basic screen belongs to first-release authority by default.

## Repo boundary

The minimal release lane for this phase lives under:

1. `C:\projects\fa\products\vam\assets\player`
2. `C:\projects\fa\products\vam\plugins\player`

Do not widen this boundary by treating Meta UI integration as part of the first release.
