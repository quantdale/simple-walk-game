## Context

See `proposal.md` (Why) for the reproduction.

Constraints that shape the approach:

- The composer is a pure static function in the Application layer
  (`ReturnSummaryComposer.Compose(events, content, existing, generatedAtUtc)`)
  with no access to the clock, the store, or the state aggregate. Keeping it
  pure is what makes the whole "summary can never alter progression" guarantee
  cheap to hold and easy to test.
- `PendingReturnSummaryState.MaxItems = 12` is the documented glance budget and
  is already asserted by
  `ReturnSummaryDurabilityTests.Composer_BoundsOutput_AndPrioritizesTransformationOverAggregates`.
  The change must keep that exact total bound; it changes what fills the bound,
  not how big the bound is.
- `PendingSummaryItemState` currently has `Kind` and `Text` only. Both are
  serialized into the canonical save inside `GameState`, which is currently
  schema v2.
- The schema's established pattern for growing canonical state is **additive**:
  new fields decode as "nothing yet" and need no migration (D-036, used by
  `ProcessedRecords`, `PendingReturnSummary` and `LastIngestionOutcome`). The
  `SaveJson.PopulateReadOnlyCollections` modifier is what makes that safe for
  collections; scalar properties need no special handling.
- `GameStateValidator` already validates the pending summary: non-null item
  list, total count within `MaxItems`, no blank item text, and a generation
  timestamp. Any new item field must be validated consistently.
- `GetHome()` is a pure projection over canonical state; the attention signal is
  computed inline from `game.PendingReturnSummary != null` plus a queue-idle
  check. There is no separate "attention" state to keep in sync.

## Goals / Non-Goals

**Goals**

- Make the summary report *recent* progress indefinitely instead of freezing at
  first saturation.
- Make the ordering claim in the code true, or remove the claim.
- Keep the composer pure and the total bound at 12.
- Keep old saves valid without a migration.

**Non-Goals**

- Changing which events produce items, or the wording of any item.
- Changing the acknowledgement operation, or making acknowledgement mandatory.
- Adding a second summary kind, a summary archive, or a "seen everything"
  marker in canonical state.
- Any change to `docs/MASTER_PLAN.md`'s 5–15 second glance budget.

## Decisions

### 1. Per-kind retention bounds that sum to the existing total bound

Add a per-kind bound table (for example: transformation 5, actionable decision
3, production/notice 3, aggregate 1) whose sum equals `MaxItems = 12`. The
total bound is unchanged and still hard-enforced; the per-kind table decides
*which* 12 items survive.

Eviction is always the oldest item **of the kind being added to**. This
guarantees the spec's priority-preservation property structurally rather than by
convention: a lower-priority kind can never displace a higher-priority one,
because it only ever evicts from its own bucket.

*Alternative considered:* evict globally oldest-first once at 12.
Rejected: it is not priority-preserving — the first Aggregate item ever
committed would eventually evict a Transformation item, and the flagship
"the world changed" line would be the first thing to disappear.

*Alternative considered:* raise the total bound. Rejected: the bound is the
product's glance budget (`PendingReturnSummaryState.MaxItems` doc comment,
`MASTER_PLAN` §8, `GAME_SYSTEMS` §11). Enlarging it to fit more history trades
the actual product constraint for a cosmetic improvement.

### 2. Make item currency explicit and additive

Add one field to `PendingSummaryItemState` recording when the item was first
committed (reusing the summary's `generatedAtUtc` clock at compose time). Two
consumers need it:

- `GetHome()` decides "current" versus "historical" by comparing the newest item
  timestamp against a documented currency rule.
- The shell can render them differently without guessing.

The field is a scalar added to an existing persisted type. Per the schema's
additive convention it decodes as absent-means-unset, which the spec pins to
"current" — the safe direction, because a legacy summary is by definition made
of things the player has not seen yet.

*Alternative considered:* a boolean "isHistorical". Rejected: the shell will
eventually want to know *how* old, and a timestamp is strictly more
informative at no extra cost. The two existing timestamp fields on the state
already use `DateTimeOffset`, so no new serialization concern is introduced.

*Alternative considered:* derive currency purely from the summary's generation
timestamp, with no per-item field. Rejected: a single timestamp cannot express
"item 1 is from March, item 12 is from today", which is exactly the case
rotation creates.

### 3. Fix the sort rather than document around it

Replace `List<T>.Sort(Comparison<T>)` with an explicit, stable ordering: order
by kind ordinal, then by insertion sequence. The simplest stable formulation is
to sort a list of `(item, insertionIndex)` pairs by `(kind, insertionIndex)`.

*Alternative considered:* keep the sort and weaken the comment.
Rejected: the claim is load-bearing for reproducibility. An unstable sort means
which 12 items survive the bound is not obviously a function of the event
sequence, and this repository's determinism discipline is that it must be.
`List.Sort` is deterministic for a fixed input, so nothing is *currently*
non-reproducible — but the guarantee is incidental, not stated, and the code
says otherwise.

### 4. Home attention uses currency, not existence

`GetHome()` changes from "a summary exists" to "a summary exists and contains
at least one current item". A summary that is entirely historical reports
`HomeAttentionReason.None` and `RequiresAttention = false`, while
`HasPendingSummary` stays true so the surface can still offer to read it.

The queue-idle-with-banked-Vitality branch is deliberately **not** gated on
currency: banked Vitality with an empty queue is a live decision the player can
still act on, and that behaviour is already covered by
`M5H1ShellAcceptanceTests.Scenario06_QueueEmptyWhileAway_ExplicitReasonAndNextAction`.

The currency rule itself (how old is "historical") is a single documented
threshold chosen by the implementer and recorded in `docs/UX_DESIGN.md`; the
design deliberately leaves the number open because it is a product-calibration
value, not an architectural one.

### 5. No schema bump

Nothing about the meaning of existing persisted bytes changes. New field
absent-means-current. The summary is also self-healing: it is regenerated on
every commit that produces events, so a stale one is replaced rather than
migrated. `SchemaVersions.Current` stays 2 and `DefaultMigrations` is unchanged.

*Alternative considered:* bump to schema v3 to make the rotation policy
explicit. Rejected: a schema bump buys nothing when the change is additive and
absent-decodes to the safe default, and every schema bump costs a registered
migration plus a mature-save fixture.

## Risks / Trade-offs

- **[Per-kind buckets mean a player with a burst of discoveries can never see
  all of them]**
  → Intended. The bound is the product's attention budget; the rest remain
  visible in the Discoveries screen, which is the archival surface for
  exactly that. The summary is a re-entry signal, not a log.

- **[Rotation silently drops a fact the player may care about]**
  → Rotation is strictly newest-of-kind-wins, so a fact is dropped only after
  N newer facts of the same kind exist. That is the correct trade for a
  glance surface, and the archival surfaces (Discoveries, Region, Projects)
  remain complete.

- **[The currency threshold is a judgement call]**
  → Pinned by tests at the boundary and recorded in `docs/UX_DESIGN.md`, so a
  later retune is a one-constant change with a documented reason, not a
  redesign.

- **[A legacy summary is treated as fully current, so attention is demanded once
  after upgrading]**
  → Correct and desirable: the player genuinely has not seen it. One extra
  "look at this" is strictly better than silently dropping pre-upgrade content.

- **[Changing the item shape touches the save payload]**
  → `SaveCodecRoundtripTests.Encode_Decode_PreservesEveryField` must be extended
  to cover the new field, and `MatureSaveMigrationTests` /
  `M4BackwardDecodingTests` must still pass unchanged, which is the proof that
  no migration is needed.

## Migration Plan

No migration. Additive field with a safe default, verified by the existing
pre-M4/backward-decoding suites continuing to pass.

Rollback is a plain revert; payloads written by the new build decode under the
old build with the field ignored.

## Open Questions

None. The currency threshold is a calibration constant assigned explicitly in
the task list; it does not change the shape of this design.
