# Add an honest "days walked" count (local date per record, maintained count, "at least N" migration)

**Status:** Approved for implementation by the executing agent (D-103). Region 1 scope; it uses the existing save-migration policy (`openspec/config.yaml`: a persisted-shape change needs a registered `ISaveMigration` plus a mature-save fixture test). It is not a post-MVP item (D-011 / ROADMAP gate concern Region 2 only).

## Why

D-099 left "days walked" out of the Progress screen, and D-102 deferred it because an honest count could not be built from a v2 save:
1. retired processed-record rows (D-066) keep only their identity, so old days cannot be recovered;
2. records carry only UTC instants (`NormalizedActivityRecord.StartUtc/EndUtc`), so a player's local calendar day is undefined.

D-102 named the fix: persist each credited record's local date, maintain a distinct-day aggregate, and migrate legacy saves to a value shown as a lower bound ("at least N days").

## What Changes

- `NormalizedActivityRecord` gains an optional `EndZoneOffsetSeconds` (the provider's zone offset at the record's end). It is not part of the record identity or fingerprint, so dedup is unchanged.
- The Health Connect adapter reads an optional sixth line field (the record's `endZoneOffset` in seconds) from the bridge; the Java bridge writes it when Health Connect supplies one. A five-field line still parses (offset unknown).
- `ProcessedRecordEntry` gains `LocalDay` (days since 0001-01-01 of the local date of the record end). Null when the offset is unknown or out of range (±18 h).
- `ProcessedRecordLedgerState` gains a maintained aggregate: `WalkedLocalDays` (sorted distinct day numbers), `LegacyDaysLowerBound`, `LegacyLastUtcDay`, `RetiredThroughLocalDay`, `HasUndatedCredits`, `IsLegacyLowerBound`.
  - Credit with steps > 0 and a known local day adds the day; with an unknown day sets `HasUndatedCredits`.
  - A deletion or correction to zero steps removes the day only when no other retained row with steps credits that day and the day is after every retired row's day.
  - Retirement keeps the days (the set is never pruned by retirement) and advances `RetiredThroughLocalDay`.
- Schema v2 → v3: `MigrationV2ToV3` seeds `LegacyDaysLowerBound` with the minimum number of local days that can explain the retained credited rows' end instants (greedy cover with a 50-hour window, the widest span one local date can have in UTC for offsets −12 h … +14 h). It sets `IsLegacyLowerBound` when anything was credited or retired. That is a proven lower bound, never an overcount.
- Read model `DaysWalkedReadModel(Days, IsLowerBound)` from `GameSession.GetDaysWalked()`. Legacy days and post-migration days are summed without overlap: post-migration days at or before `LegacyLastUtcDay + 1` are not added (their local date may equal a legacy one).
- Progress screen line: "You have walked on N days." or "You have walked on at least N days."

## Non-goals

Streaks, "days missed", per-day step totals, charts, calendars or any time-pressure copy. Backfilling exact days for legacy saves (impossible, D-102). Offset capture for the fixture and synthetic dev sources (they produce undated credits, which keep the label "at least").

## Compatibility

Schema 2 → 3 with a registered, idempotent migration; v1 saves migrate 1 → 2 → 3. Fields are additive; a v3 save is refused by older builds (newer schema fails closed, existing policy). Content version and fingerprint unchanged. Save growth: one nullable int per retained row plus one int per walked day; re-measured under the D-098 gate.

## Evidence

`src/WalkGame.Domain/Activity/ActivityIngestion.cs` (`ProcessedRecordEntry`, `ProcessedRecordLedgerState.Retire`), `src/WalkGame.Infrastructure/Persistence/Migrations/MigrationV1ToV2.cs`, `HealthConnectActivityAdapter.ParseLine` (5 fields today), D-102.
