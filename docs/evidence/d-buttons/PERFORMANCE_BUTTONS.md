# KI-018 follow-up: pooled action buttons (2026-10-16, D-089)

**Unity-editor-tested** (6000.3.26f1 batchmode -nographics on the box). These are **not device numbers**; device memory/frame time stay **UNVERIFIED** (KI-002).

## Method
Same as `docs/evidence/c-perf-pass/PERFORMANCE_PASS.md`: `ShellPerformanceTests.Screen_switch_allocations_are_measured_and_bounded` on the 365-day mature save, all 8 game screens, `GC.GetTotalMemory` growth, median of 60, 4 KB resolution (figures are approximate, "~"). Before = `docs/evidence/c-perf-pass/after/` (main at 3023e17); after = `after/` here.

## Change
`ShellView.Render` now pools the `actions` container and its buttons per root (`ShellView.ActionButton`). Each pooled button has one click handler for its lifetime that reads the action id and callback for the current render, so no closure, Button or inline style block is created per screen switch. Label, name, tooltip, size and primary/secondary fill are reset every render. New test `Pooled_buttons_match_each_screen_and_fire_the_current_action` checks ids, labels, primary styling and tap routing across 6 switches and that an instance is actually reused.

## Results
| Screen | view bytes before | view bytes after |
|---|---|---|
| Settings | ~40,960 | ~4,096 |
| Home | ~36,864 | ~0 |
| Discoveries | ~20,480 | ~0 |
| Region | ~16,384 | ~4,096 |
| ReturnSummary | ~16,384 | ~4,096 |
| Story | ~12,288 | ~4,096 |
| Projects | ~12,288 | ~0 |
| ActivityStatus | ~12,288 | ~4,096 |

| Measure | Before | After |
|---|---|---|
| 3,000 screen enter/exits (EditMode wall) | 384 ms | 151 ms |
| Live-panel growth 750→3,000 renders (PlayMode) | ~0 (−4 KB, noise) | +49,152 B / 2,250 ≈ ~22 B/render (≈ 12 GC pages; within the 4 KB-resolution noise band) |

Remaining per-render view allocation is at or below the 4 KB resolution (title/notice/preview labels and the valley element are still rebuilt). Tests: EditMode 109/109, PlayMode 4 passed + 1 capture-only ignored. KI-018 stays open only for a device measurement.
