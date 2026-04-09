# Player Tier Boundary And Witness

## Current live canon milestone

The current runtime-backed canon is recorded in:

1. `PLAYER_CORE_CANON_0_2_1_V1.md`
2. `PLAYER_MANUAL_TEST_CHECKPOINT_2026_04_09_0115_V1.md`

Those files lock in the post-recovery answer that:

1. the clean-room base player is restored
2. aspect is correct in the current base-screen lane
3. native resizing is sufficient for the base screen
4. existing VaM buttons remain the correct phase-1 control surface

## Canon

Operator-stated current product truth:

1. the player moved from a projected scene or session screen toward a real CUA assetbundle screen plus plugin split
2. that split matters for distribution control and IP posture because the product is intended to ship as a freemium surface with tiered features
3. the base player lane is the `FAP` player lane
4. the first authority seam is the screen assetbundle plus matching player plugin, with VaM-exposed methods and VaM-driven controls

## Base player authority

Base player authority is:

1. one authored screen assetbundle
2. one matching `fa_cua_player.<version>.dll`
3. exposed player methods for in-game control
4. deterministic VaM buttons and sliders that call those exposed methods
5. native VaM resizing on the authored screen instead of a custom resize subsystem

Future control approach:

1. custom buttons projected into the scene
2. those projected controls should target the same exposed player methods
3. they are a richer surface, not a replacement definition for phase 1
4. historical feature proof for that richer surface is tracked separately in `PLAYER_HISTORICAL_FEATURE_WITNESS_V1.md`

## Separate authored surfaces

Operator correction:

1. these are different authored surfaces, not one simple progression ladder
2. they may share the same player core, but they are not interchangeable and should not be collapsed into one product seam

Current separated surface families:

1. basic screen
2. Meta screen
3. laptop shell with the screen
4. phone shell with swipe behavior
5. tablet shell with swipe behavior
6. TV surfaces, including retro and modern

Working rule:

1. keep the player core separable from the authored surface family
2. keep shell-specific behavior, like swipe on phone and tablet, out of the base screen authority seam
3. do not treat Meta screen, laptop, phone, tablet, and TV as mere cosmetic skins of one identical resource
4. do not treat the rear backing-object mismatch as proof that the base player is not restored; it is a bounded post-canon visual bug

## Upgrade and adjacent lanes

The following are not base-player authority:

1. Meta UI control surfaces
2. gaze-on-screen focus capture
3. gaze-off-screen focus release
4. focus disabling joystick navigation
5. focus capturing joystick movement for video scrubbing and image stepping
6. custom file browser integration

These may already have existed and may already have been fully working, but they belong to upgrade or adjacent product lanes rather than the base first-release player authority.

## Witnesses

Direct screenshot witnesses from `2026-03-08`:

1. `F:\sim\vam\Saves\screenshots\1772953314.jpg`
2. `F:\sim\vam\Saves\screenshots\1772999983.jpg`
3. `F:\sim\vam\Saves\screenshots\1773008593.jpg`

What these prove:

1. the player screen existed as a real in-game surface
2. a richer projected control surface was already wired to the player lane
3. the important recovery value from those screenshots is feature proof, not control-layout proof
4. playlist, load, play or pause, next, previous, seek or scrub, aspect, and mute had visible operator proof
5. loop and random were visible on the projected surface by that date
6. resize belongs in the same historical lane, but should be treated as partially solved rather than already-closed product proof

Historical deterministic scene witness:

1. `C:\projects\frameangel\products\vam\assets\player\build\scene_builds\0.0.94\player_demo_scene_build_receipt.md`

That historical receipt shows a deterministic button and slider scene already existed for the player seam, with mapped controls for:

1. aspect buttons
2. load
3. previous and next
4. play or pause
5. seek and skip
6. volume buttons
7. scrub and volume sliders
8. resize up and resize down

Historical resource-production note:

1. the operator has an existing Unity training line that can generate most of these separate screen and shell resources
2. that means resource production should be treated as a bounded authoring lane, not as justification to re-mix authored surfaces, Meta UI, and player runtime logic into one compile surface

## Working rule for this repo

Use this boundary when deciding what to rebuild first:

1. recover the base `FAP` player lane first
2. keep the witness seam aligned with exposed player methods
3. do not let Harp, Halo, Meta UI, or File Browser features silently become required for first-release success
4. treat projected-scene controls as historical witness only
5. treat playlist, load, scrub, and partial resize as historical feature targets worth recovering without importing the projected control surface as canon
