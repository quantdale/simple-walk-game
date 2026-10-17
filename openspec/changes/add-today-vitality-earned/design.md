# Design

## Source of truth
`GameSession.IngestActivityBatch` is the only writer of `ProcessedRecordEntry(…, EligibleSteps, VitalityCredited, …, RecordEndUtc, LocalDay)`:
- **new record:** `VitalityCredited` = Vitality credited (with the D-079 carry); `LocalDay = DayOf(endUtc, providerOffset)`.
- **correction:** net applied = previous + applied delta. A clamped clawback counts only what was actually reversed. `LocalDay` = the corrected record's day, or the previous day when the offset is unknown.
- **deletion:** `EligibleSteps = 0`; net applied = previous + applied reversal; `LocalDay` is kept.

Rows commit atomically with the reward ledger. `GameStateValidator` enforces that every row has `VitalityCredited >= 0`, that the rows' lifetime total never exceeds the reward ledger's, and that every dated row's day is in `WalkedLocalDays`.

## Derivation
`VitalityOnLocalDay(d)`:
- **`d <= RetiredThroughLocalDay`:** null. The day may include retired rows, so it is not knowable, and a partial sum would understate silently.
- **otherwise:** Σ `VitalityCredited` over rows with `LocalDay == d`, `EligibleSteps > 0` and `VitalityCredited > 0`.
  - Rows with a null `LocalDay` (legacy or undated) never count, so the figure can only understate.
  - A deleted row counts 0 even when its clawback was clamped by a spent balance (the residue is already in `UnappliedReversalVitality`). The source says that walk did not happen.
  - A downward correction that was clamped keeps its applied residue on the day, which matches what the reward ledger shows.

Cost is O(retained rows) per Home render. The 45-day retention window bounds the retained rows. Retirement only removes rows whose end is before trusted progress − 45 d, so today's rows are not retired in practice, and `RetiredThroughLocalDay` guards the edge.

## Today
`DayOf(clock.UtcNow, deviceOffsetSeconds)`. An offset outside ±18 h makes the read model unknown, and no line is shown. A walk recorded in a zone different from the device's current one counts on the day its provider recorded (travel edge, the same rule as days walked).

## Rejected: persisted per-day ledger + v3→v4 migration
See the proposal. It duplicates canonical rows, needs a seeding rule that is either wrong (empty) or a copy (rows), adds bytes and a validator invariant, and adds a second structure that corrections must update atomically.

## Copy
`home.today.earned` = "Today: {0} Vitality earned from your walks.", directly under "Unspent: …", shown only when the value is > 0.

## Tests
- Domain `DailyEarningsTests` (5): day sum, undated rows excluded, clamped deletion counts 0, correction down and day move, retired day unknown.
- Application `DailyEarningsSessionTests` (13):
  - fresh known zero; local-day boundary (UTC+8 vs UTC−5); exactly-once replays;
  - correction down and deletion reduce today; clamped deletion; undated walks; offset out of range;
  - save/reload round trip (reading writes nothing, no new field, schema 3);
  - v1 and v2 saves load with today 0 and never invented credit; a hostile v2 save with injected local days fails closed or shows 0; a tampered negative row fails closed.
- EditMode `HomeTodayEarnedTests` (5): the line sits under Unspent, no line for yesterday, undated or zero, a deletion lowers it, and the device offset decides the day.
- Gates: save growth, pacing 7/7, content compatibility, architecture.
