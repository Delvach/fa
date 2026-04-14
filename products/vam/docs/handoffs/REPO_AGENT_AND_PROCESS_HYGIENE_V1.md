# Repo Agent And Process Hygiene V1

This note keeps long FrameAngel lanes from drifting into stale sidecars,
contradictory handoff state, and superseded witness artifacts.

## Agent hygiene

1. Spawn sidecars only for bounded questions or disjoint implementation slices.
2. Do not leave completed explorers or workers open after their useful result
   has been promoted into repo truth.
3. Close stale agents the same session they finish.
4. If a sidecar produced useful findings, write the winning parts into code,
   receipts, or handoff docs before closing it.
5. Keep the live lane small:
   - one main implementation thread
   - at most one truly useful sidecar at a time
6. If a sidecar freezes, contradicts canon, or stops being relevant, close it.

## Process hygiene

1. Repo docs are the handoff authority, not thread memory.
2. Versioned player work should promote important operator conversation into the
   versioned JSON conversation logs, not leave it only in thread memory.
3. Running logs should always record:
   - current branch
   - current witness artifact
   - current recommended command
   - known invalid or superseded artifact paths
4. If a proof artifact is replaced by a better authority surface, explicitly
   demote the old one in docs.
5. Remove or quarantine stale deployed artifacts that can misroute operator
   testing.
6. Keep `main` as tested truth and keep uncommitted dirt near zero.
7. Preserve the operator's unrelated local edits.
8. When hydrating or handing off, report these separately and in this order:
   - current checkout branch and commit
   - current live authority seam
   - historical packaged inventory or built-but-undistributed package state
9. Do not let a build report or staged package path masquerade as live package
   authority.

## Official VaM inspection hygiene

1. If the question is about VaM's real built-in behavior, inspect the official
   live managed DLLs first.
2. Do not pivot into archived repos, recovery repos, or old prototypes unless
   the operator explicitly asks for that historical lane.
3. Preferred official gameplay target:
   `F:\sim\vam\VaM_Data\Managed\Assembly-CSharp.dll`
4. The verified local inspection tool is:
   `C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\ildasm.exe`
5. Preferred workflow:
   - dump IL from the official DLL to a temp file
   - search the IL output for the exact runtime class or member seam
   - record the winning official class or method in repo docs if it matters to the lane
6. Current verified example:
   - the official built-in media panel runtime seam is `ImageControl`
   - it owns a `VideoPlayer` directly
   - it does not carry a separate `AudioSource` field in that runtime class

## Volodeck and VaM hygiene

1. Volodeck is a fast witness seam, not a separate product truth.
2. VaM-valid hosted proofs must name their actual export authority.
3. Do not let crisp Volodeck witnesses silently stand in for VaM-valid bundles.
4. If a witness path is visual-only or snapshot-based, say so plainly.

## Freeze recovery rule

If the session starts to degrade:

1. close stale agents first
2. open the active running log
3. confirm branch and working tree state
4. confirm the current witness artifact and recommended command
5. resume from repo truth, not chat recollection

## Current Meta UI application

As of `2026-04-13`:

1. the `modern_tv` interactive hosted-player proof is the active VaM-facing
   host lane
2. the raw 2018 shell export is the active VaM-valid host authority
3. 2022 host-package outputs remain Volodeck/package witnesses, not host-load
   authority
4. completed architecture and Volodeck audit sidecars should be closed once
   their findings are written into repo docs
