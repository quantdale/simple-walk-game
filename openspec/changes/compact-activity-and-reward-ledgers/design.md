## Context

See `proposal.md` (Why) for the measured series.

What makes this safe rather than dangerous:

- The exactly-once guarantee does **not** depend on retaining every record
  forever. It depends on two things: a stable identity scheme, and a durable
  answer to "have I already credited this?". Retirement preserves both, because
  a retired record's identity remains recorded in a much smaller form — the
  question becomes "was this identity retired?" rather than "what did this
  record contain?".
- `GameStateValidator` already cross-checks
  `ProcessedRecords.TotalVitalityCredited <= Ledger.TotalVitalityCredited`. That
  cross-check is the natural place to also verify a maintained aggregate, and it
  already runs on every boot — no new gate is needed.
- Both structures are mutated only through a small number of methods
  (`Record`, `Update`, `Apply`, `ApplyCorrection`). Retirement is a new method
  on those same types, not a new subsystem.
- `ProcessedRecordLedgerState.UnappliedReversalVitality` establishes the
  precedent this change follows: a durable scalar that records something the
  history can no longer show, so trust impact stays visible forever.
- The additive schema convention (D-036) means a new retirement-bookkeeping
  field decodes as "nothing retired" on every existing save, with no migration.

What constrains the design:

- Retirement must be **deterministic** — the same inputs must retire the same
  records in the same order, or byte-identical determinism (a hard repository
  invariant, asserted by both acceptance suites) breaks. Retiring "the oldest N"
  is deterministic; retiring "whatever iterates first" is not, because
  `Dictionary` enumeration order is an implementation detail.
- The trust suite pins very specific numbers. `ActivityRedTeamTests`,
  `SessionIngestionTests` and `MatureSaveMigrationTests` assert exact credited
  totals, exact processed-record counts, and exact watermark values. Any change
  that alters what a test observes is a change to that test's intent and must be
  handled deliberately, not silently.
- `ProcessedRecordLedgerState.Entries` is a `Dictionary<string, ...>` keyed by
  identity. Identity strings are `rec1|...` or `fpt1|<64 hex chars>`. Retirement
  therefore cannot be a simple "drop the first N iterated" operation.
- Both `TotalVitalityCredited` properties are `foreach` loops over their
  collections and are called from the validator on **every** boot. Making them
  O(1) is a mechanical win that also removes a boot-time O(n) cost that grows
  forever.

## Goals / Non-Goals

**Goals**

- Make canonical state converge to a documented bound instead of growing
  linearly forever.
- Keep the exactly-once guarantee provably intact under retirement, with the
  proof in the red-team suite rather than in a comment.
- Remove the O(n) recomputation from the boot path and from diagnostics reads.
- Turn the existing growth series into a convergence gate.

**Non-Goals**

- Changing the identity scheme, the conversion rule, or the reconciliation
  window.
- Compressing, encrypting or otherwise changing the save envelope format.
- Reducing the size of any individual retained record.
- Any claim that the state size becomes constant; it becomes **bounded**, which
  is the honest and achievable goal.

## Decisions

### 1. Retire by identity-recency against a durable watermark, not by count

The record most safe to retire is the one furthest behind the reconciliation
watermark, because the pipeline can no longer legitimately be asked about it:
the acceptance window is anchored to the watermark, so records ending before the
window's lower bound are already rejected before deduplication ever runs.

This gives retirement a **principled** rule rather than an arbitrary one, and
it composes with the trusted-time anchor from the sibling change: retirement and
acceptance share the same reference point, so nothing can be retired that would
still be accepted.

The processed-record ledger additionally records `ProcessedAtUtc` per entry,
which gives a deterministic, content-independent ordering key.

*Alternative considered:* retain the newest N records by count. Rejected: it is
simpler but arbitrary, and a profile with a long silent period would retire
records the pipeline would still be willing to accept. The watermark-relative
rule is self-correcting.

*Alternative considered:* retire only zero-credit entries. Rejected: on the
authoritative records a player is the ones that credit, so this would not bound
the dominant growth at all.

### 2. Retirement is an amortized operation inside an ordinary commit

Retirement runs on the commits that push the ledger past its bound, using the
same "retire up to a batch size, then stop" shape the codebase already uses for
note-reading and capacity management elsewhere. This keeps any single commit's
work bounded, which matters because commit latency is on the player's critical
path (the app is opened, credited, and closed).

Determinism is preserved because the *selection* is deterministic (watermark-
relative ordering with a stable tiebreak) even though the *cadence* is
opportunistic: the same input sequence retires the same records, just possibly
on a different commit than before. Byte-identical determinism is asserted over
the same input sequence, so this holds.

### 3. Retired identities are kept as a compact durable set

A retired record leaves the full `ProcessedRecordEntry` form and leaves a
minimal identity-only marker. That is what makes "was this retired?" answerable
in O(1) without keeping the payload, and it is what guarantees a retired record
can never be re-credited.

This is the same trade `rec1`/`fpt1` identity prefixes already make: the identity
is the trust anchor, and the payload is the audit detail. Retirement drops the
audit detail for records the pipeline can no longer be asked about, and keeps
the trust anchor forever.

### 4. Maintain lifetime aggregates incrementally, cross-check on load

`ProcessedRecordLedgerState` and `RewardLedgerState` each gain a durable
maintained total, updated in the same method that changes the collection. The
O(1) property then holds for `GetDiagnostics()` and for the validator.

The validator gains a **new** cross-check: the maintained total must equal the
full recomputation over retained history plus the durably recorded retired
total. This is strictly stronger than today's check, and it is what makes
trusting the maintained value safe — a bug that corrupts the aggregate is caught
on the next boot rather than silently reported.

*Alternative considered:* cache the recomputation in memory only. Rejected: it
would still be O(n) on the first read after every boot, which is exactly the
cost being removed.

### 5. Keep the reward-ledger index alive across the state transition

`RewardLedgerState._index` is currently rebuilt from `Records` on first use
after a load. The change persists the index construction cost down to the
retained size, and (where cheap) keeps it warm across commits within a session
so a commit does not invalidate it.

The durable rows remain authoritative: the index is a derived accelerator, and
the spec requires that an inconsistent index can never present as a false
duplicate or a lost credit. In practice it cannot be inconsistent because it is
only ever mutated alongside the rows.

### 6. Bound the reward ledger independently of the record ledger

The two histories have different retirement safety arguments. A reward
transaction must be retained exactly as long as its identity could be
re-derived by a replay — which is the same answer as for records, since a
correction re-derives its transaction identity from the record identity. So both
retire together, on the same watermark-relative rule, keeping the existing
`ProcessedRecordCredited <= LedgerTotal` invariant trivially satisfied.

This is stated explicitly because it is a **new coupling** between two structures
that were previously independent, and a reviewer should see it called out rather
than discover it.

### 7. The gate asserts a bound, not a byte count

`scripts/verify-save-growth.sh` currently asserts byte-reproducibility of the
series. The change adds a convergence assertion: after the horizon, the final
state must be within a documented bound. A byte count is a poor gate (it breaks
on any unrelated field addition); a growth *model* assertion ("growth in the
second half is within X% of the first") is robust to incidental change and still
catches the regression this change exists to prevent.

## Risks / Trade-offs

- **[Retirement could weaken a trust property the red-team suite pins]**
  → This is the whole risk. Mitigation is explicit: the task list requires
  running `ActivityRedTeamTests`, `SessionIngestionTests` and
  `MatureSaveMigrationTests` unchanged and green, plus new tests that
  specifically re-deliver retired records, correct retired records, and
  interact with the recovery path. If any of those need changing, that is a
  signal to redesign, not to adjust the test.

- **[Retired identities themselves still grow]**
  → Accepted and bounded: an identity is a short string with no payload, so the
  set grows by a small constant per retired record. The design does not claim
  constant size. The long-term answer (a probabilistic structure) is recorded in
  the master plan as a Region-2 decision, not solved here.

- **[A backward clock could make retirement appear to skip records]**
  → Retirement is watermark-relative, and the watermark is already proven
  monotonic under backward clocks by `TemporalAnomalyTests`. Retirement is
  therefore no more clock-sensitive than acceptance.

- **[The `ProcessedRecordCredited <= LedgerTotal` invariant could be
  accidentally tightened]** by an implementer reading "retire them together" as
  "retire in lockstep". The task list calls this out as a review point.

- **[Byte sizes in committed evidence shift]** if any record's serialized form
  changes. Mitigation: the change adds fields without altering existing ones, and
  `verify-save-growth.sh` is a mandatory verification task so any drift is
  caught rather than committed.

## Migration Plan

No `ISaveMigration`. `ProcessedRecordLedgerState` and `RewardLedgerState` gain
additive fields that decode as "nothing retired" and "aggregate equals
recomputation", both of which are correct for every existing save.

Rollout order:

1. Add the maintained aggregates and their validator cross-check, with
   retirement still disabled. Proves the O(1) path against the full existing
   suite.
2. Enable retirement and add the convergence gate.

This split matters: if step 1 has a bug, step 2's behaviour changes are not yet
in play.

Rollback: a plain revert. A state written with retirement enabled decodes under
the old build with the extra fields ignored, and the old build simply sees fewer
retained records.

## Open Questions

None. The retention bound and the retirement batch size are calibration
constants assigned explicitly in the task list, constrained by "the gate must
pass for the existing 730-day series".
