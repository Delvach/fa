# Player Operator Conversation Log Canon V1

This lane now keeps a versioned JSON conversation log alongside the changelog.

The changelog records what changed in the product. The conversation log records
what the operator and assistant said that materially shaped the version,
including test feedback, seam authority, and recovery decisions that would
otherwise get lost in thread memory.

## Scope

These logs are for:

1. operator and assistant conversation relevant to a specific player version or
   deploy iteration
2. test feedback that materially changed the seam or the release decision
3. exact repo timestamps that anchor the conversation in durable truth
4. explicit separation between exact facts and reconstructed inference

These logs do not replace:

1. `products/vam/assets/player/changelog/<version>.json`
2. release validation receipts
3. package reports
4. packet-level markdown running logs

## Location

Store them under:

`C:\projects\fa\products\vam\assets\player\docs\handoffs\operator_conversation_logs`

## File naming

Use the shortest name that still preserves authority:

1. retroactive recovered version log:
   `<version>.recovered.json`
2. active deploy-iteration log:
   `<version>.<iteration>.json`

Examples:

1. `0.6.8.recovered.json`
2. `0.6.10.recovered.json`
3. `0.6.11.alpha.json`

## Schema

Use:

`frameangel_player_operator_conversation_log_v1`

Required top-level fields:

1. `schemaVersion`
2. `version`
3. `iteration`
4. `deployLabel`
5. `captureMode`
6. `timeAuthority`
7. `entries`
8. `relatedArtifacts`

Recommended fields:

1. `startedAt`
2. `endedAt`
3. `exactAnchors`
4. `decisions`
5. `openQuestions`
6. `notes`

## Timestamp rules

Use exact timestamps whenever they come from durable repo truth:

1. git commit timestamps
2. release validation receipts
3. package report receipts
4. proof receipts
5. file generation timestamps recorded inside JSON artifacts

If the exact conversation message time is unavailable, do not invent one.
Instead:

1. set `timestampKind` to `approx_between_exact_events`
2. provide `after` and `before` anchors
3. mark the entry `reconstructedFrom`

## Entry rules

Each entry should preserve:

1. who spoke
2. what was said or decided
3. whether the text is verbatim or reconstructed
4. what durable evidence supports it

Suggested entry fields:

1. `entryId`
2. `timestamp`
3. `timestampKind`
4. `after`
5. `before`
6. `speaker`
7. `verbatim`
8. `message`
9. `reconstructedFrom`

## Operational rule

For every versioned player change that is built for testing or release:

1. keep the changelog
2. keep the release receipts
3. update or create the matching conversation log JSON

If a version is being retested without code changes but under a new deploy
iteration, update the iteration-specific conversation log instead of creating a
new semantic-version log.

## Honesty rule

These logs exist to preserve continuity, not to fake precision.

So:

1. exact facts stay exact
2. reconstructed conversation stays marked reconstructed
3. repo truth beats memory
4. thread memory may enrich the log, but it cannot overwrite receipts, commits,
   manifests, or live deployed artifact paths
