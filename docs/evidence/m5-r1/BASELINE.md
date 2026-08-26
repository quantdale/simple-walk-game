# M5-R1 BASELINE

Date: 2026-08-26
Repository: quantdale/simple-walk-game
Campaign worktree: `simple-walk-game-wt-m5-r1-runtime-shell-ox1` (branch
`agent/simple-walk-game/m5-r1-runtime-shell-ox1`, own writer lease)

## Starting state

* Starting SHA: `d73183497a6d2ca3f7845cfee1697d1faeff7c5d`
  (= `origin/main` at session start; fetched fresh, no remote drift; matches the
  planner's observed baseline exactly).
* Identity guard: `REPOSITORY IDENTITY OK: quantdale/simple-walk-game`.
* The primary checkout held another session's writer lease with uncommitted work on
  `main`; per the single-writer contract it was left completely untouched and this
  campaign ran in a dedicated linked worktree created via
  `scripts/new-agent-worktree.sh m5-r1-runtime-shell ox1`.

## Baseline verification (in the campaign worktree, at starting SHA)

```
dotnet build SimpleWalkGame.sln   → clean
dotnet test                       → 221/221 passed
  WalkGame.Domain.Tests           105 passed
  WalkGame.Infrastructure.Tests    37 passed
  WalkGame.Application.Tests       79 passed
```

Matches the planner's stated baseline (221 automated tests green) and the M8-H1
outcome record. Guard proof suite not re-run in this environment beyond the identity
and lease checks performed (POSIX guard scripts require Git Bash sh; the two checks
that gate writing were exercised directly).
