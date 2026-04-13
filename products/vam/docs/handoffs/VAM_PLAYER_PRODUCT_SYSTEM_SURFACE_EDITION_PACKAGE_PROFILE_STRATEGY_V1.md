# VaM Player Product System Surface Edition Package Profile Strategy V1

## Purpose

This note captures the long-range scaling model for the player family.

The stable product equation is:

1. surface
2. edition
3. package profile

That is the durable release identity rule for the family.

The surface defines the authored presentation and control shell.

The edition defines the feature entitlement and product tier.

The package profile defines how a version is assembled, branded, and released.

This note is for long-term scaling and release scheduling, not for the first-release authority seam.

The current machine-readable schedule artifact for this strategy is:

1. `products/vam/config/player_version_capability_schedule.v1.json`

## Architecture Position

The current architecture should keep solidifying around shared core runtime, surface modules, edition profiles, and package profiles.

Once that architecture is stable enough to trust, Meta UI toolkit becomes the next prerequisite.

It should come after the current architecture solidifies, not before it.

The reason is simple: Meta UI should land on top of a stable surface x edition x package profile contract, not become the contract that the rest of the product has to reshape around.

## Product Taxonomy Rule

The scalable internal grammar is:

1. surface
2. edition
3. package profile
4. capability bundle

Do not mint new architecture roots when an idea can be expressed as:

1. a new capability bundle
2. a new surface
3. a new edition/profile combination
4. an adjacent product family that reuses the same packaging and release grammar

Legacy portfolio labels stay useful, but they should be classified cleanly:

1. base player, TV, phone, tablet, and laptop are surface families
2. Halo, joystick navigation, keyboard, file browser, editor, AR, and movie-studio ideas are capability bundles or adjacent product families
3. bundle or pricing labels are edition/package concerns, not runtime roots

The old product sheet is still useful for intent, but it should now be read with
that classification instead of being used as a new architecture map.

## Meta UI Toolkit Checkpoint

Meta UI toolkit is the next prerequisite after the current architecture is solid.

Treat that as Packet `1.5` in practice:

1. the matrix, package profiles, shell exports, and Volodeck parity seam must already be trustworthy
2. Meta UI toolkit should then be integrated as a reusable authored/control module
3. it should not replace the surface x edition x package profile grammar

The goal is for Meta UI to become one more composable piece in the factory, not a special lane that bypasses the factory.

## Version And Capability Schedule

Use a version/capability schedule to plan releases.

The schedule pairs:

1. a semantic version
2. one or more capability bundles
3. the surface x edition x package profile cells that can ship those capabilities

This lets the release line factory-produce multiple variants on a skewed cadence.

That skew is intentional:

1. one variant can land first when it is ready
2. related variants can pick up the same capability later
3. the family still stays aligned to the same versioned capability plan

The schedule should answer three questions before release work starts:

1. what version is being stamped
2. what capability set is being shipped
3. which surface, edition, and package profile combinations are included

### Schedule shape

Keep the schedule as a table, not scattered prose.

Keep the machine-readable JSON schedule and the human-readable table aligned.

Suggested columns:

| family version | capability bundle | prerequisite state | included surfaces | included editions | release skew note |
| --- | --- | --- | --- | --- | --- |
| `v1` | baseline family authority | matrix, package profiles, shell parity, Volodeck parity | selected launch surfaces | selected launch editions | launch the first ready surface first, then fill the family |
| `v1.5` | Meta UI toolkit foundation | baseline family authority stable | only surfaces that genuinely need toolkit proof | mostly internal/proof editions first | do not widen before toolkit parity is real |
| `v2` | next scheduled bundle, such as loop or shuffle if chosen | `v1` stable in live witnesses | all surfaces that can honestly ship it | whichever editions are intended to expose it | skew okay; one surface can lead, others can follow |
| `v3` | next scheduled bundle | `v2` stable | same rule | same rule | keep cadence intentional, not ad hoc |

The exact bundles can stay in flux until chosen, but the table shape should exist before implementation starts.

The current canonical schedule artifact is:

1. `products/vam/config/player_version_capability_schedule.v1.json`

## Release Factory Rule

Do not schedule the family by memory or ad hoc branch history.

Use the version/capability schedule as the source of truth for which variants are built, when they are built, and which package profile they use.

That gives the product line a factory shape:

1. choose the version
2. choose the capability set
3. choose the surface x edition x package profile cells
4. emit the matching release artifacts and changelog entries

The result is a scalable release system that can expand without forcing every variant to move in lockstep.

## Drift Guardrail

If a planned release cannot be described as:

1. one version row
2. one or more capability bundles
3. a set of surface x edition x package profile cells

then the issue is architecture, not feature implementation.
