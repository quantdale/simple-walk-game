# D-204 evidence — painted Old Trailhead (Unity editor capture, NOT a device)

Captured 2026-10-24 (Asia/Manila) by `tools/unity/capture-trailhead.sh` (Unity 6000.3.26f1, `-force-glcore`, Mesa llvmpipe under Xvfb).
Each PNG is the composite of the camera render (painted layers) and the UI Toolkit layer (caption + dialogue), labelled in-image.
Every frame except `trailhead-s4-flourishing-keydriven` comes from a real `GameSession` walked through `WorldPresentationController`.

| File | Canonical state |
|---|---|
| `trailhead-s0-forgotten-bridge-ruined` | new game: `lm.trailhead@ruined`, bridge `@ruined` |
| `trailhead-s1-awakening-bridge-ruined` | trailhead cleared: `@stabilized`, bridge `@ruined` |
| `trailhead-dialogue-mira` | tap Mira at stabilized (line.mira.2, worried; neutral bust fallback) |
| `trailhead-s1-bridge-build-0of3/1of3/2of3`, `trailhead-s1-bridge-done` | bridge goal picked and walked |
| `trailhead-s3-restored` | continued play until the lookout opens |
| `trailhead-s4-flourishing-keydriven` | key-driven: no content raises the trailhead to Flourishing |

`editmode/`: EditMode run (374/374). Device: UNVERIFIED (KI-002).
