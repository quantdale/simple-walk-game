# D-164 onboarding steps at 1x and 2x — editor-rendered captures (NOT device)

`tools/unity/capture-onboarding-steps.sh` (one `capture-valley.sh` run per PNG; `WG_CAPTURE_SCREEN=Onboarding|Guide`, `WG_CAPTURE_GUIDE_STEP`, `WG_CAPTURE_GUIDE_CHOSEN`, `WG_CAPTURE_TEXT_SCALE`, `WG_CAPTURE_SCROLL=end`). Fresh device, dev time source, walking connected (editor fake provider). 540×1170.

| Step | 1x | 2x | 2x scrolled to end |
|---|---|---|---|
| Welcome (pre-start) | `step0-welcome-editor-rendered.png` | `step0-welcome-2x-…` | `step0-welcome-2x-end-…` (scrolls) |
| 1 The valley today | `step1-valley-…` | `step1-valley-2x-…` | fits |
| 2 Connect your walking | `step2-walking-…` | `step2-walking-2x-…` | `step2-walking-2x-end-…` (scrolls) |
| 3 Choose a first restoration | `step3-first-project-…` | `step3-first-project-2x-…` | fits |
| 3 after choosing | `step3-chosen-…` | `step3-chosen-2x-…` | fits |
| 4 Progress while you're away | `step4-away-…` | `step4-away-2x-…` | fits |
| 5 That's all | `step5-done-…` | `step5-done-2x-…` | fits |

"fits": the `-2x-end` PNG is identical to the `-2x` PNG because nothing scrolls.

Findings fixed (D-164); `before-fix/` keeps the pre-fix 1x PNGs:
- step 2 said "You can keep going without connecting; nothing is locked." right under "Connected. Waiting for your first walk to arrive." Now that line shows only while walking is not connected.
- step 3 said "Pick one place to restore first. You can change the order later." with one place offered. Now: "Start here. More places open as you restore."

No clipping, overlap or unreachable button at 1x or 2x. `ONBOARDING_RUN.txt`: per-capture exit codes and renderer. `editmode-results.xml` / `editmode-run.log`: EditMode 263/263 (licence identifiers redacted). Labels: Unity-editor-tested, editor-rendered; device UNVERIFIED (KI-002).
