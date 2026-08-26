# M5-H1 acceptance matrix (named scenarios)

All rows executed through real Application/Infrastructure boundaries with deterministic clocks; suite: tests/WalkGame.Application.Tests/M5H1ShellAcceptanceTests.cs. Result column reflects the final integrated tree (295/295 green).

| # | Scenario | Proof highlights | Result |
|---|---|---|---|
| 1 | First-run / grant path | Onboarding stages to Complete only via real enqueue; waiting-for-first-data transitions to connected-current after first batch; restart resumes complete | PASS |
| 2 | First-run / denial path | Denied permission leaves every read model available; no fabricated credit; onboarding still completable manually | PASS |
| 3 | One-day return | Quiet return owes nothing (no fabricated summary); acknowledged home is explicitly calm (attention reason None) | PASS |
| 4 | Seven-day return | App-closed advancement invests banked vitality; summary within glance budget; second reload coherent | PASS |
| 5 | Thirty-day return | Summary hard-bounded (D-033 cap); projections fixed-size; watermark-age diagnostics in expected range | PASS |
| 6 | Queue empty while away | Explicit QueueEmptyWithBankedVitality attention reason with exact banked amount; automation fallback policy untouched | PASS |
| 7 | Source temporarily fails | RefreshTemporarilyFailed + RetryLater; progress preserved; retry processes new records exactly once; replay of retry window credits zero | PASS |
| 8 | Permission revoked externally | Status flips without touching earned balances; reconnect representable (needed -> granted) | PASS |
| 9 | Save recovery used | Calm backup-restored notice in durable summary; no silent reset; diagnostics expose RecoveredFromBackup + primary MalformedEnvelope category | PASS |
| 10 | Preference isolation | save.json bytes identical across rapid toggles AND across restarts; user choices persist in separate store | PASS |
| 11 | Replay after UX operations | Identical history replayed after onboarding/settings/status/diagnostics operations credits zero additional | PASS |
