# Player Manual Test Checkpoint 2026-04-09 01:15

## Context

Operator provided a direct in-VaM runtime witness after the clean-room player
recovered still-image loading, aspect correction, playlist step behavior, and
the current screen-core presentation seam.

This proof was gathered on the working `0.1.6` runtime and immediately promoted
to the `0.2.1` milestone line so the restored baseline would be recorded as
canon instead of left inside thread memory.

Screens supplied by the operator:

1. `F:\sim\vam\Saves\screenshots\1775718939.jpg` on `2026-04-09 01:15:39 -06:00`
2. `F:\sim\vam\Saves\screenshots\1775718956.jpg` on `2026-04-09 01:15:56 -06:00`
3. `F:\sim\vam\Saves\screenshots\1775719226.jpg` on `2026-04-09 01:20:26 -06:00`

## Passes

Confirmed by operator in this run:

1. core functionality is restored in the hosted CUA player lane
2. aspect behaves correctly enough to treat the current presentation as the
   intended base-screen behavior
3. native VaM resizing works and removes the need for a custom resize solution
   in the base player lane
4. existing VaM buttons and sliders are sufficient for phase 1
5. `Next` works across a real media sequence
6. the actual front display size is correct; the black bars behind it are a
   separate rear object rather than proof that the front display is still sized
   wrong

## Canon implications

This run upgrades the current answer for first-release authority:

1. the basic authored CUA screen plus matching `fa_cua_player.<version>.dll`
   split is now treated as restored canon for the base player
2. phase 1 does not require projected custom controls; existing VaM controls are
   acceptable and should be preferred as the baseline
3. phase 1 does not require a custom resize system; native resizing is now the
   canonical answer for the base screen
4. aspect should be treated as solved for the current base-screen lane unless a
   new direct runtime regression appears

## Failures Or Suspect Seams

The operator still reported bounded follow-on issues:

1. `loop` behaves as `single` regardless of setting, even though `next` works
2. the front display can detach and drift forward away from the rear backing
3. that detachment can be observed as `next` is triggered
4. once detached, front-facing see-through artifacts can appear while the rear
   object still blocks from behind
5. desired future visual behavior is a rear object that matches the front
   display size if that can be accomplished cleanly

## Screens

Operator attached:

1. side-view witness showing the detached front display and rear backing
2. second side-view witness showing the same separation from a straighter angle
3. close witness showing front-facing see-through artifacts after detachment

## Working rule after this checkpoint

Use this run as the file-backed proof that:

1. the clean-room player core is restored
2. aspect and native resize are good enough to stop being treated as open
   release blockers for the base screen
3. existing VaM controls are the correct phase-1 control answer
4. remaining issues should be treated as bounded post-milestone bugs, not as a
   reason to reopen the entire first-release definition
