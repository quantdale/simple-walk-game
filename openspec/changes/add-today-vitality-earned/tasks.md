## 1. Domain
- [x] 1.1 `ProcessedRecordLedgerState.VitalityOnLocalDay` (tests: `tests/WalkGame.Domain.Tests/DailyEarningsTests.cs`).
## 2. Application
- [x] 2.1 `DailyEarningsReadModel`, `GameSession.GetTodayEarnings(int)` (tests: `tests/WalkGame.Application.Tests/DailyEarningsSessionTests.cs`, incl. v1/v2 and hostile old saves).
## 3. Unity
- [x] 3.1 Home line + `ShellController.LocalOffsetSeconds` (EditMode `HomeTodayEarnedTests`).
## 4. Verify
- [x] 4.1 .NET, EditMode, PlayMode green; save-growth, pacing, content-compat, architecture gates; DECISIONS D-156 records "schema v3 unchanged" explicitly.
