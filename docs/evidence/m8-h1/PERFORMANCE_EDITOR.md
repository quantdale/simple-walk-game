# M8 Performance: Unity-editor half (2026-10-16, D-074)

**Unity-editor-tested** (6000.3.26f1 batchmode -nographics on the box; layout runs, pixels are not rasterised). These are **not device numbers**: cold/warm launch on a phone, memory, thermal and battery stay **UNVERIFIED** (KI-002). Wall-times are logged (`WG_PERF`), never gated. Structural budgets are asserted.
Suites: `Tests/EditMode/ShellPerformanceTests.cs` and `Tests/PlayMode/ShellPanelMemoryTests.cs`.

## Mature save and restored end-state
365 app-closed days (14,000 steps/day, 2,000 every 7th day) through the real session. The valley is fully restored, with the post-completion care program running.

| Measure | Value |
|---|---|
| Building the 365-day save (365 boots + ingests + commits) | 3.0–3.3 s |
| Save size | 117 KB |
| Warm boot of the mature save (controller.Boot) | 5–7 ms |

| Screen (mature, restored) | Median model + render | Elements (budget 250) |
|---|---|---|
| Home | 0.29–0.35 ms | 15 |
| Projects | 0.37–0.38 ms | 25 |
| Region (valley picture) | 0.26–0.41 ms | 18 (shapes ≤ 86) |
| Return summary | 0.32 ms | 18 |
| Settings | 0.25–0.27 ms | 12 |
| Activity | 0.14–0.19 ms | 7 |

## Repeated scene enter/exit (3,000 renders across 6 screens)

| | Before fix | After fix |
|---|---|---|
| Wall time, 3,000 renders (EditMode) | 1,209–1,279 ms | 627 ms |
| Retained growth in a live UIDocument panel | ~470 B/render (1.07 MB from 750→3,000) | ~116 B/render (0.26 MB from 750→3,000) |

The growth was found by this campaign. Probing showed that `new ScrollView()` retains about 233 B inside UI Toolkit, and other elements 6–33 B. The view rebuilt its ScrollView on every render. **Fix:** `ShellView.Render` reuses one ScrollView per root, replacing only its content and resetting the scroll position. The remaining ~116 B/render is UI Toolkit's own per-element retention, tracked as KI-018 for device measurement. Gate: under 1 MB growth between 750 and 3,000 live-panel renders; each render replaces and never stacks elements.
