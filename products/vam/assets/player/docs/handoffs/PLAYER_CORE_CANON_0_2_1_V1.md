# Player Core Canon 0.2.1

## Current answer

Version `0.2.1` is the first clean-room milestone where the base player should
be treated as restored canon.

The canonical phase-1 product seam is:

1. one authored CUA screen assetbundle
2. one matching `fa_cua_player.<version>.dll`
3. existing VaM buttons and sliders calling the exposed player methods
4. supported media loaded and controlled through that same base player surface

This canon is promoted from live runtime proof, not from historical docs alone.

## What "meant to work" means now

After the `2026-04-09` runtime witness:

1. aspect should be treated as correct for the base screen
2. native VaM resizing should be treated as the intended base answer
3. the repo no longer needs a custom resize solution for the base player lane
4. existing VaM controls are sufficient for phase 1
5. projected custom controls remain a valid future surface, but they are not
   required to define or prove the base player

## Witness authority

Use these as the current authority chain for the restored base player:

1. `PLAYER_MANUAL_TEST_CHECKPOINT_2026_04_09_0115_V1.md`
2. `PLAYER_FIRST_RELEASE_BOUNDARY_V1.md`
3. `PLAYER_TIER_BOUNDARY_AND_WITNESS_V1.md`

The operator-supplied screenshots for this milestone are:

1. `F:\sim\vam\Saves\screenshots\1775718939.jpg`
2. `F:\sim\vam\Saves\screenshots\1775718956.jpg`
3. `F:\sim\vam\Saves\screenshots\1775719226.jpg`

## Explicit non-requirements for phase 1

The restored base-player canon does not require:

1. Meta UI control surfaces
2. projected custom control buttons
3. custom resize logic
4. Harp, Halo, File Browser, or joystick-focus upgrades

Those may still matter as separate product lanes, but they are not part of the
phase-1 definition now that the base player has working runtime proof.

## Known post-canon bugs

These are real issues, but they do not cancel the milestone:

1. loop-state behavior is not yet truthful and can behave like `single`
2. the front display can detach and drift away from the rear backing
3. detachment can become more visible as `next` is triggered
4. when detached, front-facing see-through artifacts can appear while the rear
   object still occludes from behind
5. the preferred future presentation is a rear object that matches the front
   display size

## Working rule for future recovery

Start from this canon and narrow bugs from here.

Do not reopen the whole player definition unless live runtime proof shows that
the restored base seam itself has regressed.
