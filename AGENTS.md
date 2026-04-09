Use OpenMemory as the first retrieval surface. Start with a term cluster, not a single fuzzy word.
Read the top answer-first entries, then open the cited source paths and code paths to verify them.
Treat OpenMemory as primary memory, but let live code, receipts, manifests, and runtime proof win ties.
Do not bulk hydrate across repos unless the lane genuinely requires it.
Keep canon, working hypothesis, and historical substrate separate.
Every versioned player change that is built for testing or release must get its own git commit in this repo.
Use verbose commit messages with a clear subject and a body that records the version, the seam changed, and the operator-visible outcome.
Every versioned player change that is built for testing or release must also have a matching changelog file under `products/vam/assets/player/changelog/<version>.json`.
Do not leave version bumps, deployment-worthy fixes, or release-boundary changes uncommitted.
Do not use `list_mcp_resources` to decide whether OpenMemory exists in this repo.
In this lane, OpenMemory is the local HTTP retrieval surface:
- `POST http://127.0.0.1:8081/api/ui/search`
- `POST http://127.0.0.1:8081/api/ui/context`
- `GET http://127.0.0.1:8081/api/ui/browse?kind=tag&value=<term>`
