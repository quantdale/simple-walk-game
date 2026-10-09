## Context

See proposal.md for why a glance is missing. The facts already exist: `GameSession.GetHome()` classifies attention, `GetPendingReturnSummary()` carries bounded current items, `GetOnboarding()` carries the stage, and `ActivityStatusProjector` classifies the source. `GetActivityStatus()` throws `InvalidOperationException` when no `IActivityConnectionPort` is configured, which is the normal simulation-CLI composition. Domain has no copy and must stay that way. `scripts/verify-architecture.sh` enforces that layering.

## Goals / Non-Goals

**Goals:**

- One application projection, `GlanceReadModel`, produced by a pure function and exposed as parameterless `GameSession.GetGlance()`.
- Landmark-stage wording shared by that projection and by newly composed summary lines.
- A `glance` verb that prints the projection.

  **Corrected 2026-10-09 (post-review):** this line originally read "and does not write the
  save", which was false. The verb boots through the shared boot flow, which persists —
  exactly as `ack`, `dump` and `validate` already did. The projection and `GetGlance()`
  perform no save of their own; see the spec amendment and D-065. A read-only boot is a
  separate change.
**Non-Goals:**

- A second attention classifier, a localization catalog, a schema bump, or any change to `Out.HomeLine`, `dump`, `profile`, `validate`, or pacing output.
- Rendering. Unity remains UNVERIFIED.

## Decisions

### 1. Project in Application, from existing reads

Add `src/WalkGame.Application/ReadModels/GlanceReadModel.cs` and `GlanceProjector.cs`. `GameSession.GetGlance()` gathers `GetHome()`, `GetPendingReturnSummary()`, `GetOnboarding()`, the content graph, and an activity snapshot, then calls `GlanceProjector.Project`. It does not save, acknowledge, or ingest.

Activity status: if `_connectionPort` is null, call `ActivityStatusProjector.Project(null, hasProcessedAnyRecord, lastOutcome, lastBatchVitality, lastProcessedAtUtc)`. That existing null path is `SourceUnavailable`. Do not call `GetActivityStatus()` from `GetGlance()`, and do not change `GetActivityStatus()`'s throw. The CLI can then run `glance` on a session constructed without a port.

`GetGlance()` stays parameterless so `EveryReadModelSurface_IsSweptByReflection_AndOnlySupportSurfacesMayCarryAdapterDetail` picks it up. Also add it to the hand list in `PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads`.

Alternative considered: put the phrase table in Domain next to `RestorationStage`. Rejected. Domain must not grow player copy, and the architecture gate would be the wrong place to learn that.

Alternative considered: extend `HomeReadModel` with sentences. Rejected. Home is the fact snapshot the attention tests already pin. A separate model keeps those assertions stable.

### 2. Attention is a switch on `HomeAttentionReason`

`GlanceProjector` reads `HomeReadModel.AttentionReason` and `RequiresAttention`. It does not re-derive them from queue, vitality, or completion. Sentences are the ones in `specs/player-glance/spec.md`.

Primary action:

- `QueueEmptyWithBankedVitality` → `"Choose a restoration project."`
- `PendingReturnSummary` and `HomeReadModel.PrimaryNextAction` is non-null → that existing string (`"Queue the next restoration project."`)
- every other reason → null

A finished region never receives a queue action, even if a stale summary string were present. `RequiresAttention` is copied from `HomeReadModel`, not recomputed.

### 3. Closed phrase table, shared, not parsed loosely

`GlancePhrases` (same file as the projector, or a sibling static class) maps `RestorationStage` to the five phrases in the spec. Newly composed lines and glance lines both call `FormatLandmark(title, stage)` → `"{title} {phrase}."`.

`ReturnSummaryComposer`'s `LandmarkStageReached` arm is the only composer edit. It calls that formatter instead of `" has reached {stage}."`. Other summary lines stay as they are. No save migration. `SaveCodecRoundtripTests` keeps the stored historical sentence.

Read-time substitution is exact, not fuzzy. If a current summary item's text equals `"{known landmark title} has reached {StageName}."`, the glance renders `FormatLandmark` for that title and stage and does not write the substitution back. Any other stored text is used only when it contains none of the stage identifiers as whole words. If it does, the glance lead falls back to `"The valley changed."` rather than echoing the identifier. That fallback is how the historical-fixture scenario stays true without a migration.

Lead selection uses current items only (`ItemRow.IsCurrent`). Lowest `SummaryItemKind` ordinal wins; ties keep composer order. If another current item exists, `FurtherChangeCount` is that remainder and the printed extra sentence is `"{n} more changes are waiting."` Otherwise the count is 0 and no extra sentence is printed. An aggregate is never selected while a current transformation exists.

Restoring-now:

- active project → `"{title} is underway ({invested} of {cost})."`
- else first queued title → `"Next up is {title}."`
- else region complete and a post-completion program exists → `"{program title} continues ({invested} of {cost} this cycle)."` using `PostCompletionProgramDefinition.TitleKey` and `HomeReadModel`'s care invested/cost fields
- else → `"Nothing is being restored right now."`

Identifiers: if a title is missing, use `"A restoration project"` rather than the id. Do not print `lm.*` or project ids.

Onboarding baseline landmark: first landmark in content order whose current stage is `Ruined`; if none, the first landmark. Sentence is `FormatLandmark`. When `OnboardingNextAction` is `None`, the onboarding sentence is null.

### 4. CLI prints and does not save

Add `"glance"` to the switch in `tools/simulation/Program.cs` and `Cli.Glance`
in `Commands.cs`. It loads the save the way `ack` does, calls `GetGlance()`, prints the
sentences, and returns.

**Corrected 2026-10-09 (post-review):** the original line read "It must not call save, ack,
or ingest." That is still true of the glance surface — `GetGlance()` performs no save
operation of its own, which is what `ReadingAGlancePerformsNoSaveOperation` pins — but
it is false if read as a claim about the VERB: booting via `Boot` → `Continue` →
`FinishBoot` persists before presenting. The spec requirement was amended to this
truer contract rather than left unmet.

Add the verb to `Usage`. Do not edit `Out.HomeLine` or `Out.HomeBlock`.

### 5. Record the decision, do not claim a screen

Add D-065 to `docs/DECISIONS.md`: the glance is a headless sentence projection; stage phrases are the application table above; historical summary text is not migrated; Unity rendering stays UNVERIFIED. Add a short binding note to `docs/UX_DESIGN.md` under the M5-H1 section. Do not mark ROADMAP M5 exit criteria done.

## Risks / Trade-offs

- [Composer text change breaks a test that asserts the old landmark sentence] → Update that assertion to the phrase form. Do not delete the test. The historical round-trip fixture must stay on the old sentence.
- [`GetGlance` throws on a port-less CLI session] → Null-snapshot path above. A test constructs `GameSession` without a port and reads the glance.
- [Reflection sweep starts failing because a sentence contains the hostile marker] → Sentences are fixed templates plus titles and integers. No exception message is interpolated. The sweep is the proof.
- [Exact substitution misses a future composer wording] → New compositions go through `FormatLandmark`, so they do not depend on substitution. Substitution exists only for already-stored `has reached` lines.
- [Phrase table drifts from the spec] → Tests pin each stage's sentence, including `Old Trailhead is safe to approach.`
- ["is restored" contains the lowercase word restored] → The forbidden check is the enum spelling as a whole word (`Restored`), not a case-insensitive search.

## Migration Plan

No schema change, no `ISaveMigration`, no evidence regeneration. Rollback is reverting the change. Existing saves keep their summary text. New landmark advances write the phrase form from the deploy onward.

## Open Questions

None. The sentence table and the decision not to migrate historical text are specified. Changing either would be a new change, not an implementation choice.
