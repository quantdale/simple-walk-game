# D-160 celebration recap — editor-rendered captures (NOT device)

> Superseded for the summary lines by `docs/evidence/d162-copy-sweep/` (D-162 fixed "Complete the valley survey is complete." and the expedition lines). The recap itself is unchanged.

Rendered with `tools/unity/capture-valley.sh` (Unity 6000.3.26f1 PlayMode `ValleyCaptureTests`, software OpenGL llvmpipe under Xvfb) from `CompletedRegionFixture` (Region 1 completed through the real pipeline on day 77, catalog-order player, 20,000 synthetic steps/day).

| File | Settings |
|---|---|
| `celebration-top-editor-rendered.png` | `WG_CAPTURE_SCREEN=Celebration`, 1x |
| `celebration-recap-editor-rendered.png` | same, scrolled to "What you restored" (`WG_CAPTURE_SCROLL_TO`), 1x |
| `celebration-recap-2x-editor-rendered.png` | same, `WG_CAPTURE_TEXT_SCALE=2` |

Synthetic walks carry no local day, so this fixture has no days-walked line; dated walks add "You have walked on N days." after the letters line.

`editmode-results.xml`: EditMode 252/252 on this branch (Unity-editor-tested). `capture-results.xml` / `capture-run.log`: the last (2x) capture run, licence identifiers redacted. Labels: Unity-editor-tested, editor-rendered; device UNVERIFIED (KI-002).
