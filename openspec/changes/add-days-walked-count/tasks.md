## 1. Domain
- [x] 1.1 `EndZoneOffsetSeconds` on `NormalizedActivityRecord`; `LocalDay` on `ProcessedRecordEntry`; `LocalDayCalendar` (tests: `tests/WalkGame.Domain.Tests/DaysWalkedTests.cs`).
- [x] 1.2 Aggregate in `ProcessedRecordLedgerState`; validator rules.
## 2. Application
- [x] 2.1 `GameSession` paths record days; `GetDaysWalked()` (tests: `tests/WalkGame.Application.Tests/DaysWalkedSessionTests.cs`).
## 3. Infrastructure
- [x] 3.1 `SchemaVersions.Current = 3`, `MigrationV2ToV3` (tests: `tests/WalkGame.Application.Tests/MigrationV2ToV3Tests.cs`, incl. a mature fixture and v1→v3 chain).
- [x] 3.2 Health Connect sixth field (adapter tests) + Java bridge.
## 4. Unity
- [x] 4.1 Progress line (EditMode `ProgressScreenTests`).
## 5. Verify
- [x] 5.1 `dotnet test` all green; save-growth and pacing evidence gates; EditMode suite; existing migration tests keep passing.
