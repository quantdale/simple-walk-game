# M8-H1 Persistence & Recovery Evidence (Workstream B)

## Production defects fixed

### H-1 (High) — Recovery re-commit displaced the last healthy generation

`Continue()` recovering from backup used plain `WriteAtomic`, whose first step
rotates the CURRENT primary into the backup slot. During a recovery that primary is
known-corrupt — so the rotation replaced the last valid generation with garbage.
An interruption during the very same commit window (delete-primary → move-temp) then
left NO valid copy on disk even though one existed moments earlier.

**Fix:** `ISaveStore.WriteAtomicPreservingBackup` commits the recovered state over
the primary WITHOUT touching the backup slot. Crash-window analysis: after the
commit there are always ≥1 valid copies (backup keeps generation N−1, primary holds
the recovered N′); an interruption before promotion leaves the exact pre-recovery
state (corrupt primary ignored, healthy backup recovers again).

**Regression tests:**
`PersistenceFaultInjectionTests.RecoveryRecommit_CorruptPrimary_HealthyGenerationSurvivesInBothSlots`,
`.WriteAtomicPreservingBackup_NeverTouchesExistingBackup`;
`SessionPersistenceHardeningTests.RecoveryFromBackup_RepeatedBoots_StableAndNeverRepeatedNotice`.

### H-2 (Medium) — Access failures crashed or masqueraded as "no save"

* `UnauthorizedAccessException` escaped read/write paths (unhandled crash instead
  of diagnostic).
* `File.Exists` returns false for permission-denied paths → an intact but
  inaccessible save was reported as `NoSaveFound` (fail-open toward creating a
  fresh profile over mature progress).

**Fix:** reads probe once when `Exists` is false and classify access-denied /
directory-as-file as `IoFailure` with detail; "no save found" now means the save is
really absent. Persist paths translate access-denied into the documented
`IOException` failure type; `StartNewGame` reports `SaveUnreadable` instead of
throwing.

**Tests:** `.ReadPath_NamesADirectory_ReportsIoFailureInsteadOfThrowing`
(portable access-failure proxy),
`SessionPersistenceHardeningTests.UnrecoverableSaves_ContinueFailsClosed_...`.

### H-3 (Medium) — Boot discarded WHY a save was unreadable

`Continue()` reported only read-level details and fell back to
"Save data could not be read.", losing checksum/version/migration/validation
reasons.

**Fix:** primary/backup decode+validation details are captured and surfaced in
`StartResult.Detail`.

**Test:** `SessionPersistenceHardeningTests.FutureSchemaSave_...` asserts the
specific "newer game version" detail.

### V-1 (Medium, domain) — Missing producer runtime row went undetected

Rows are created for the full producer set at game start; a payload missing one
silently disabled that producer forever (no production, no unlock path). The
validator flagged unknown EXTRA rows only.

**Fix:** `GameStateValidator` requires a runtime row for every content producer.

### H-4 (Low) — Stale temporaries from crashed sessions were never cleaned

Slot `.tmp` files are never canonical data; they are now removed at store
construction (best-effort). Reads already ignored them; next write consumed them.

### Diagnostics (Low) — Unidentifiable deletions vanished from counters

Deletion markers without provider namespace are now counted in
`IngestResult.DeletionsIgnored`.

## Recovery contract (now explicit and tested)

Boot selects the newest VALID generation: primary first, else backup, else explicit
`SaveUnreadable` failure with the specific reason. Unrecoverable saves are never
rewritten, repaired or replaced by reads; only an explicit successful commit may
change bytes. A failed commit is never reported as success. Stale temporaries never
masquerade as canonical data.

## Hostile-path matrix coverage

| # | Scenario | Covered by |
|---|---|---|
| 1–3 | failure before/during temp-write, after durable temp | stale-temp states + `InterruptingStore` commit injection |
| 4 | preserving previous primary during recovery | H-1 regression tests |
| 5–7 | backup staging / replacement windows | exact post-crash disk states (`CrashDuringPromotion_...`) |
| 8–9 | failure immediately before/during primary promotion | deleted-primary + stale-temp + valid-backup state |
| 10–11 | stale `.tmp` / stale backup temp | `StaleTemporaries_FromEarlierCrash_...`, `StaleBackupTemp_...` |
| 12 | malformed/empty primary + valid backup | `EmptyPrimary_ValidBackup_...`, `RecoveryRecommit_...` |
| 13 | valid primary + malformed backup | `ValidPrimary_MalformedBackup_PrimaryRemainsAuthoritative` |
| 14 | malformed primary AND backup | `UnrecoverableGenerations_AreNeverRewrittenOrDeletedByReadPaths`, session-level fail-closed test |
| 15 | inaccessible location / directory-as-file / permission denial | `ReadPath_NamesADirectory_...` (+ classification fix H-2) |
| 16 | storage exhaustion (deterministic) | injected `IOException` at atomic-commit boundary (two suites) |
| 17 | interrupted recovery attempt | H-1 crash-window analysis + interrupted-promotion states |
| 18 | repeated recovery across boots | `RecoveryRecommit...` third-boot assertions, acceptance repeated-boots step |

All suites deterministic; no virtual filesystem was needed — real files in temp
directories reproduce exact post-crash states.
