## 1. Rotation in the composer

- [ ] 1.1 In `src/WalkGame.Application/Summaries/ReturnSummaryComposer.cs`, add a
  per-kind retention bound table whose entries sum to exactly
  `PendingReturnSummaryState.MaxItems` (12). Keep 12 as the hard total.
- [ ] 1.2 Implement per-kind eviction: when adding an item of a kind already at
  its bound, retire the oldest item **of that kind**. Retired items must not be
  carried into the next composition round.
- [ ] 1.3 Preserve the existing behaviour of `WithNotice` (the calm recovery
  notice path) under the new eviction: the notice must still be retained.
- [ ] 1.4 Replace the unstable `List<T>.Sort` with an explicit stable ordering:
  kind ordinal first, then first-committed order within a kind. Update the
  comment so the claim matches the implementation.
- [ ] 1.5 Keep `Compose` a pure static function: no clock, no store, no state
  access. The currency timestamp it needs is passed in as a parameter.

## 2. Item currency (additive)

- [ ] 2.1 Add one scalar timestamp field to
  `src/WalkGame.Domain/Summaries/PendingSummaryItemState` recording when the
  item was first committed.
- [ ] 2.2 Populate it in both `Compose` and `WithNotice` from the supplied
  generated-at instant, using `DateTimeOffset` to match the existing fields.
- [ ] 2.3 Do **not** bump `SchemaVersions.Current` and do **not** add a
  migration: the field is additive and absent-decodes to "current".
- [ ] 2.4 Extend `GameStateValidator`'s pending-summary checks to validate the
  new field consistently with the existing item checks (no item may be blank;
  a present currency timestamp must not be after the summary's generation
  timestamp).
- [ ] 2.5 Expose the currency information on
  `ReturnSummaryReadModel.ItemRow` so a shell can render current versus
  historical items differently.

## 3. Home attention signal

- [ ] 3.1 In `GameSession.GetHome()`, change the pending-summary attention
  branch to require at least one **current** item rather than mere existence.
  A purely historical summary reports `HomeAttentionReason.None` and
  `RequiresAttention = false`.
- [ ] 3.2 Keep `HasPendingSummary` true whenever a summary exists, so the
  surface can still offer to read it.
- [ ] 3.3 Choose and document the currency threshold (how old an item must be to
  count as historical), record it in `docs/UX_DESIGN.md`, and keep the
  queue-idle-with-banked-Vitality branch ungated by currency so
  `Scenario06` semantics are unchanged.

## 4. Tests

- [ ] 4.1 New in `tests/WalkGame.Application.Tests/ReturnSummaryDurabilityTests.cs`:
  saturate the transformation kind with more items than the total bound in a
  single batch → exactly 12 items, the most recent retained, older ones gone.
- [ ] 4.2 New: a new transformation item evicts only a transformation item, never
  a production, notice or aggregate item.
- [ ] 4.3 New: a production item never displaces a transformation item when the
  transformation bucket is full, and the documented priority order still holds.
- [ ] 4.4 New: within-kind presentation order equals first-commit order for a
  known sequence.
- [ ] 4.5 New: replaying the same event sequence twice produces byte-identical
  pending summary state.
- [ ] 4.6 New: 30 boots without acknowledgement → bound never exceeded, and the
  final summary contains items from the most recent boots (this is the
  regression test for the saturation defect).
- [ ] 4.7 New: a summary written in the old shape (no currency field) loads with
  every item treated as current, and is not invalidated.
- [ ] 4.8 New in `M5H1ShellAcceptanceTests`: a fully historical summary reports
  `RequiresAttention == false` with the correct reason, while
  `HasPendingSummary` stays true; a summary containing a current actionable
  decision reports `RequiresAttention == true`.
- [ ] 4.9 New: a retired item does not reappear in the next several summaries.
- [ ] 4.10 Extend
  `tests/WalkGame.Infrastructure.Tests/SaveCodecRoundtripTests.Encode_Decode_PreservesEveryField`
  to cover the new item field.
- [ ] 4.11 Confirm unchanged: `ReturnSummaryDurabilityTests` (5 existing),
  `SessionRecoveryAndSummaryTests` (3),
  `M5H1ShellAcceptanceTests` (11), `M3AmbientProgressionAcceptanceTests`,
  `M4Region1AcceptanceTests`, `M4BackwardDecodingTests`,
  `MatureSaveMigrationTests`.

## 5. Documentation

- [ ] 5.1 `docs/GAME_SYSTEMS.md` §11 — document per-kind rotation and the
  currency rule.
- [ ] 5.2 `docs/UX_DESIGN.md` — record the currency threshold and how the shell
  should render current versus historical items.
- [ ] 5.3 `docs/DECISIONS.md` — add an entry for rotation and additive item
  currency, superseding the "pending summary only grows" implication of D-033.
- [ ] 5.4 `docs/KNOWN_ISSUES.md` / `docs/RISK_REGISTER.md` R-001 — add the
  evidence that a low-engagement player no longer receives a frozen summary.

## 6. Verification

- [ ] 6.1 `dotnet build SimpleWalkGame.sln` — zero errors, no new warnings.
- [ ] 6.2 `dotnet test` — all suites green; confirm both acceptance suites still
  assert and achieve byte-identical deterministic state.
- [ ] 6.3 `scripts/verify-pacing-evidence.sh` — seven reports reproduce
  byte-for-byte (summary text appears in `profile` output, so this gate proves
  the change did not alter item text or ordering).
- [ ] 6.4 `scripts/verify-save-growth.sh` — growth series reproduces exactly.
- [ ] 6.5 `scripts/verify-architecture.sh` — 11 passed, 0 failed.
- [ ] 6.6 `tests/guards/run-guard-tests.sh` — green.
- [ ] 6.7 `openspec validate rotate-durable-return-summary --strict` passes.
- [ ] 6.8 `git status` clean apart from intended files; writer lease released.
