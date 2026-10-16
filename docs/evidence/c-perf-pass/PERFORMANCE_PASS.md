# Performance pass: shell and screen switching (2026-10-16, D-088)

**Unity-editor-tested** (6000.3.26f1 batchmode -nographics on the box). These are **not device numbers**; device frame time, memory, thermal and battery stay **UNVERIFIED** (KI-002). Wall-times are logged (`WG_PERF`), never gated.

## Method
- `ShellPerformanceTests.Screen_switch_allocations_are_measured_and_bounded` (new): on the 365-day mature, fully restored save, for all 8 game screens (now including Story and Discoveries). It measures managed bytes per `ShellController.Render()` (model) and per `ShellView.Render()` (view), and the cost of a `nav:` invoke.
- Unity's Mono returns 0 from `GC.GetAllocatedBytesForCurrentThread` (first baseline run showed all zeros), so allocation is read as the growth of `GC.GetTotalMemory(false)`. Samples during which a collection ran are dropped, and the median of 60 is kept. Resolution is the GC's 4 KB heap granularity, so the figures are **approximate** (“~”).
- Live-panel retention: existing PlayMode `ShellPanelMemoryTests` (3,000 renders in a live UIDocument).

## Hotspot found
The view, not the model, dominated: every screen switch created a new `Label` plus its inline style block for every body line (up to ~86 KB per render on Story). The model side was ≤ ~16–20 KB (Discoveries builds two lookup dictionaries) and ~0 for most screens.

Also, `ShellBootstrap.Redraw` called `Debug.Log` on **every** render in release builds too (string building plus stack-trace capture per screen switch).

## Fixes
1. `ShellView.Render` pools body-line labels per root and reuses them, resetting text, font size and name on each render. Title, notice, preview, valley and buttons are still rebuilt. A new test proves that pooled labels never carry stale text or the `empty-state` name between screens.
2. The `WG_RENDER` log only runs when `Debug.isDebugBuild` is set. The editor and development builds, including emulator autodrive, are unchanged.

## Results (same machine, same test; before = `baseline/`, after = `after/`)

| Screen | view bytes before | view bytes after | model bytes (before → after) |
|---|---|---|---|
| Story | ~86,016 | ~12,288 | ~0 → ~0 |
| Projects | ~77,824 | ~8,192 | ~8,192 → ~4,096 |
| Home | ~65,536 | ~36,864 | ~0 → ~0 |
| Discoveries | ~65,536 | ~20,480 | ~16,384 → ~20,480 |
| ReturnSummary | ~57,344 | ~20,480 | ~0 → ~0 |
| Region | ~53,248 | ~16,384 | ~4,096 → ~0 |
| Settings | ~49,152 | ~40,960 | ~0 → ~0 |
| ActivityStatus | ~16,384 | ~8,192 | ~0 → ~0 |

| Measure | Before | After |
|---|---|---|
| 3,000 screen enter/exits (EditMode wall) | 678 ms | 392 ms |
| Live-panel retained growth 750→3,000 renders | ~116 B/render (D-074) | ~78 B/render (176,128 B / 2,250) |
| `nav:` invoke | 1–4 µs | 1–2 µs |

Model-side numbers are at or under the 4 KB resolution and were left alone: no hotspot worth the code. The remaining view allocations are buttons (closures and style) and the valley element; KI-018 stays open for device measurement.

Raw logs: `baseline/editmode-run.log`, `after/editmode-run.log`, `after/playmode-run.log` (grep `WG_PERF`). EditMode after the fix: 103/103 on this branch. PlayMode: 4 passed, 1 capture-only ignored.
