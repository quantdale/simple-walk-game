# Rotate the durable return summary instead of letting it saturate

## Why

The durable return summary is the game's flagship re-entry surface: it is the
thing a player sees after being away, and its entire design goal is a 5–15
second glance. Today it only ever **grows**. Every batch merges its new items
into the previously pending items, the merged list is sorted by item kind, and
the first 12 survive. Nothing is ever removed except by an explicit
acknowledgement.

For a player who does not tap "dismiss" — which is precisely the busy player
this product is designed for — the summary saturates within roughly the first
dozen region events. After saturation, every new Transformation item is dropped
in favour of the oldest ones, and no Production, Notice or Aggregate item is ever
shown again. The Home surface compounds this: `RequiresAttention` is true
whenever a pending summary exists, so the app permanently claims attention is
required while showing a frozen, stale summary. The player who engages least is
the one who gets the least useful information.

## What Changes

- The pending summary **rotates**: items are bounded per kind, and when a kind
  reaches its bound the oldest item of that kind is retired so newer, more
  relevant facts of the same kind can appear.
- Retirement is **kind-scoped and priority-preserving**: the documented ordering
  (transformation → actionable decision → production/notice → aggregate) is
  unchanged, and a lower-priority item is never promoted above a higher-priority
  one to make room.
- The item ordering is made **deterministically stable within a kind**, matching
  what the code already claims to do. The current sort relies on an unstable
  sort and does not actually guarantee insertion order within a kind.
- The summary carries enough provenance to distinguish "still current" from
  "historical" so the shell can render them differently, and so a retired item
  cannot silently reappear.
- The Home attention signal stops claiming attention is required for a summary
  that contains only historical items, while still surfacing a genuinely new
  fact.
- Regression tests for saturation, rotation, priority preservation, stable
  ordering across many boots, and the attention signal.

**Non-goals:** changing which events produce summary items, the wording of any
item, the 12-item hard bound itself, the acknowledgement operation, or any
canonical progression. This change alters only what the summary *retains* and
how the attention signal is derived from it.

## Capabilities

### New Capabilities

- `return-summary`: the durable, bounded re-entry contract — what a summary is,
  how items are prioritised and bounded, how it rotates as new progress arrives,
  how it survives a crash between committing and presenting, how it is
  acknowledged, and how the Home surface derives its "does this need my
  attention" signal from it.

### Modified Capabilities

<!-- None. `openspec/specs/` is empty at HEAD; `return-summary` is introduced
     here. The Home attention signal is specified here too because it is derived
     entirely from this capability's state. -->

## Impact

**Evidence from the audit**

- `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs` —
  `Compose` does `merged.AddRange(existing.Items)` and then `Finalize` takes the
  first `MaxItems` after sorting by `(int)Kind`. There is no eviction anywhere.
  The comment claims "Stable priority sort: kind order, then insertion order
  within a kind", but `List<T>.Sort(Comparison<T>)` is an unstable introsort, so
  the second half of that claim is not guaranteed.
- `src/WalkGame.Application/GameSession.cs` `GetHome()` sets
  `requiresAttention = true` whenever `game.PendingReturnSummary != null`, and
  `AcknowledgeReturnSummary()` is the only thing that clears it.
- `docs/evidence/m4/pacing-pathological.txt` shows the practical consequence: a
  single run produces 19 project completions, 20 landmark-stage events, 8
  progression events, 13 discovery unlocks, 6 expedition events and 1 region
  completion — far more Transformation-kind items than the 12-item bound, in one
  profile.
- `ReturnSummaryDurabilityTests.Composer_BoundsOutput_AndPrioritizesTransformationOverAggregates`
  proves the bound and the priority, but nothing covers what happens *after* the
  bound is reached.
- `M5H1ShellAcceptanceTests.Scenario03/04/05` assert bounded, calm returns, but
  each starts from a fresh profile, so the saturated case is never reached.

**Affected code**

- `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs` — the merge,
  ordering and eviction logic.
- `src/WalkGame.Domain/Summaries/PendingReturnSummary.cs` — item shape and the
  total bound.
- `src/WalkGame.Application/GameSession.cs` `GetHome()` — attention derivation.
- `src/WalkGame.Domain/Validation/GameStateValidator.cs` — if item shape gains
  fields, the existing pending-summary validation must accept them.
- `src/WalkGame.Application/ReadModels/ReturnSummaryReadModel.cs` and
  `HomeReadModel.cs` — expose the distinction the shell needs.

**Affected tests**

- `ReturnSummaryDurabilityTests` (5 tests), `SessionRecoveryAndSummaryTests`
  (3 tests), `M5H1ShellAcceptanceTests` (11 tests),
  `M3AmbientProgressionAcceptanceTests`,
  `M4Region1AcceptanceTests` (both assert byte-identical determinism).

**Compatibility**

Additive and save-safe: new fields on a summary item decode as absent-means-
unset, matching the established additive pattern in this schema (D-036). A
schema bump is only required if the change alters the meaning of existing
persisted bytes, which this proposal explicitly avoids.

**Dependencies**

- None. Independent of the save-recovery and activity-window changes.
