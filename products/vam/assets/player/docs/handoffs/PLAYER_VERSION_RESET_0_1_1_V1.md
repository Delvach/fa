# Player Version Reset 0.1.1

## Current canon

The `fa` repo is the replacement home for the FrameAngel monorepo player code.
Its clean-room version line starts at `0.1.1`.

The old `0.0.x` numbers were inherited while standing the repo up from the older lane.
They are useful as historical substrate and test witnesses, but they are not the long-term canon for this repo.

## Why this reset exists

1. the new repo needs its own clear version lineage
2. old monorepo player numbers should not keep polluting the replacement repo
3. operator test notes and future release receipts should point at the clean-room line going forward

## Working rule

1. every versioned player change that is built for testing or release must get its own git commit
2. commit messages should be verbose and capture:
   - the version
   - the seam changed
   - the operator-visible result
3. do not leave version bumps or deployment-worthy changes uncommitted

## Starting point

- current clean-room version line: `0.1.1`
- previous `0.0.111` through `0.0.113` builds remain historical witnesses only
