## Context

See `proposal.md` (Why) for the evidence.

Constraints that shape the approach:

- There are **two independent version axes** in this codebase and the design
  must not conflate them:
  - **Payload schema** (`SchemaVersions.Current = 2`, `SaveCodec`,
    `ISaveMigration`, `MigrationRunner`, `DefaultMigrations`, D-027/D-032). This
    migrates the *shape* of the payload.
  - **Authored content** (`RegionDefinition.ContentVersion`). This changes the
    *meaning* of identifiers and costs inside an unchanged payload shape.
  Conflating them would mean every content edit forces a migration, which is
  exactly the "migration churn" the repository's D-036 additive pattern exists
  to avoid.
- The fail-closed posture is already correct and must be preserved:
  `GameStateValidator` reports unknown runtime project, discovery and expedition
  IDs, and the boot then fails. `MatureSaveMigrationTests` proves it.
- Region 1 is the only authored content and is a static, immutable graph built
  once per process by `Region1Catalog.Create()`. There is no content loader, no
  asset pipeline and no content file format. Authoring a tombstone set is
  therefore a code change in the catalog, not a data-file change — which means
  there is no "content shipped in the wrong build" failure mode to guard, only
  "content changed between builds".
- `GameSession` validates content in its constructor via
  `ConditionalWeakTable<RegionDefinition, IReadOnlyList<string>>` and throws
  `ArgumentException` on violations. A new content-level validation rule is
  therefore automatically enforced at every composition root, including the CLI.
- `scripts/verify-ci-steps.sh` executes every `run:` step of `.github/workflows/ci.yml`
  verbatim. A new gate is only enforced if it is *inside* the workflow file; a
  standalone script that CI does not call is not a gate. This is documented
  explicitly in `verify-ci-steps.sh`'s own header and is why D-049 exists.

## Goals / Non-Goals

**Goals**

- Make the save↔content version relationship observable and checkable.
- Give removed content a resolution path so a rename or removal cannot
  unrecoverably brick a profile.
- Detect content drift at a gate, before a player's first boot.
- Keep the payload schema version untouched.

**Non-Goals**

- A general content-migration framework, or translating one content version's
  costs into another's.
- Retrofitted `ISaveMigration` steps.
- Any change to Region 1's identifiers, costs, or graph.
- Region 2 content.

## Decisions

### 1. The recorded content version is the *creation-time* version, not the current one

`GameState` gains an additive `AuthoredContentVersion` field, written once by
`GameFactory.NewGame` and never updated by later commits. Recomputing it on every
commit would make the field answer "what is this build" rather than "what must
this save still resolve against", which is the only useful question.

Absence decodes as "unknown" and is evaluated as the oldest supported content
version, so every save in existence today keeps loading without a migration.

*Alternative considered:* store the current content version and update on every
save. Rejected: it destroys the information needed to detect drift, and it makes
the field change on every single commit for no benefit.

*Alternative considered:* bump `SchemaVersions` to 3. Rejected: the payload
shape does not change. D-036 established that additive fields need no migration,
and `M4BackwardDecodingTests` exists precisely to prove pre-existing payloads
decode correctly without one.

### 2. Compatibility is a three-way classification, not a boolean

`ExactMatch` / `CompatibleOlder` / `NewerThanBuild`. The middle case is the
important one: an older save is *not* automatically safe, it is safe **if every
identifier it references still resolves** — which is where tombstones and the
existing unknown-ID validation do the real work. The version comparison is a fast
pre-classification that produces a good diagnostic; the validator remains the
authority on whether the state is actually loadable.

*Alternative considered:* reject any version mismatch. Rejected: that would make
every content addition require a migration, which is precisely the churn the
additive pattern avoids, and would break the M4 precedent of adding discoveries
and expeditions additively under an unchanged schema version.

### 3. Tombstones are authored code, deliberately

A tombstone set on `RegionDefinition` is a code change in `Region1Catalog`, not
a data file. This is the right trade at this stage: there is no content pipeline,
the catalog is already code, and making retirement a code review is the strongest
possible guard against accidental removal.

A tombstone resolves a reference to a **retired form**, not to nothing. In
practice this means: a completed retired project still reads as completed, a
retired discovery still appears in the journal as discovered, and the content
validator resolves the ID against the tombstone instead of flagging it unknown.
Nothing is deleted and nothing is re-awarded.

*Alternative considered:* rewrite the reference to a "retired projects" bucket
in state. Rejected: that is a payload shape change, would need a migration, and
would lose the player's specific history.

*Alternative considered:* keep failing closed and merely improve the diagnostic.
Rejected: a better error message is not a recovery path. The product thesis
(MASTER_PLAN §3.6, D-004) is that long-term progress is never destroyed.

### 4. Content validation gains a live/tombstone collision rule

The cheapest possible guard against a tombstone hiding live content: an
identifier cannot be both. Plus the two drift rules the spec requires — a live
project may not depend on a tombstoned prerequisite, and a content change that
alters save meaning requires a version bump.

The last rule needs a comparison against the previous content, which the
validator cannot see. It therefore belongs in the **gate**, not in
`ContentValidator`: the gate compares the shipped content version against the
version recorded in the repository's retained mature-save fixtures.

### 5. The drift gate lives inside `ci.yml`, not beside it

Add a step to `.github/workflows/ci.yml` that runs the shipped content against
the retained save fixtures and fails on any unresolved identifier. Because
`verify-ci-steps.sh` executes the workflow file verbatim, the gate is then
enforced locally too — the property D-049 was created to guarantee.

*Alternative considered:* a standalone script that developers run manually.
Rejected: the repository has already been bitten by exactly this (the pacing and
save-growth evidence existed before they were gated, and drifted silently).

### 6. CLI surfaces the verdict, because the CLI is the release evidence tool

`validate` already decodes and validates against `Region1Catalog`; it should also
print the content-compatibility verdict. `dump` should print the recorded
content version. Both are one-line additions to existing output and make the
gate's result inspectable by hand.

## Risks / Trade-offs

- **[A tombstone could be used to retire content that should still be
  reachable]**
  → Two guards: a tombstone may not collide with a live definition, and a live
  project may not depend on a tombstoned prerequisite. The gate additionally
  fails if a retained fixture stops resolving. Retirement is a deliberate,
  reviewed, code-level act.

- **[Retired content accumulates indefinitely in the catalog]**
  → Accepted deliberately, and bounded by the fact that the MVP ships one region
  with a fixed roster. The master plan records tombstone pruning as a
  pre-Region-2 decision, not as work for this change.

- **[A content edit made without a version bump is now a gate failure]**
  → That is the point, but it is a *process* change and will surprise anyone
  editing `Region1Catalog`. Mitigation: the gate's failure message must name the
  fixture and the specific drift, and `docs/AGENT_EXECUTION_GUIDE.md` gets a
  short "changing authored content" section. This is a real ergonomic cost
  accepted in exchange for detecting the failure class.

- **[Content drift could be detected only after a player's first boot in the
  worst case]**
  → The gate makes the CI-visible case impossible. A player who updated through
  an intermediate build with drift in it would still hit a fail-closed boot on
  the next build; the tombstone mechanism exists to make that recoverable rather
  than to prevent it in every hypothetical ordering.

- **[Adds a field to the canonical payload]**
  → Additive, absent-means-unknown, no schema bump. `SaveCodecRoundtripTests`
  is extended and `M4BackwardDecodingTests` plus `MatureSaveMigrationTests` must
  pass unchanged, which is the proof.

## Migration Plan

No `ISaveMigration` is added and `SchemaVersions.Current` stays 2.

Rollout order matters because the gate is new:

1. Add the field, the classification and the tombstones with an empty
   tombstone set. Nothing changes behaviourally for existing saves.
2. Add the validator rules and the CLI output.
3. Add the CI gate step last, so the gate is introduced against content that is
   already known to satisfy it.

Rollback: a plain revert. Payloads written by the new build decode under the
old build with the extra field ignored, because STJ ignores unknown members by
default and no consumer requires it.

## Open Questions

None. The tombstone set stays empty for Region 1 as authored today — this change
introduces the mechanism and its guard, it does not retire anything. Whether and
when to actually retire content is a future content decision, recorded in the
master plan rather than as an open question here.
