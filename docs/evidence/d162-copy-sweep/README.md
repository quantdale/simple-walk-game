# D-162 copy-quality sweep — editor-rendered captures (NOT device)

Rendered with `tools/unity/capture-valley.sh` (Unity 6000.3.26f1 PlayMode `ValleyCaptureTests`, software OpenGL llvmpipe under Xvfb) from `CompletedRegionFixture` (Region 1 completed through the real pipeline, catalog-order player, 20,000 synthetic steps/day). Same fixture and framing as `docs/evidence/d160-restored-recap/`, which showed the bug ("Complete the valley survey is complete.").

| File | Settings |
|---|---|
| `celebration-recap-editor-rendered.png` | `WG_CAPTURE_SCREEN=Celebration`, `WG_CAPTURE_SCROLL_TO="What you restored"`, 1x (the D-160 framing; now "The valley survey is complete.") |
| `celebration-items-editor-rendered.png` | same, scrolled to "The valley changed", 1x: "Expedition walked: Valley Transect (+50 Vitality).", "The valley survey is complete.", "Ready to queue: …" |
| `celebration-items-2x-editor-rendered.png` | same, `WG_CAPTURE_TEXT_SCALE=2` |

`editmode-results.xml`: EditMode 255/255 on this branch (Unity-editor-tested), including `CopyQualitySweepTests` (3). `capture-results.xml` / `capture-run.log`: the last (2x) capture run, licence identifiers redacted. Labels: Unity-editor-tested, editor-rendered; device UNVERIFIED (KI-002).
