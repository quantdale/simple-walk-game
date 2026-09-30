# Agent Execution Guide

## 1. Purpose

This repository is intended to support large autonomous or semi-autonomous development sessions. Agents should make **substantial integrated progress** while preserving architectural boundaries, state integrity, and release evidence.

The objective is not to maximize changed-file count. The objective is to complete coherent engineering campaigns safely.

---

## 2. Read-before-write order

Before modifying code, an agent should review at minimum:

1. `README.md`;
2. `docs/MASTER_PLAN.md`;
3. `docs/ROADMAP.md`;
4. the domain-specific document for the planned work;
5. `docs/TECHNICAL_ARCHITECTURE.md`;
6. `docs/TESTING_AND_RELEASE.md`;
7. recent commit history;
8. open issues/PRs if they exist;
9. the relevant implementation and tests.

An agent must determine the repository’s **actual current state** before selecting work.

---

## 3. Campaign selection rule

Choose the single highest-value development campaign that:

- builds directly on completed work;
- resolves the next dependency bottleneck;
- spans enough related work to justify a long session;
- leaves the repository in a coherent state;
- does not bypass critical correctness work for visible feature expansion.

Good campaign examples:

- activity ledger + dedup + persistence recovery + fixtures + tests + diagnostics;
- project queue + offline advancement + return summary + UI + tests;
- save versioning + migrations + corruption recovery + upgrade fixtures + docs;
- Region 1 content graph + validator + simulation + presentation bindings.

Bad campaign examples:

- add one icon;
- rename random files;
- implement Region 2 before Region 1 is qualified;
- add a backend because it seems “production-like”;
- add visual effects while canonical state integrity is broken.

---

## 4. Planning requirement

Before implementation, write an internal campaign plan containing:

- current repository state;
- target milestone;
- dependencies;
- scope;
- files/subsystems likely affected;
- invariants at risk;
- migration implications;
- tests to add/change;
- device/runtime verification required;
- explicit non-goals;
- completion criteria.

Do not spend the entire session planning. Planning exists to prevent architectural drift and duplicated work.

---

## 5. Default priority order

Unless evidence says otherwise:

1. data corruption/privacy/security defects;
2. activity double-credit/loss defects;
3. migration/recovery defects;
4. build/test breakage;
5. core progression blockers;
6. lifecycle/permission failures;
7. severe accessibility/performance defects;
8. missing integration needed for current milestone;
9. UX polish needed to complete the vertical slice;
10. new content/features.

Visible feature count is not the priority system.

---

## 6. Architectural guardrails

Agents must preserve:

- pure C# domain boundary;
- canonical state outside Unity presentation;
- application orchestration boundary;
- platform adapters behind interfaces;
- durable stable IDs;
- injected clock/RNG;
- versioned persistence;
- deterministic/idempotent reward processing;
- explicit content definitions separate from runtime state.

Do not create a global `GameManager` that becomes the real architecture.

---

## 7. Activity integrity guardrails

Any change touching activity or progression must answer:

- Can this operation run twice?
- What happens after a crash between calculation and save?
- Can source queries overlap?
- Can records arrive late?
- Can a correction happen?
- What stable identity prevents duplicate reward?
- Can checkpoint state advance without durable reward state?
- Is the conversion rule versioned?

If these questions are unanswered, the change is incomplete.

---

## 8. Persistence guardrails

Any persisted-field/schema change requires:

- schema version consideration;
- migration implementation if needed;
- migration fixture/test;
- old-save compatibility decision;
- recovery behavior;
- documentation update.

Never silently reinterpret an existing persisted field.

Never overwrite the final recoverable copy before migration/validation completes.

---

## 9. Time guardrails

Do not introduce direct uncontrolled wall-clock calls into domain logic.

Use injected time.

Any offline/time change should test:

- zero elapsed time;
- long elapsed time;
- backward clock;
- time-zone change;
- repeated resume;
- same-timestamp ordering.

---

## 10. Randomness guardrails

Canonical randomness must use persisted deterministic seeds/state.

Do not allow:

- save/reload rerolls;
- UI animation randomness to determine rewards;
- retrying an operation to roll again;
- hidden random modifiers in activity conversion.

### Dormant surface: canonical randomness has no consumer today (D-057)

The policy above is correct and enforced. The **implementation** is currently inert, and
that is recorded rather than papered over:

- `DeterministicRng` is complete, persisted in `GameState.Rng`, round-tripped by the save
  codec, printed by `dump` and unit-tested — and **nothing in `src/` or `tools/` draws
  from it**. Grepping `NextUInt64|NextInt64|NextDouble` returns only the definitions.
- Consequently `StartNewGame(ulong seed)` **cannot currently affect any outcome**. Two
  profiles run with different seeds reach identical canonical state, and
  `SeedDoesNotAffectCanonicalStateTests` pins exactly that.
- A property test's use of the generator as a **data generator** is NOT canonical
  randomness. `SeededProgressionPropertyTests` names its local `testDataRng` for this
  reason and is the single most misreadable reference in the repository.

Rules that follow from this, and they are the point of recording it:

- **Do not add a random mechanic to justify the existing generator.** Inventing a feature
  to excuse a dependency is worse than a documented unused one.
- **The first canonical consumer must add an end-to-end determinism test** covering the
  save/load round trip. That will be the first time `Rng` is read, the first time the
  seed matters, and the first opportunity to regress determinism. It must also revisit
  seed semantics deliberately rather than inheriting whatever the code happens to do.
- A general rule this repository has now been bitten by three times: **a mechanism that
  reads as live is not evidence that it is live.** Ask what a guarantee *protects*, and
  whether any production path *exercises* it. Dormant surface gets a comment, a test, or
  removal — never silence.

---

## 11. UI implementation rules

Every core screen/feature should include:

- normal state;
- loading state;
- empty state;
- error state;
- offline state where relevant;
- restart/re-entry behavior;
- accessibility semantics;
- reduced-motion behavior if animated;
- stable test selectors/semantics where practical.

UI must invoke application operations rather than edit save/domain objects directly.

---

## 12. Attention-budget review

Before accepting a new mechanic, ask:

- Does it require the player to open the app more often?
- Does it waste progress if the player does not claim something?
- Does it create a repeated maintenance chore?
- Does it make the game worse after several days away?
- Can automation remove the busywork?
- Is this action actually meaningful?

If a mechanic increases foreground obligation, it requires strong justification.

---

## 13. Testing requirement

Every campaign should add the tests appropriate to its risk.

Minimum expectation:

- domain behavior → pure tests;
- orchestration → application tests;
- serialization/migration → persistence fixtures/tests;
- Unity presentation → EditMode/PlayMode where useful;
- platform feature → real-device verification before claiming device-ready.

Do not use manual playtesting as a substitute for deterministic invariants.

---

## 14. Failure-driven development

For high-risk systems, explicitly test failure paths before calling work complete.

Examples:

- interrupted save;
- duplicate activity;
- invalid content;
- permission denied;
- source unavailable;
- empty queue;
- world scene load failure;
- migration exception;
- app restart mid-operation.

A robust feature has designed failure behavior.

---

## 15. Scope control

During a campaign, agents may fix adjacent issues when they are:

- prerequisites;
- regressions caused/exposed by the campaign;
- architectural debt that materially blocks completion;
- missing tests required to trust the result.

Agents should not opportunistically expand into unrelated features.

Use the roadmap, not novelty, to decide what belongs.

---

## 16. Documentation requirement

Update documentation in the same campaign when implementation changes:

- architecture;
- persisted state;
- public/internal system behavior;
- milestone status;
- platform limitations;
- performance evidence;
- verification status;
- known risks.

Never leave docs intentionally describing behavior that no longer exists.

---

## 17. Verification vocabulary

Use precise language in reports:

- **implemented** — code exists;
- **automated verified** — named tests passed;
- **runtime verified** — exercised in relevant runtime;
- **device verified** — tested on named physical device/platform;
- **unverified** — no adequate evidence yet.

Do not claim “fully working” from code inspection alone.

---

## 18. Commit strategy

For long campaigns, prefer coherent checkpoints.

A useful sequence may be:

1. domain/state foundation;
2. integration/persistence;
3. presentation/content;
4. tests/hardening;
5. documentation/evidence.

Each commit should remain understandable and should not intentionally leave mainline state corrupt at the final campaign boundary.

The final repository state must be clean.

---

## 19. Branch/PR policy

When working directly under an autonomous campaign, follow the user/session instruction for branch strategy.

If no strategy is supplied:

- use a dedicated branch for risky multi-file implementation campaigns;
- keep documentation-only bootstrap changes simple;
- do not overwrite unrelated active branches;
- do not force-push shared work without explicit reason.

Always report branch and final SHA.

---

## 20. End-of-campaign report

Every substantial campaign should report:

### Repository state

- starting SHA;
- final SHA;
- branch;
- push status;
- working-tree cleanliness.

### Delivered

- major capabilities;
- important fixes;
- migrations;
- tests;
- docs.

### Verification

- exact automated commands/suites;
- runtime checks;
- device checks;
- performance measurements if applicable.

### Known gaps

- unresolved Medium/Low defects;
- unverified platform behavior;
- deferred work;
- blockers.

### Recommended next campaign

One substantial next campaign grounded in the new repository state.

---

## 21. Stop/continue criteria

An autonomous long session should continue while:

- planned campaign work remains;
- tests are actionable;
- adjacent fixes are clearly part of the campaign;
- repository can be improved without violating constraints.

Do not stop merely because one feature works.

Do stop expanding scope when:

- the campaign exit criteria are satisfied;
- the next work belongs to a different milestone;
- missing device/manual access prevents honest verification;
- a destructive ambiguity cannot be safely resolved from repository evidence.

In that case, leave the code safe, document the gap, and report the exact blocker.

---

## 22. Definition of a strong autonomous session

A strong session does not mean “many files changed.” It means:

- one meaningful milestone moved substantially closer to completion;
- high-risk invariants became better protected;
- implementation, tests, migrations, UX, and docs stayed aligned;
- technical debt did not increase invisibly;
- the next agent can understand the resulting state quickly;
- verification claims are precise;
- the repository ends buildable and reviewable.

---

## 23. Incident prevention — wrong repositories and concurrent writers

This repository has actually suffered both failure classes; the mechanisms below are
mandatory, not advisory. The binding contract lives in root `AGENTS.md`; this section
explains why.

**Wrong-repository execution.** An agent intended for the sibling repository
`quantdale/walk-game` (a different product with its own history and campaigns) once had
no mechanical barrier against operating here. Folder names are not identity.
*Prevention:* `scripts/assert-repo-identity.{sh,ps1}` must print OK before any write
(exit 86 otherwise); CI re-checks under GitHub's own `GITHUB_REPOSITORY`. If you discover
mid-session that identity fails: stop writing, change nothing, report to the operator.

**Same-worktree concurrent writers.** Two executor sessions once wrote this tree
simultaneously and interleaved/deleted each other's lineage (see commits `b12f52c`,
`67368e3`). *Prevention:* acquire `scripts/writer-lease.{sh,ps1}` before your first write;
if it reports busy (exit 87), STOP or move to your own worktree via
`scripts/new-agent-worktree.sh` (one writer = one worktree = one branch). Stale locks are
recovered only by an explicit human override — never silently.

**Stale prompts.** An execution prompt marked ACTIVE describes intended state, not
current state. Always reconcile its claims against README/ROADMAP/tests before resuming,
and planners must mark superseded prompts SUPERSEDED (see `.agent/EXECUTION_PROMPT.md`).

**Remote races / lost updates.** Another session may push while you work. Before any
integration: `git fetch origin`, compare against your recorded starting SHA, inspect
incoming commits for overlap, reconcile deliberately, re-run full verification. The
pre-push hook mechanically refuses pushes that would discard remote commits (exit 88).
Force-push, hard reset to remote state, and `git clean -fdx` are forbidden conflict
shortcuts without explicit operator authorization.

**Safe recovery after damage.** Preserve evidence first (`git log`, diffs against the
last known-good SHA); snapshot unions of conflicting lineages in dedicated WIP commits
like `b12f52c`/`67368e3` did; reconcile in a deliberate, reviewed commit; add regression
coverage for whatever allowed the incident. Prevention knowledge belongs in these docs,
not in anyone's memory.

---

## 21. Changing authored content

A change to the authored content graph — an ID, a name, a cost, a removal — is a change
to the *meaning* of every save that references it. Canonical state fails CLOSED on unknown
content, so an unreviewed content change does not degrade gracefully: it makes existing
saves unloadable, and the one-generation backup holds the same invalid content so it fails
too (D-059).

Before changing authored content:

- **Prefer keeping a stable ID.** Renaming a *title key* is free; renaming an *ID* is a
  save-meaning change.
- **If an ID must be retired, add a tombstone.** `ContentTombstone(id, reason,
  retiredInContentVersion)` keeps an existing save's reference RESOLVABLE. The reason is
  mandatory and non-empty: an unattributed silent removal is exactly the change this
  mechanism exists to make reviewable.
- **A tombstone grants nothing.** A retired project may not sit Active, a retired producer
  may not unlock, a retired expedition may not be recorded as completed. "It still loads"
  is not "it still works" — and it must not be.
- **Bump `AuthoredContentVersion`** when authored meaning changes. It is recorded once, at
  profile creation, and is how a save-newer-than-this-build is detected at boot.
- **Run the content-drift gate.** `scripts/verify-content-compatibility.sh` proves the
  shipped content still resolves mature saves, and it runs in CI. A content change that
  breaks it fails the build; that is the point.

```bash
scripts/verify-content-compatibility.sh
```

Note the two version axes, because conflating them produces the wrong fix: the **payload
schema** (`SchemaVersions`, migrated by the `ISaveMigration` chain) is the wire format; the
**authored content version** (`AuthoredContentVersion`) is the graph the payload refers to,
and is never migrated. If a save is rejected, check which axis rejected it before acting —
`dotnet run --project tools/simulation -- validate --save <dir>` prints both.
