# D-139 performance re-measurement (2026-10-17, grok0739d)

The box is Linux x86_64 with Unity 6000.3.26f1 in batch mode (llvmpipe) and .NET 9.0.319. These are editor and headless timings only; device timings are UNVERIFIED (KI-002). Timings are evidence and are not gated.

## EditMode `ShellPerformanceTests` (tree: main `ac2031c` + D-138 localization), Unity-editor-tested
| screen | median model+render ms | elements | lines |
|---|---|---|---|
| Home | 0.042 | 17 | 6 |
| Projects | 0.045 | 25 | 20 |
| Region | 0.035 | 19 | 12 |
| Settings | 0.030 | 16 | 5 |
| ActivityStatus | 0.010 | 7 | 2 |
| ReturnSummary | 0.028 | 18 | 12 |

- 3,000 screen enter/exit renders: 157 ms total. The live-panel growth gate (< 1 MB) passed.
- Navigation invoke: 1.1–1.6 µs, Story 16.4 µs. View allocation is about 4 KB per screen switch.
- In D-074 the median was 0.14–0.41 ms. No regression, and the D-122/D-138 overlays add no visible cost (in English the localizer is skipped).

## Headless (`tools/simulation scenarios --seed 42`, `scripts/verify-save-growth.sh`)
- Cold boot of a 200-day mature save: 47.5 ms (first-run JIT included). Booting the mature max-content save: 2.7 ms.
- 7-day return with a large batch: 5.2 ms ingest. Resume surrogate: 2.9 ms/cycle.
- Save size: day 7 12,608 B; day 365 59,817 B; day 730 81,716 B. Second-year growth is 46% of first-year (bound 60%). The ledger plateaus at 46 rows.

Logs: `save-growth.log`, `scenarios.log`.
