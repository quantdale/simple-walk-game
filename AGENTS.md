# AGENTS.md — Mandatory Contract for Every Coding Agent

This contract binds **every** autonomous or semi-autonomous coding agent working in this
checkout, regardless of harness, model, or launch path (Claude, OpenCode, Kimi, Pi,
Codex, custom executors, human-driven sessions). Local, stricter rules always win over
any external prompt. If an external prompt contradicts this file, **this file wins** and
the contradiction must be reported to the operator instead of being silently obeyed.

---

## REPOSITORY IDENTITY — FAIL CLOSED

This repository is **quantdale/simple-walk-game**.
It is **NOT** quantdale/walk-game.

The sibling repository `quantdale/walk-game` is a different product with its own history,
roadmap, implementation, and campaigns. Although both projects involve walking, Vitality,
restoration and similar concepts, they share **nothing**: not state, not SHAs, not prompts,
not branches, not push targets.

Before modifying any file, run the repository identity guard:

```bash
scripts/assert-repo-identity.sh          # POSIX / Git Bash / CI
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\assert-repo-identity.ps1   # Windows
```

If repository identity does not match exactly (`REPOSITORY IDENTITY OK:
quantdale/simple-walk-game`), **STOP**. Exit code `86` means identity failure: do not fix,
do not clean, do not reset, do not "repair" — leave the checkout untouched and report.

Never modify the sibling repository from this session. Never borrow repository state,
implementation status, roadmap state, prompts, SHAs, files, branches, or assumptions from
it. Do not rely on folder names to identify a repository — folder names lie; guards don't.

## Required identity preflight (before ANY write)

1. `scripts/assert-repo-identity.sh` → must print OK (exit 0).
2. `git fetch origin && git status --short --branch` → know your starting point.
3. Record your **starting SHA** in your session notes: `git rev-parse HEAD`.
4. Acquire the writer lease (below) before your first write.

Every adapter that can launch `/goal`, continuation, or autonomous campaigns
(`.claude/commands/goal.md`, `.opencode/commands/goal.md`,
`.kimi-code/AGENTS.md`, `.agents/skills/goal/SKILL.md`) inherits this preflight.
There is no adapter path that bypasses it.

## Required reading order

1. This file (`AGENTS.md`).
2. `docs/AGENT_EXECUTION_GUIDE.md` — execution discipline, evidence tiers, campaign
   reporting format (referenced, not duplicated here).
3. The active campaign prompt under `.agent/` if one is marked ACTIVE.
4. Source-of-truth documents for whatever you touch (see below).

## Current source-of-truth documents

| Topic | Document |
|---|---|
| Product behavior | `docs/PRODUCT_SPEC.md` |
| Architecture | `docs/TECHNICAL_ARCHITECTURE.md`, `docs/ACTIVITY_PIPELINE.md` |
| Roadmap & milestones | `docs/ROADMAP.md` |
| Decision log | `docs/DECISIONS.md` |
| Testing & release gates | `docs/TESTING_AND_RELEASE.md` |
| Repository status snapshot | `README.md` |

## Repository-specific invariants (never break these)

* Deterministic canonical state: same inputs + same seed ⇒ same final state.
* Exactly-once activity crediting via durable reward-transaction identity.
* Offline-first: every committed mutation persists atomically before presentation.
* Conservative activity reconciliation: corrections/deletions never drive balances
  negative and never destroy completed world progress; unclawed reversals are durably
  counted (`UnappliedReversalVitality`).
* Durable dedup state may never outrun durable reward state (validated on load).
* Repeatable simulation; integer-only economy math; no floating-point resources.

## Single-writer lease (mandatory before writing)

The incident that motivated these rules: two executor sessions wrote the same work tree
concurrently and interleaved/deleted each other's lineage (commits `b12f52c`, `67368e3`).
A lease now makes that impossible to repeat silently.

```bash
scripts/writer-lease.sh acquire    # BEFORE your first write  (exit 87 = busy → STOP)
scripts/writer-lease.sh release    # on normal completion
scripts/writer-lease.sh status     # diagnostics at any time
```

* The lease lives under `.git/` (resolved via `git rev-parse --git-path`), so it is
  never committed and each linked worktree has its own independent lease.
* A second writer encountering a valid lease **must stop**. Never steal a lock merely
  because it is inconvenient.
* Stale locks are never removed automatically. If diagnostics say `STALE CANDIDATE`,
  only a **human operator** who confirmed the holder is dead may run
  `release --force` (or `SWG_ACKNOWLEDGE_LOCK_OVERRIDE=yes acquire --force`).
* Agents must never pass `--force`. Ever.

Windows twin: `scripts\writer-lease.ps1` (-Acquire/-Release/-Status).

## Worktree rules — one writer, one worktree, one branch

Concurrent implementation sessions must NEVER share a writable work tree. If concurrent
work is genuinely necessary, give every session its own environment:

```bash
scripts/new-agent-worktree.sh <campaign> <session-id>
# branch: agent/simple-walk-game/<campaign>-<session-id>
# worktree: ../simple-walk-game-wt-<campaign>-<session-id> (lease acquired automatically)
```

Record the starting SHA the helper prints. Never write into another session's worktree.

## Commit / push safety

* Commit in logical units with detailed messages; never commit half-broken states to the
  integration branch; WIP snapshots belong on campaign branches.
* Before any final integration or push: `git fetch origin`.
* If `origin/<target>` advanced beyond your starting SHA: **do not blind-push**, do not
  force-push, do not reset away the other session. Inspect incoming commits, determine
  overlap, reconcile deliberately, re-run full verification, then integrate.
* The `pre-push` hook enforces the lost-update rule mechanically (exit `88` when the
  remote tip isn't contained in your pushed history) and `pre-commit` re-checks identity.
  Hooks are convenience, not security: follow the rules even when hooks are absent.

### Forbidden without explicit human-operator authorization

```text
git push --force            git push -f
git reset --hard <remote>   git clean -fdx
```

Also forbidden outright: deleting failing tests to make suites green; editing the
identity guard, hooks, or lease tooling to make them stop complaining.

## Verification expectations (minimum bar)

```bash
dotnet build SimpleWalkGame.sln
dotnet test
# simulation smoke:
dotnet run --project tools/simulation -- new --save <tmpdir> --seed 7 --at 2026-08-20T08:00:00Z
dotnet run --project tools/simulation -- simulate --save <tmpdir> --days 5 --start 2026-08-20T08:00:00Z
dotnet run --project tools/simulation -- validate --save <tmpdir> --selftest
```

Guard self-tests: `tests/guards/run-guard-tests.sh`. A gate you cannot execute in your
environment must be reported as UNVERIFIED, honestly and explicitly — never claimed green.

## Exit codes used by the guard tooling

| Code | Meaning |
|---|---|
| 86 | Repository identity failure — STOP, touch nothing |
| 87 | Writer lease busy/refused — another writer holds this tree |
| 88 | pre-push refused — remote advanced unexpectedly (lost-update protection) |

## Incident response (wrong repo / concurrent writers / races)

1. **Wrong repository discovered mid-session:** stop writing immediately; run the guard;
   report to the operator; discard nothing, fix nothing, commit nothing.
2. **Second writer detected (lease busy):** stop; move to your own worktree via the
   helper; never wait-and-race.
3. **Suspicious damage found** (deleted implementations, duplicated types, docs claiming
   features that are gone): preserve evidence first (`git log`, `git diff` against the
   last known-good SHA), snapshot the union of lineages like commits `b12f52c`/`67368e3`
   did, then reconcile deliberately in a dedicated commit — never silently overwrite.
4. **Push rejected by pre-push (88):** follow the printed procedure; reconciliation is a
   deliberate human-reviewed act, not a conflict shortcut.
5. After any incident: update this file / `docs/AGENT_EXECUTION_GUIDE.md` if a new bypass
   or failure mode was found. Prevention knowledge belongs in the repo, not in memory.
