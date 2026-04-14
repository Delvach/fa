# FrameAngel Repo Agent Canon

Updated: `2026-04-14`

## Instruction hierarchy

For this repo:

1. this file is the main repo authority
2. deeper `AGENTS.md` files are additive only
3. if a deeper file contradicts this file or the current canon docs, update the
   deeper file or ignore it

## Core retrieval rule

This repo now carries the working truth for the current player and Meta lanes.
OpenMemory is no longer a source-of-truth surface for this repo.

Use this order:

1. repo-local canon docs
2. live code
3. receipts, manifests, and staged package reports
4. runtime proof
5. OpenMemory only when historical retrieval is genuinely needed and the repo
   does not already carry the answer cleanly

If OpenMemory is used:

1. treat it as historical substrate, not authority
2. start with a term cluster, not one fuzzy term
3. read answer-first hits
4. open the cited source paths and verify against live repo truth
5. let repo docs, live code, receipts, manifests, and runtime proof win ties

Do not bulk hydrate across repos unless the lane truly requires it.
Keep canon, working hypothesis, and historical substrate separate.

## Official VaM DLL inspection rule

When the lane needs the official VaM implementation:

1. inspect the live managed VaM assemblies first
2. do not drift into archived repos, recovery repos, or older prototypes
3. prefer the live gameplay assembly:
   `F:\sim\vam\VaM_Data\Managed\Assembly-CSharp.dll`
4. use the locally available IL disassembler:
   `C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\ildasm.exe`
5. dump IL to a temp file and search that output instead of guessing type names
6. treat the official DLL result as authority over historical recollection

Current verified official seam:

1. the built-in VaM media panel runtime is `ImageControl`
2. `ImageControl` owns a `VideoPlayer` directly in the official gameplay DLL
3. `ImageControl` does not carry a separate `AudioSource` field in that runtime class

## Current product state

As of this update:

1. `main` is the latest merged tested player line
2. the current tested player baseline is the raw core-player `0.6.41` line
3. the current recovery and pro-polish lane is the raw core-player `dev_deploy` seam plus the deterministic `demo3` scene:
   - `0.6.37` reset visible progress on media navigation
   - `0.6.38` reset generated demo3 scrub sliders to zero
   - `0.6.39` made demo3 sliders and labels reflect live player state
   - `0.6.40` rebased side controls into left and right screen space
   - `0.6.41` fixed demo3 text repaint and rotated the side control groups around the owning screen yaw
4. the modular product-system canon and Packet `1.5` Meta toolkit canon are already in this repo
5. the current immediate queue is:
   - `0.6.45` is the active raw operator test seam for demo3 toggle-state readback and restored side control transform data
   - `0.6.46`: interactive stateful `A-B` checkbox enable/disable behavior
   - `0.6.47`: image slider navigation and state
   - `0.6.48`: more aggressive scrub feedback while dragging
   - `0.6.49`: slideshow
   - `0.6.50`: right-stick up/down volume without trigger
6. Favorites is a separate `0.7.0` capability lane and must not be folded into the current `0.6.x` transport fixes
7. the `modern_tv` hosted proof is still useful as a witness seam, but it is not the default operator test seam for the current player lane

Primary current docs:

1. `C:\projects\fa\products\vam\docs\handoffs\VAM_PLAYER_PRODUCT_SYSTEM_BLUEPRINT_V1.md`
2. `C:\projects\fa\products\vam\docs\handoffs\VAM_PLAYER_PRODUCT_SYSTEM_EXECUTION_PLAN_V1.md`
3. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_META_UI_PACKET_1_5_RUNNING_LOG_V1.md`
4. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_VOLODECK_PARITY_BOUNDARY_V1.md`
5. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_SHELL_RECOVERY_BOUNDARY_V1.md`
6. `C:\projects\fa\products\vam\docs\handoffs\REPO_AGENT_AND_PROCESS_HYGIENE_V1.md`
7. `C:\projects\fa\products\vam\docs\handoffs\VAM_DEPLOYMENT_AND_NAMING_CANON_V1.md`
8. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_OPERATOR_CONVERSATION_LOG_CANON_V1.md`
9. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_MIXED_MEDIA_PLAYLIST_AND_SCRUB_SEAMS_V1.md`
10. `C:\projects\fa\products\vam\assets\player\docs\handoffs\PLAYER_FAVORITES_ARCHITECTURE_PLAN_0_7_0_V1.md`

## Current live player authority

The current live core-player raw `dev_deploy` seam is:

1. direct-player raw asset:
   `F:\sim\vam\Custom\Assets\FrameAngel\Player\dev_cua_player.0.6.45.assetbundle`
2. raw plugin:
   `F:\sim\vam\Custom\Plugins\dev_plugin_player.0.6.45.dll`
3. deterministic live scene:
   `F:\sim\vam\Saves\scene\demo3.0.6.45.json`
4. release validation receipt:
   `C:\projects\fa\products\vam\assets\player\build\releases\0.6.45\foundation_release_validation.json`

The current `modern_tv` hosted proof remains a witness seam, not the default
operator test seam for this recovery slice:

1. preset:
   `F:\sim\vam\Custom\Atom\CustomUnityAsset\Preset_dev_modern_tv.0.6.20.alpha.vap`
2. host bundle:
   `F:\sim\vam\Custom\Assets\FrameAngel\Player\asset_dev_modern_tv.0.6.20.alpha.assetbundle`
3. hosted proof receipt:
   `C:\projects\fa\products\vam\assets\player\build\meta_interactive_host_proof\modern_tv\receipts\meta_interactive_hosted_player_proof_receipt.json`

Important:

1. the current live authority for the player recovery lane is the raw core-player direct asset plus plugin, not the hosted `modern_tv` witness
2. do not send the operator back to the stale `fa_cua_player_modern_tv_v1.assetbundle` 2022 seam
3. search-bar and grid-menu Meta proofs are still snapshot carriers, not full interactive widgets
4. keep the matching version or iteration conversation log updated under
   `C:\projects\fa\products\vam\assets\player\docs\handoffs\operator_conversation_logs`
5. keep exact repo timestamps and reconstructed chat timing explicitly separate
6. the current preset chooser fix means saved presets must stay on an explicit `(none)` state until the operator chooses one
7. the current plugin and demo3 readback seam exposes live build version, target, media, timeline, playlist state, sliders, and current or total labels without widening the transport model
8. the deterministic resize truth for scene generation remains the hosted CUA native `scale` storable targeted directly with VaM trigger timer or tween fields, as proven by `F:\sim\vam\Saves\scene\demo3.json`
9. the current hosted proof preset discovery rule is literal: raw preset filenames must begin with uppercase `Preset_`
10. the current hosted and baseline 2018 asset names are intentionally short:
    - `assets/fa/ps18/modern_tv/main.prefab`
    - `assets/fa/player/main.prefab`
11. the current one-second sync seam remains active for this lane, but its correction authority is fully video-time-owned: `AudioSource.time` is no longer consulted, while `prepareCompleted`, `started`, `seekCompleted`, and `clockResyncOccurred` refresh the player-owned timeline
12. the current mixed-media policy is still explicit: mixed directories are valid playlists, but image scrub is a deliberate extension seam rather than already-frozen canon

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
   - if this lane needs package inventory for reports or wrappers, build it with
     `-SkipVarDistribute`; do not use `-PackageOnlyDeploy` for a raw-only slice
2. `.var` lane:
   - release/distribution seam
   - must stay healthy even when raw proofing is used
3. Volodeck lane:
   - preflight visual witness seam

Do not let one seam silently replace another in canon.

## Live authority and caching rule

VaM caching is a real product constraint in this repo.

Deployment labels:

1. `dev_deploy`
   - versioned raw `Custom/...` authority
   - used for faster interactive/debug proofing
2. `prerelease_deploy`
   - versioned `.var` authority using `Dev` externally and `dev` internally
   - used for operator prerelease testing
3. `release_deploy`
   - versioned `.var` authority using `Prod` externally and `prod` internally
   - used for final public release

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
6. treat `player_var_package_report_latest.json` as a build artifact, not live
   prerelease authority, unless it shows `distributed:true` and the exact `.var`
   is physically present in `F:\sim\vam\AddonPackages`
7. when hydrating or handing off, report these separately and in this order:
   - current checkout branch and commit
   - current live authority seam
   - historical packaged inventory or built-but-undistributed package state

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
6. if the working branch name drifts behind the actual tested version line:
   - fast-forward `main` to the latest tested commit first
   - then cut a fresh release branch for the next version
   - do not keep shipping new versions from a stale branch label

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
