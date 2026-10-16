# Design

## Day number
`LocalDayCalendar.DayOf(endUtc, offsetSeconds)` = `(endUtc.UtcDateTime + offset).Date.Ticks / TicksPerDay`; null if offset is null or |offset| > 18 h. Integer only, deterministic.

## Where days are recorded
`GameSession` new-record path computes the day from the record; the correction path uses the record's day or keeps the existing row's day; the deletion path keeps the existing row's day with `EligibleSteps = 0`. The aggregate is maintained inside `ProcessedRecordLedgerState.Record/Update/Retire`, the same place as `MaintainedVitalityCredited` (D-066), so it is committed atomically with the rows.

## Count
`DaysWalkedCount = LegacyDaysLowerBound + |{d ∈ WalkedLocalDays : LegacyLastUtcDay is null or d > LegacyLastUtcDay + 1}|`.
`IsLowerBound = IsLegacyLowerBound || HasUndatedCredits`.

## Migration v2 → v3
Pure JSON node transform like `MigrationV1ToV2`: collect `recordEndUtc` of retained `processedRecords.entries` with `eligibleSteps > 0`; sort; greedy groups of 50 h; write `legacyDaysLowerBound`, `legacyLastUtcDay` (UTC day of the latest), `isLegacyLowerBound`, `walkedLocalDays: []`; set `schemaVersion = 3`. Rows with no `recordEndUtc` are not counted (the flag still marks the result a lower bound).

## Validator
`WalkedLocalDays` strictly ascending and non-negative; `LegacyDaysLowerBound >= 0`; every retained row's `LocalDay` with steps > 0 is in the set.

## Layering
Domain only (no clock, no zone lookup); the offset arrives on the record from the adapter.
