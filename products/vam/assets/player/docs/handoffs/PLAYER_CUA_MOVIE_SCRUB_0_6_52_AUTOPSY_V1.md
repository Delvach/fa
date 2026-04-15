# CUA movie scrub 0.6.52 autopsy

Updated: `2026-04-14`

## Purpose

This note records the exact process failure behind the `0.6.52` movie scrub regression so future threads do not repeat it.

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

Specifically, `0.6.52` removed the movie scrub session lifecycle that `0.6.51` used:

- begin scrub session
- accumulate scrub target continuously
- finalize seek on release
- optionally resume playback

It then rewrote the non-trigger movie stick path to discrete skip actions. That changed operator-visible semantics, including trigger plus joystick behavior, instead of preserving the established scrub model.

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
