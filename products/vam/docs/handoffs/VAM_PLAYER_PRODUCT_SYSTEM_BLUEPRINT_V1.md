# VaM Player Product System Blueprint V1

## Purpose

This note replaces ad hoc thread reasoning with a product-system blueprint for
the next player line.

The current player is now treated as:

1. a stable prototype and feature witness
2. not the final architecture for the full product family

The next lane should be built so development becomes composition rather than
reinvention: LEGO pieces with a blueprint.

For the durable scaling and scheduling model behind that composition, use:

1. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`
2. `products/vam/config/player_version_capability_schedule.v1.json`

## Recommendation

Do this in the current repo.

Do not start a fresh repo yet.

### Why staying in `C:\projects\fa` is the right move now

The repo already has the most important hard-won seams:

1. a tested player baseline on `main`
2. a real runtime split across:
   - `products/vam/plugins/player/src/cua-runtime`
   - `products/vam/plugins/player/src/scene-runtime`
   - `products/vam/plugins/player/src/shared-runtime`
   - `products/vam/plugins/player/src/innerpiece-core`
3. existing compile-time feature gates in:
   - `products/vam/plugins/player/vs/fa_cua_player/fa_cua_player.csproj`
4. real shell/export/build scripts in:
   - `products/vam/assets/player/scripts/Export-GhostPlayerHostCuaFamily.ps1`
   - `products/vam/assets/player/scripts/Export-GhostPlayerHostCatalog.ps1`
   - `products/vam/assets/player/scripts/Build-CuaPlayerHostPackage.ps1`
   - `products/vam/assets/player/scripts/Build-CuaPlayerHostCatalog.ps1`
5. a deterministic package-first player build lane in:
   - `products/vam/assets/player/scripts/Build-PlayerScreenCoreFoundation.ps1`
   - `products/vam/assets/player/scripts/Build-CuaPlayerVarPackage.ps1`
6. a complete changelog and handoff trail for feature classification

### When a fresh repo would make sense later

A fresh repo becomes reasonable only after the first public product family is
shipping and the architecture is proven.

That later extraction would be for cleanup, not for discovery.

The current contamination risks are real but not severe enough to justify a
restart before release:

1. recovery Unity content under `products/vam/assets/player/unity/ghost_training_export_clone`
2. hybrid shell/meta-toolkit recovery scripts
3. old recovery references inside some handoff docs

Those are manageable if the new lane is explicitly separated inside this repo.

## Governing Rule

Do not keep extending the current prototype line for product differentiation.

Use the current player as:

1. a stable reference implementation
2. a feature witness
3. a source of reusable core seams

The new product system must be built from shared modules and declarative
product profiles.

## Architecture Model

The correct model is not fifteen independent code products.

The correct model is:

1. shared core runtime
2. shared packaging/deploy system
3. shared surface export pipeline
4. surface modules
5. edition profiles
6. package profiles
7. product matrix assembled from those pieces
8. version/capability schedule governing what ships when

### Shared Core

The shared core should own:

1. media load/play/pause/next/previous/seek/scrub
2. image/video default behavior
3. playlist/random/loop/preset state
4. sync policy
5. shared exposed control actions
6. package-safe plugin/runtime identity

The shared core should not directly own:

1. phone-specific swipe behavior
2. tablet-specific swipe behavior
3. shell mesh/layout specifics
4. marketing/demo media decisions
5. edition branding

### Surface Modules

The surface module is the authored shell/screen family around the core.

Initial release surfaces:

1. `player`
2. `modern_tv_player`
3. `laptop_player`
4. `phone_player`
5. `tablet_player`

Parked, not in the initial grid:

1. `retro_tv`
2. Meta screen line

### Edition Profiles

Editions are not full forks.

They should be profiles layered over the same core and surface contracts.

Initial editions:

1. `free`
2. `pro`
3. `pro_demo`

### Better Scheme Than Fifteen Code Lanes

Build only two real code editions:

1. `free_core`
2. `pro_core`

Then treat `pro_demo` as:

1. the `pro_core` build
2. a demo packaging profile
3. demo metadata/media/scene constraints

That means the fifteen public SKUs are real packages, but not fifteen runtime
branches.

## Existing Seams To Reuse

### Compile/feature seams

Existing compile-time and project seams already support this direction:

1. `FRAMEANGEL_CUA_PLAYER`
2. `FRAMEANGEL_FEATURE_PLAYER_INPUT`
3. stub/sidecar inclusion patterns like `FASyncRuntime.CuaTextInputBridge.cs`

These prove the new lane should stay compile-profile driven, not runtime toggle
driven.

### Runtime seams

The current partial class split should be preserved conceptually:

1. `FASyncRuntime.Player.cs` for transport/state
2. `FASyncRuntime.PlayerHosted.cs` for hosted/surface binding
3. `FASyncRuntime.Player.ControlSurface.cs` and `PlayerPluginSurface.cs` for control contracts
4. `FASyncRuntime.PlayerPresets.cs` for state persistence
5. `FASyncRuntime.Player.StandaloneResize.cs` for size behavior
6. `innerpiece-core` for reusable shell/control contracts

### Export seams

The shell/export lane already exists and should become authoritative for surface
production:

1. Unity export of shell families
2. host catalog generation
3. host package composition
4. CUA family export

That is the correct foundation for scalable new products.

### Volodeck parity seam

Volodeck should stay in the system as a repo-local parity witness for shell and
control work.

It should:

1. use the repo-local Unity authoring project
2. use the repo-local Unity bridge package
3. exercise the same exposed player and control methods that VaM will use

It should not:

1. become a separate control contract
2. depend on external legacy repos
3. become a special-case runtime authority

Meta UI toolkit should re-enter through this same parity seam during Packet
`1.5`, not as a bypass around it.

## Product Matrix

The first public family is:

1. Player
2. Player Pro
3. Player Pro Demo
4. Modern TV Player
5. Modern TV Player Pro
6. Modern TV Player Pro Demo
7. Laptop Player
8. Laptop Player Pro
9. Laptop Player Pro Demo
10. Phone Player
11. Phone Player Pro
12. Phone Player Pro Demo
13. Tablet Player
14. Tablet Player Pro
15. Tablet Player Pro Demo

### Product identity rule

Every public SKU gets:

1. a stable display name
2. a stable package name
3. one surface key
4. one edition key
5. one core build profile
6. one package profile

Future capability bundles and staggered release timing should be planned
against that same product identity rule, not as a separate scheduling system.

That identity rule is the `surface x edition x package profile` model described
in:

1. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`
2. `products/vam/config/player_version_capability_schedule.v1.json`

### Suggested package naming

Use:

1. `FrameAngel.Player.<n>.var`
2. `FrameAngel.PlayerPro.<n>.var`
3. `FrameAngel.PlayerProDemo.<n>.var`
4. `FrameAngel.ModernTVPlayer.<n>.var`
5. `FrameAngel.ModernTVPlayerPro.<n>.var`
6. `FrameAngel.ModernTVPlayerProDemo.<n>.var`
7. `FrameAngel.LaptopPlayer.<n>.var`
8. `FrameAngel.LaptopPlayerPro.<n>.var`
9. `FrameAngel.LaptopPlayerProDemo.<n>.var`
10. `FrameAngel.PhonePlayer.<n>.var`
11. `FrameAngel.PhonePlayerPro.<n>.var`
12. `FrameAngel.PhonePlayerProDemo.<n>.var`
13. `FrameAngel.TabletPlayer.<n>.var`
14. `FrameAngel.TabletPlayerPro.<n>.var`
15. `FrameAngel.TabletPlayerProDemo.<n>.var`

This is simpler than adding extra dot-separated taxonomy to the package name,
and it stays readable in `AddonPackages`.

## Free / Pro / Pro Demo Rule

### Free

Free should contain:

1. the stable player core
2. the stable shell/surface contract
3. the standard controls
4. the shipping-safe packaging path
5. no experimental/proof/debug surfaces

### Pro

Pro should contain:

1. everything in Free
2. advanced optional modules
3. shell/surface-specific richer behavior where justified
4. future premium features developed after this architecture reset

### Pro Demo

Pro Demo should contain:

1. the Pro code path
2. a deliberately bounded demo profile
3. demo media/demo scenes/marketing witness content
4. no unique runtime branch unless strictly required

## Product Build Strategy

Every product build should be generated from declarative inputs:

1. `coreBuild`
2. `surface`
3. `edition`
4. `metadataProfile`
5. `demoProfile`
6. `deployTargets`

No product should be hand-assembled in scripts by remembered arguments.

## Release Waves

### Wave 0: Freeze Prototype

Purpose:

1. stop adding new feature debt to the current prototype lane
2. treat the current player as stable witness only

### Wave 1: Architecture Setup

Deliverables:

1. product matrix config
2. version/capability schedule config
3. lane blueprint docs
4. clean package metadata profiles
5. explicit surface and edition keys

### Wave 1.5: Meta UI Toolkit Foundation

Deliverables:

1. Volodeck parity-bound Meta UI toolkit proof
2. toolkit module boundaries that fit the shared grammar
3. no special-case architecture root for toolkit work

### Wave 2: Shell Validation

Deliverables:

1. export the version-1 shell family
2. validate modern TV, laptop, phone, and tablet surfaces
3. confirm host/control anchors and player core compatibility

### Wave 3: Edition Scaffolding

Deliverables:

1. `free_core` build profile
2. `pro_core` build profile
3. `pro_demo` package profile
4. product package identities for all fifteen SKUs

### Wave 4: Parallel Product Witness Builds

Deliverables:

1. basic screen free/pro/pro_demo
2. modern tv free/pro/pro_demo
3. laptop free/pro/pro_demo
4. phone free/pro/pro_demo
5. tablet free/pro/pro_demo

At this stage the goal is not full polish. The goal is "all products build from
the same system without special-case reasoning."

### Wave 5: Remaining Feature Development

All future feature work lands here:

1. only on the new modular lane
2. only against explicit module/edition/surface boundaries
3. only after a matching row exists in the version/capability schedule
4. never as prototype drift

## Joystick Scroller Parallel Marketing Ladder

The scroller should be used as a parallel exposure lane while the player family
is being established.

Recommended ladder:

1. metadata and brand cleanup
2. deterministic demo-scene witness package
3. small UX polish update
4. first public branded release

The scroller is useful because it can keep the FrameAngel brand active between
larger player-family milestones.

## Immediate Implementation Order

This is the intended next execution order:

1. clean player metadata and brand defaults
2. export and validate shell family outputs
3. create the machine-readable product matrix
4. create the machine-readable version/capability schedule
5. scaffold edition/build profiles
6. build the first parallel witness set
7. only then start adding the few remaining desired features

## What Success Looks Like

Success is not "the prototype keeps growing."

Success is:

1. one stable shared core
2. one explicit product matrix
3. one explicit version/capability schedule
4. one shell export pipeline
5. one packaging system
6. parallel product updates that can be shipped together for visibility
7. future development that follows the architecture instead of improvising it
