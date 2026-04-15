# CUA movie scrub 0.6.52 autopsy

Updated: `2026-04-14`

## Purpose

This note records the exact process failure behind the `0.6.52` movie scrub regression so future threads do not repeat it.

## Exact Boundary

The regression boundary is exact:

- `b82b4b2` = `0.6.51` sanity-check baseline
- `9eeafce` = `0.6.52` discrete-step rewrite
- `43f222d` = `0.6.53` rollback restoring the last safe behavior

Between `b82b4b2` and `9eeafce`, the only behavior file change was:

- [FASyncRuntime.CuaPlayerInputModule.cs](/C:/projects/fa/products/vam/plugins/player/src/cua-runtime/FASyncRuntime.CuaPlayerInputModule.cs)

The only other `.cs` change in that slice was the version stamp in:

- [BuildRuntimeInfo.cs](/C:/projects/fa/products/vam/plugins/player/src/shared-runtime/BuildRuntimeInfo.cs)

## What Held

The version and branch hygiene held. The repo carried a clean sequence of versioned rungs from `0.6.51` through `0.6.53`, with matching changelog and operator-log records for each step. That made the boundary easy to isolate and made the rollback path obvious.

Repo-local truth also held. The exact break could be traced to a single behavior file:

- [FASyncRuntime.CuaPlayerInputModule.cs](/C:/projects/fa/products/vam/plugins/player/src/cua-runtime/FASyncRuntime.CuaPlayerInputModule.cs)

The adjacent release notes were also consistent:

- [`0.6.51`](/C:/projects/fa/products/vam/assets/player/changelog/0.6.51.json) preserved the sanity-check rollback baseline
- [`0.6.52`](/C:/projects/fa/products/vam/assets/player/changelog/0.6.52.json) documented the discrete 15-second skip-step replacement
- [`0.6.53`](/C:/projects/fa/products/vam/assets/player/changelog/0.6.53.json) restored the original movie joystick scrub session behavior

## What Failed

`0.6.52` crossed the wrong boundary. The live symptom was scrub magnitude, but the fix replaced the interaction model instead of correcting the rate inside the existing model.

Specifically, `0.6.51` carried a full movie scrub session lifecycle:

- begin scrub session
- accumulate scrub target continuously
- finalize seek on release
- optionally resume playback

That lifecycle was implemented through the scrub-session fields plus:

- `BeginCuaPlayerVideoScrubSession(...)`
- `EndCuaPlayerVideoScrubSession(...)`

`0.6.52` deleted that lifecycle and rewrote the non-trigger movie stick path to discrete skip actions:

- `PlayerActionSkipForwardId`
- `PlayerActionSkipBackwardId`

That changed operator-visible semantics, including trigger plus joystick behavior, instead of preserving the established scrub model.

Two collateral changes in the same file also mattered:

- the still-image branch stopped calling `EndCuaPlayerVideoScrubSession(false)`
- focus release stopped calling `EndCuaPlayerVideoScrubSession(true)`

The status text also drifted from reality. The mode string still described the path as scrub after the code had become step transport, which made the live behavior harder to reason about.

## Canon Lesson

When the bug is about scrub rate, distance, or step size, do not swap the transport primitive. Keep the existing interaction model stable and fix the magnitude inside that model.

In practice:

- do not replace analog scrub with discrete skip transport just because the current scrub feel is wrong
- do not remove scrub-session lifecycle if the user-visible contract is still scrub
- do not let status text claim scrub if the path has become step transport
- if a model change is truly intended, treat it as a separate reviewed change with its own expectation and rollback boundary

## Recovery Truth

The repo now records `0.6.53` as the rollback of the `0.6.52` drift and the restored last-safe behavior for movie joystick scrub. The next scrub fix, if any, must stay inside the restored session model rather than layering a new one on top.

In concrete terms, `0.6.53` restored:

- the scrub-session state fields
- `BeginCuaPlayerVideoScrubSession(...)`
- `EndCuaPlayerVideoScrubSession(...)`
- analog target accumulation inside `TickCuaPlayerVideoScrubInput(...)`
- the still-image and focus-release calls that finalize or resume the active scrub session cleanly
