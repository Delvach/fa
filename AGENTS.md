# FrameAngel Repo Agent Canon

Updated: `2026-04-13`

## Core retrieval rule

This repo now carries the working truth for the current player and Meta lanes.

Use this order:

1. repo-local canon docs
2. live code
3. receipts, manifests, and staged package reports
4. runtime proof
5. OpenMemory only when historical retrieval is genuinely needed

If OpenMemory is used:

1. start with a term cluster, not one fuzzy term
2. read answer-first hits
3. open the cited source paths and verify against live repo truth
4. let live code, receipts, manifests, and runtime proof win ties

Do not bulk hydrate across repos unless the lane truly requires it.
Keep canon, working hypothesis, and historical substrate separate.

## Current product state

As of this update:

1. `main` is the latest merged tested player line
2. the current stable player baseline is `0.6.10`
3. the active feature work beyond that restored the `0.6.11` `modern_tv` hosted interactive proof seam
4. the modular product-system canon and Packet `1.5` Meta toolkit canon are already in this repo

Primary current docs:

1. `C:\projects\fa\products\vam\docs\handoffs\VAM_PLAYER_PRODUCT_SYSTEM_BLUEPRINT_V1.md`
2. `C:\projects\fa\products\vam\docs\handoffs\VAM_PLAYER_PRODUCT_SYSTEM_EXECUTION_PLAN_V1.md`
3. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_META_UI_PACKET_1_5_RUNNING_LOG_V1.md`
4. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_VOLODECK_PARITY_BOUNDARY_V1.md`
5. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_SHELL_RECOVERY_BOUNDARY_V1.md`
6. `C:\projects\fa\products\vam\docs\handoffs\REPO_AGENT_AND_PROCESS_HYGIENE_V1.md`

## Current interactive Meta proof authority

The current real `modern_tv` hosted-player proof is:

1. preset:
   `F:\sim\vam\Custom\Atom\CustomUnityAsset\Preset_FA CUA Player Modern TV Interactive Proof.vap`
2. host bundle:
   `F:\sim\vam\Custom\Assets\FrameAngel\Player\fa_cua_player_modern_tv.assetbundle`
3. raw plugin:
   `F:\sim\vam\Custom\Plugins\dev_plugin_player.0.6.11.dll`
4. package reference:
   `F:\sim\vam\AddonPackages\FrameAngel.DevPlayer.11.var`
5. proof receipt:
   `C:\projects\fa\products\vam\assets\player\build\meta_interactive_host_proof\modern_tv\receipts\meta_interactive_hosted_player_proof_receipt.json`

Important:

1. the current proof authority is the raw `2018.1.9f2` host export seam
2. do not send the operator back to the stale `fa_cua_player_modern_tv_v1.assetbundle` 2022 seam
3. search-bar and grid-menu Meta proofs are still snapshot carriers, not full interactive widgets

## Volodeck boundary

Volodeck is a parity witness, not a separate product runtime.

Use it for:

1. visual orientation
2. shell stance and layout
3. control placement and motion
4. pre-VaM proof images and receipts

Do not use it to justify fake parity:

1. no custom Volodeck-only control path
2. no Volodeck-specific harness logic bloating the product components
3. no claim of exact VaM interaction emulation unless the live seam actually matches

Current honest split:

1. Volodeck/2022 is the best visual witness seam
2. raw 2018 shell export is the current VaM-valid host-load seam
3. `.var` packaging is the release/distribution seam

## Versioned player rule

Every versioned player change built for testing or release must:

1. get its own git commit in this repo
2. use a verbose commit message with:
   - version
   - seam changed
   - operator-visible outcome
3. update `products/vam/assets/player/player.version.json`
4. update `products/vam/plugins/player/src/shared-runtime/BuildRuntimeInfo.cs`
5. add matching `products/vam/assets/player/changelog/<version>.json`
6. build
7. deploy in the intended seam
8. leave receipts/manifests behind

Do not leave version bumps, deployment-worthy fixes, or release-boundary changes uncommitted.

## Build and deploy lanes

Use the right seam for the task:

1. raw `Custom/...` lane:
   - fastest interactive dev seam
   - valid for hosted-player proofing and manual attach workflows
2. `.var` lane:
   - release/distribution seam
   - must stay healthy even when raw proofing is used
3. Volodeck lane:
   - preflight visual witness seam

Do not let one seam silently replace another in canon.

## Live authority and caching rule

VaM caching is a real product constraint in this repo.

For any versioned test or release slice:

1. choose exactly one live authority seam:
   - versioned `.var`
   - or versioned `Custom/...`
2. do not keep the same version live in both seams at once
3. do not assume removing a `.var` from `AddonPackages` clears VaM's memory of it
4. do not assume `Custom/...` artifacts with the same version are harmless when a
   same-version `.var` has already been seen by VaM
5. all live filenames must be versioned so the operator can see exactly what is
   being loaded

Working rule:

1. pre-release uses `dev` names internally and externally
2. final release uses `prod` names internally and externally
3. if a slice is being tested in `.var`, treat `.var` as the only live authority
4. if a slice is being tested in `Custom/...`, treat raw `Custom/...` as the
   only live authority
5. do not mix those seams for one version boundary

If there is any doubt about which copy VaM is loading, the lane is not ready to
call tested.

## Branch discipline

Use minimal branch discipline.

1. stay on the branch you were assigned
2. do not make extra branches unless the user explicitly allows it
3. if a branch is needed, use:
   - `feature/<seam>` for bounded feature work
   - `release/<version>-<seam>` for versioned player slices
4. do not do versioned build/test work directly on `main`
5. `main` must remain the latest merged tested player line

Do not make a copy of the repo unless the user explicitly allows it.

## Unity parking rule

If Unity authoring dirt appears before runtime or release work is ready:

1. park or isolate it intentionally
2. do not let unrelated Unity churn ride along with runtime/release commits
3. only merge or cherry-pick Unity authoring forward when the current seam truly needs it

## Agent and process hygiene

Keep sidecars bounded and short-lived.

1. when a sidecar result becomes repo truth, close the sidecar the same session
2. maintain running logs and handoff docs for long lanes
3. if the thread freezes, reopen repo truth before reopening exploration
4. do not accumulate stale completed agents

## Coding hard rules

NEVER:

1. use `System.IO` in product code
2. use reflection in product code

## Final reminder

This repo has the knowledge needed for the current player, shell, Meta UI, packaging, and release lanes.

Start from repo truth, keep the seams separate, and do not drift.
