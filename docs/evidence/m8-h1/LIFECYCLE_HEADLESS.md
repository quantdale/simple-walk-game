# M8 Lifecycle: headless half (2026-10-16)

**Status: automated verified (headless).** Real OS process kill, device reboot and Health Connect revocation on hardware are **UNVERIFIED** (KI-002, no KVM).

Model: each "process" is a fresh `GameSession` over the same save directory, with no in-memory sharing. Suite: `tests/WalkGame.Application.Tests/M8LifecycleHeadlessTests.cs` (D-072).

| Lifecycle class | Scenario | Invariant proven |
|---|---|---|
| Repeated background/resume | 40 days × 4 resumes per day; each resume re-fetches the whole history | Lifetime Vitality, completed projects and the balance are identical to a one-process-per-day reference (exactly-once under overlapping re-fetch) |
| Process death | Torn half-written `.tmp` files next to every committed file | Boot loads the last commit unchanged; the next day's record lands once (1 accepted, 12 duplicates) |
| Interrupted world loading | 50 boots that read every surface, then die | No progress lost or invented; the next record lands once |
| Permission changed outside the app | Revoked for 10 days, then re-granted with a full back-fill | Status is PermissionDenied while revoked; earned progress is never taken back; the back-fill credits only the 11 new days (10 duplicates ignored); status returns to ConnectedCurrent |

Finding: a boot is not read-only, because it commits offline-advance and summary bookkeeping. This is by design and is not a defect: the guarded invariant is canonical progress, not save bytes.
