# Player Manual Test Checkpoint 2026-04-08 21:42

## Context

Operator provided a direct in-VaM manual witness from `2026-04-08 9:42 PM`
local time.

This is a stronger phase-1 witness than a prebuilt scene because it exercises
the actual empty-scene attach flow:

1. load empty scene
2. add CUA
3. assign `fa_player_asset.0.0.111.assetbundle`
4. choose `assets/frameangel/playerscreencore2018/fa_player_screen.core.prefab`
5. add plugin
6. assign `fa_cua_player.0.0.111.dll`
7. use the plugin UI directly

## Passes

Confirmed working in this run:

1. authored direct-CUA screen can be added manually to an empty scene
2. plugin can be attached manually to the CUA host
3. `Player Load Media` can select a movie
4. selected movie appears on the screen
5. play and pause work
6. seek start works
7. seek reference works
8. skip forward works
9. skip backward works
10. natural end loops when loop is on
11. natural end stops looping when loop is turned off

## Failures Or Suspect Seams

### Visual presentation

Observed:

1. before media load the screen reads as a black rectangle
2. the rear authored polygon reacts to ambient light
3. after load a third visible surface exists
4. the movie reads backwards from the operator-facing front view
5. the movie fills width but appears vertically squished

Current interpretation:

1. this is a real screen-core presentation issue
2. likely seam is the `runtime_overlay_quad` path on the authored
   `screen_surface`
3. do not flatten this into a generic “player broken” claim; it is a bounded
   front-face, overlay, and aspect presentation problem

### Transport

Observed:

1. `Next` does not work
2. `Previous` does not work

Current interpretation:

1. this is either:
   - a real playlist-step bug
   - or a truthful `no_change` result from a single-item resolved playlist
2. verify with playlist count or a folder containing multiple supported files
   before promoting this as a hard transport regression

### Paused seek frame

Observed:

1. if seek is performed while paused, the visible screen keeps the old paused
   frame until playback resumes from the new section

Current interpretation:

1. this was a real runtime bug
2. code already refreshed paused frames on skip but not on seek
3. the current `fa` repo has been patched so seek now calls the same paused
   frame refresh helper

## Screens

Operator attached:

1. side-view witness showing layered screen surfaces
2. plugin UI witness showing `runtime_overlay_quad`
3. second side-view witness after load
4. loop-off end-state witness

## Working rule after this checkpoint

Use this run as the current phase-1 authority witness for:

1. manual empty-scene attach works
2. manual load-to-play works
3. seek and skip are live

Do not use it as proof that:

1. screen-core presentation is visually correct
2. playlist step behavior is fully correct
3. paused seek rendering is still broken after the post-checkpoint patch
