# Player Historical Feature Witness

## Current answer

Keep historical feature proof separate from historical control-surface proof.

The important recovered claim is not that one exact button layout from March 2026
is the phase-1 baseline. The important recovered claim is that several core
player features had already been working in the lane and were visible through
real in-game witnesses.

## Operator correction

Operator-stated correction:

1. the March 2026 screenshots do not define the phase-1 VaM button layout
2. those controls were custom buttons projected into the scene
3. phase 1 remains VaM buttons and sliders calling exposed player methods
4. the preserved historical value from that period is the feature set, not the exact projected control surface

## Confirmed historical feature witnesses

Direct screenshot anchors from `2026-03-08`:

1. `F:\sim\vam\Saves\screenshots\1772953314.jpg`
2. `F:\sim\vam\Saves\screenshots\1772999983.jpg`
3. `F:\sim\vam\Saves\screenshots\1773008593.jpg`

What those witnesses prove about the player lane:

1. media loading was live
2. playback state and timer display were live
3. previous and next were live
4. seek and scrub behavior were live enough to be shown as operator-facing controls
5. aspect control was live
6. playlist-style directory or path playback was live enough to show current media path context
7. mute was live
8. loop and random had visible projected controls by that date

What those witnesses do not prove by themselves:

1. the exact phase-1 VaM button layout
2. truthful playlist edge behavior
3. scrub-versus-next fallback integrity
4. final resize correctness

Important interpretation:

1. these screenshots are feature witnesses, not authority for the phase-1 control layout
2. they prove the player line had real playlist and transport behavior in-game
3. they do not mean the projected control surface itself should be treated as the first release surface

## Deterministic scene witness

Historical deterministic-scene receipt:

1. `C:\projects\frameangel\products\vam\assets\player\build\scene_builds\0.0.94\player_demo_scene_build_receipt.md`

What that receipt proves:

1. a bounded VaM button and slider witness seam existed for the player lane
2. explicit mappings existed for load, previous, next, play or pause, seek, skip, scrub, volume, aspect, and resize

Important interpretation:

1. this receipt is the right historical shape for phase-1 witness style
2. it proves a deterministic scene harness existed without requiring the projected custom controls to be the authority surface

## Resize status

Resize needs stricter wording than the other feature witnesses.

Current honest wording:

1. resize definitely existed in the lane
2. resize had deterministic button mappings by `0.0.94`
3. resize had active historical substrate around bottom-anchor and bottom-center behavior
4. resize was not fully settled and should not be treated as already-correct product proof

Working rule:

1. keep resize in the recovery target
2. do not make "fully solved resize" a prerequisite for the first clean player recovery
3. treat it as partially recovered historical capability that still needs closure

Remote git support for the same claim now lives in:

1. `PLAYER_REMOTE_RECOVERY_MAP_V1.md`

Remote git support adds a more exact feature reading:

1. `vam_slideshow` is the prototype proof that load, folder playlist selection, transport, scrub bands, and loop modes existed as real behavior
2. `vam-plugin-suite` is the later proof that truthful playlist stepping, scrub integrity, natural-end continuation, and bottom-anchor resize closure each had specific fixes

## Practical recovery rule

For the clean `fa` repo:

1. recover the player features first through exposed methods
2. keep the phase-1 in-game surface as VaM buttons and sliders
3. use historical screenshot witnesses to remember which player capabilities already existed
4. do not let projected-scene controls redefine the first-release authority seam
5. treat `vam_slideshow` as the original prototype reference
6. treat `vam-plugin-suite` as the later stabilization reference
7. do not treat the local recovery variant folders under `G:\recovery\...` as the primary authority surface
