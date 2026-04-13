# Player Volodeck Parity Boundary V1

## Purpose

This note defines the deterministic Volodeck witness seam for player shell work.

Volodeck exists to let shell/control work be exercised in Unity before VaM
testing, but only if parity stays honest.

For the broader modular product-system plan that Volodeck supports, use:

1. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_BLUEPRINT_V1.md`
2. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_EXECUTION_PLAN_V1.md`
3. `products/vam/docs/handoffs/VAM_PLAYER_PRODUCT_SYSTEM_SURFACE_EDITION_PACKAGE_PROFILE_STRATEGY_V1.md`
4. `products/vam/config/player_version_capability_schedule.v1.json`
5. `products/vam/assets/player/docs/handoffs/PLAYER_VOLODECK_VISUAL_AND_INTERACTION_GUARDRAILS_V1.md`

## Current repo-local authority

Volodeck Unity project:

`C:\projects\fa\products\vam\assets\player\unity\ghost_training_export_clone`

Volodeck Unity bridge package:

`C:\projects\fa\products\vam\assets\player\unity_editor_bridge\current`

Current deterministic entry points:

1. `C:\projects\fa\products\vam\assets\player\scripts\Export-GhostPlayerHostCuaFamily.ps1`
2. `C:\projects\fa\products\vam\assets\player\scripts\Export-GhostPlayerHostCatalog.ps1`
3. `GhostMetaUiSetBootstrap.CreateStudySceneBatch`
4. `C:\projects\fa\products\vam\assets\player\scripts\Build-PlayerMetaUiPacket15Foundation.ps1`

## Hard rule

Volodeck is a parity witness, not a separate authority surface.

That means:

1. it should exercise the same exposed player methods and control contracts that
   VaM scenes use
2. it should not invent a special control path just for Unity proofing
3. it should not rely on external legacy repos for bridge/runtime setup
4. it should not be described as exact VaM interaction emulation when the live
   session seam is still materially different

## Current integration role

Volodeck is the fastest pre-VaM witness seam for:

1. shell layout and placement
2. control anchoring and motion
3. visible playback/parity checks
4. exported host/package validation before operator testing

Volodeck is not yet the exact authority for every VaM-internal interaction
behavior. Current honest use is:

1. prove shell orientation and control layout
2. prove the exposed control contract is the same contract the runtime expects
3. prove there is a real player-backed artifact to load in VaM
4. do not over-claim full interaction parity when live VaM chaining is still a
   known gap

Meta UI toolkit should use this same parity witness seam once Packet `1.5` begins.

## Operator gate

Do not send work to the operator for testing until Volodeck has already produced:

1. a contextual witness image that is not too far out to judge the resource
2. a tighter surface witness image that is not so zoomed in that layout/orientation is lost
3. a receipt that records the exact harness command and output paths

If the witness is blank, tiny, overly cropped, or only proves one framing, it is
not ready for operator testing.

If the current slice is using raw `Custom/...` deploy for faster interactive
proofing:

1. keep the `.var` output capability intact as the release lane
2. record the live raw asset/plugin/preset paths and the packaged release
   reference in the same receipt
3. do not let the raw dev seam silently replace the package lane in canon

## Deterministic posture

Keep the following local to `C:\projects\fa`:

1. the Unity authoring project
2. the Unity bridge package
3. the export wrappers
4. the host/shell export receipts

If any of those drift outside the repo, treat that as architecture debt to
remove before calling Volodeck healthy.
