# Player Trim Startup 0.3.0

## Current truth

Version `0.3.0` is the current clean-room repo tip for the player lane.

The strongest current-startup anchors are:

1. `C:\projects\fa\products\vam\assets\player\AGENTS.md`
2. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_CORE_CANON_0_2_1_V1.md`
3. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_MANUAL_TEST_CHECKPOINT_2026_04_09_0115_V1.md`
4. `C:\projects\fa\products\vam\assets\player\player.version.json`
5. `C:\projects\fa\products\vam\assets\player\changelog\0.3.0.json`
6. `C:\projects\fa\products\vam\assets\player\build\releases\player_screen_core_release_latest.json`

Use `0.2.1` canon as the baseline definition of the restored screen/player seam.
Use `0.3.0` as the current live repo truth for what has been built and deployed
since that canon was locked.

## What 0.3.0 adds

`0.3.0` starts the clean-room CUA input lane without reopening the restored
screen baseline.

Current `0.3.0` source of truth:

1. `C:\projects\fa\products\vam\plugins\player\src\cua-runtime\FASyncRuntime.CuaPlayerInputModule.cs`
2. `C:\projects\fa\products\vam\plugins\player\src\scene-runtime\FASyncRuntime.cs`
3. `C:\projects\fa\products\vam\plugins\player\vs\fa_cua_player\fa_cua_player.csproj`
4. `C:\projects\fa\products\vam\plugins\player\src\shared-runtime\BuildRuntimeInfo.cs`
5. `C:\projects\fa\products\vam\assets\player\changelog\0.3.0.json`

Operator-visible `0.3.0` intent is:

1. gaze on the screen can acquire focus
2. gaze off can release focus
3. focused player ownership can suppress native VaM navigation
4. focused joystick input can scrub video
5. focused joystick input can step still images through the existing previous/next seam

## Retrieval rule

For trim startup, use local repo truth first.

OpenMemory is still the first retrieval surface for historical seam recovery, but
in this lane it must be treated as historical substrate unless current repo code,
release receipts, or runtime proof agree.

Do not let older `C:\projects\frameangel` hits replace present-tense truth in
`C:\projects\fa`.

## Trim startup order

1. read `AGENTS.md`
2. read `PLAYER_CORE_CANON_0_2_1_V1.md`
3. read `PLAYER_MANUAL_TEST_CHECKPOINT_2026_04_09_0115_V1.md`
4. read `player.version.json`, `changelog\0.3.0.json`, and `player_screen_core_release_latest.json`
5. only widen into `PLAYER_FIRST_RELEASE_BOUNDARY_V1.md` or remote/shipping recovery docs if the next slice explicitly needs them

## Known continuity warts

1. baseline canon is still centered on `0.2.1`, so later closure lives mostly in per-version changelogs
2. `build/releases/0.2.13` exists as historical release output, but there is no tracked `changelog\0.2.13.json`
3. older OpenMemory answers may still point at deprecated monorepo substrate

## Working rule

For future handoffs:

1. treat baseline player/screen restoration as closed
2. treat `0.3.0` as the start of the bounded CUA input lane
3. keep canon, current repo truth, and historical substrate separate
