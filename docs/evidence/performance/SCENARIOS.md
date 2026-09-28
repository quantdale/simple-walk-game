# Performance regression scenarios + save-growth evidence

**Campaign:** C3 (2026-09-29). **Decision:** D-048. **Gate:** `scripts/verify-save-growth.sh`.

**Host:** Windows, .NET SDK 9.0.300, Debug build, local SSD. Single machine, no device.

**Standing caveat, restated because it is the whole point of this document:** every number
below is a **headless workstation measurement**. `PERFORMANCE_BUDGETS.md` §21 requires
device model, OS version, build config, scene/state, measurement tool, duration/sample size,
quality tier, observed metric and pass/fail. **None of those nine fields can be populated
for mobile here**, and `PERFORMANCE_BUDGETS.md:366` states plainly that desktop editor FPS
is not evidence of mobile performance. Nothing in this document may be cited as evidence
that a device budget is met.

---

## 1. What was missing

`PERFORMANCE_BUDGETS.md` §19 names ten repeatable regression scenarios. **None were
implemented or scripted.** §15 states "CI simulation should report save-size growth across
long-running synthetic profiles" — also not implemented; growth existed only as prose
figures inside `docs/evidence/m8-h1/`.

The ten scenarios split by what is honestly measurable without a Unity editor or a device:

| # | Scenario | Classification | Covered here |
|---|---|---|---|
| 1 | cold boot with mature save | headless-measurable | yes |
| 2 | return after 7 days with large activity batch | headless-measurable | yes |
| 3 | Home idle for 5 minutes | device-only (presentation) | **no — not simulated** |
| 4 | enter Visit World | device-only | **no — not simulated** |
| 5 | traverse dense/restored area | device-only | **no — not simulated** |
| 6 | trigger landmark state transition | headless-measurable (canonical only) | partially — canonical cost only |
| 7 | exit/re-enter Visit World repeatedly | device-only | **no — not simulated** |
| 8 | background/resume loop | headless **surrogate** | surrogate only |
| 9 | mature region with maximum unlocked content | headless-measurable | yes |
| 10 | long-running simulated save/ledger | headless-measurable | yes |

Five measured, one surrogate, four deliberately not simulated. **A number was not invented
for scenarios 3, 4, 5 or 7.** A fabricated figure would be worse than an acknowledged gap.

Scenario 6 is explicitly *canonical-only*: the headless harness can measure the cost of a
landmark stage transition in canonical state, and cannot measure the rendered frame cost of
that transition. Scenario 8 is a **surrogate**: repeated boot-from-disk plus advance is not
Android backgrounding, process death or device reboot, and is labelled as such in its own
output line.

Reproduce with:

```bash
dotnet run --project tools/simulation -- scenarios --save <tmpdir> --seed 42
```

---

## 2. Scenario results (seed 42, 8000 steps/day)

Wall-clock is **evidence-only** and is not gated — it is machine-dependent, and
`TESTING_AND_RELEASE.md` §17 treats flaky timing assertions as defects.

| Scenario | Measurement | Observed |
|---|---|---|
| 1 — cold boot, 200-day save | boot time | 35.55 ms |
| 1 | save size at maturity | 110,830 bytes |
| 2 — return after 7 days | 7-day ingest time | 7.27 ms |
| 2 | records / credited | 7 records, 56,000 steps → 560 Vitality, 0 duplicates |
| 6 — landmark transition | canonical enqueue cost | 2.99 ms, 1 landmark advanced |
| 8 — resume surrogate | 20 boot+advance cycles | 8.51 ms/cycle, 170.10 ms total |
| 9 — mature region | boot with all content unlocked | 10.62 ms, `requiresAttention=true`, **0 validator violations** |

Scenario 2 is a correctness result as much as a timing one: a single aggregated 7-day batch
credits 56,000 steps → 560 Vitality through the real trust pipeline with **zero duplicates**,
confirming the conversion rule holds for a multi-day batch.

---

## 3. Save-size growth series (PERFORMANCE_BUDGETS §15)

This is the deterministic half, and the reason the gate exists.

| Day | Save bytes | Processed ledger | Ledger records | Ledger Vitality | Materials | Knowledge | Projects done | Region complete |
|---|---|---|---|---|---|---|---|---|
| 7 | 10,344 | 7 | 7 | 560 | 0 | 0 | 1 | no |
| 30 | 23,752 | 30 | 30 | 2,400 | 40 | 0 | 4 | no |
| 90 | 56,085 | 90 | 90 | 7,200 | 100 | 0 | 8 | no |
| 180 | 104,615 | 180 | 180 | 14,400 | 404 | 0 | 15 | no |
| 365 | 202,644 | 365 | 365 | 29,200 | 1,144 | 200 | 19 | **yes** |
| 730 | 394,401 | 730 | 730 | 58,400 | 2,604 | 638 | 19 | yes |

**Growth is linear and unbounded, at roughly 525–530 bytes per ingestion day.** Measured
segment rates: 180→365 days = +98,029 bytes over 185 days (530 B/day); 365→730 = +191,757
over 365 days (525 B/day). This corroborates the ~554 B/day figure recorded in
`LONG_HORIZON.md` (measured on a different host) and confirms the accepted cost explicitly.

Projection, stated as arithmetic rather than prediction: at ~525 B/day a save reaches ~1 MB
around day 1,900 (≈5.2 years) and ~10 MB around day 19,000 (≈52 years). For any realistic
play lifetime the size is **not** a practical problem; the cost is unbounded *shape*, not
magnitude.

**Region completion is reached at day 365 and stays stable through day 730** with all 19
projects complete and the validator clean, so growth past completion is pure ledger
accumulation — exactly the shape D-045 deferred.

Compaction remains deferred and is still a product decision: `DECISIONS.md` D-045 records
that "late deletions target ledger rows without horizon checks, so compaction changes
deletion semantics and requires a product decision." This campaign produced the measurement
that decision needs; it did not make the decision.

---

## 4. Secondary-resource accumulation (D-032 deferral, now quantified)

At day 730 the save holds **2,604 Materials and 638 Knowledge with no consumer anywhere in
`src/`**. `ResourceBalances.SetCap` has no production call site and `TryConsume` in `src/`
only ever consumes Vitality, so two of the three currencies are pure dead weight that grows
without bound and can never bind producer capacity — the condition D-032 explicitly deferred
to "M4 concern" and which M4 never picked up.

The fix (project secondary-resource requirements plus content-defined caps) is **not** a
mechanical change: it alters progression balance, and `ContentValidator`'s critical-path
reachability would have to prove that a project is *schedulable* given the materials its
unlock dependency actually produces, or it can declare a reachable graph that deadlocks in
play. That is a real hazard, recorded rather than shipped at speed. The C3 measurement
series above is the evidence such a change would have to be evaluated against.

---

## 5. What remains UNVERIFIED

- All four device-only scenarios (3, 4, 5, 7) — no editor, no device.
- Scenario 6's rendered frame cost; only the canonical transition is measured.
- Scenario 8's real backgrounding, process death and device reboot behaviour.
- Every §3 numeric budget (cold launch ≤3.0 s, warm resume ≤1.0 s, input <100 ms, 60 fps,
  memory, GC, asset, battery, thermal): **0 of the §22 "definition of performance-ready"
  conditions are satisfied**, and none can be on this hardware.
- Frame pacing, thermal throttling and battery draw.

**§19 is now 5/10 measured headlessly, 1/10 by surrogate, 4/10 not simulated. §15's CI
save-growth obligation is discharged. §22 remains 0/10 and is device-blocked.**
