Use OpenMemory as the first retrieval surface. Start with a term cluster, not a single fuzzy word.
Read the top answer-first entries, then open the cited source paths and code paths to verify them.
Treat OpenMemory as primary memory, but let live code, receipts, manifests, and runtime proof win ties.
Do not bulk hydrate across repos unless the lane genuinely requires it.
Keep canon, working hypothesis, and historical substrate separate.
Every versioned player change that is built for testing or release must get its own git commit in this repo.
Use verbose commit messages with a clear subject and a body that records the version, the seam changed, and the operator-visible outcome.
Every versioned player change that is built for testing or release must also have a matching changelog file under `products/vam/assets/player/changelog/<version>.json`.
Do not leave version bumps, deployment-worthy fixes, or release-boundary changes uncommitted.
## Branch workflow rule

Use a minimal branch discipline for this lane:

1. start every non-trivial slice from `main` on a named branch
2. use `feature/<seam>` for bounded feature work
3. use `release/<version>-<seam>` for any slice expected to produce a tested versioned player build
4. do not perform versioned build/test work directly on `main`

## Unity parking rule

If the local Unity authoring project becomes dirty before runtime or release work is ready:

1. commit and park those Unity changes on their own branch such as `unity/<seam>` or `park/unity-<seam>`
2. return runtime work to a fresh branch from `main`
3. only merge or cherry-pick the parked Unity commits forward when the runtime or release slice actually needs them

Do not let unrelated Unity authoring churn ride along with runtime or release commits.

## Merge back to main rule

A tested versioned player change must merge back to `main` from its branch only after:

1. the change was actually tested in the intended local witness path
2. the version stamp was updated
3. the matching `products/vam/assets/player/changelog/<version>.json` exists
4. the tested versioned player commit already exists in git with the version, seam, and operator-visible outcome recorded

`main` must remain the latest tested player line, not a parking lot for partial release work.

## Commit and changelog alignment rule

For versioned player work:

1. keep the tested seam, the version stamp, and the matching `changelog/<version>.json` aligned in the same branch
2. do not merge a version bump without its matching changelog
3. do not merge a changelog-only version entry before the tested code that justifies it
4. do not squash away the explicit tested version commit if that would sever the version boundary from its changelog and receipt trail

Do not use `list_mcp_resources` to decide whether OpenMemory exists in this repo.
In this lane, OpenMemory is the local HTTP retrieval surface:
- `POST http://127.0.0.1:8081/api/ui/search`
- `POST http://127.0.0.1:8081/api/ui/context`
- `GET http://127.0.0.1:8081/api/ui/browse?kind=tag&value=<term>`
