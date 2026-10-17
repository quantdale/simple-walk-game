# D-136 — Guide and Settings at 200% text (editor-rendered, NOT device)

Captured with `tools/unity/capture-valley.sh` (`WG_CAPTURE_TEXT_SCALE=2.0`, Mesa llvmpipe under Xvfb, PlayMode `ValleyCaptureTests`) on branch `agent/simple-walk-game/polish-grok0739` (base main `9f1e058`). `before/` = main before the fix; top-level PNGs = after.

Review findings (2x text):
1. **Fixed.** The title scaled the full 2x (56 px) and wrapped one word per line ("The / valley / today"); on the Guide this pushed the only action, Continue, below the fold. Titles now grow at half rate (`ShellView.TitleSizeFor`: 28 -> 42 px at 200%, always >10% above body). After: the Guide title takes two lines and Continue is fully visible without scrolling.
2. OK. All body text and buttons wrap, nothing clips; buttons keep >= 2x (112 px) touch height; contrast unchanged (D-095).
3. OK. Settings is long at 2x but scrolls; every action (reduced motion, sound, diagnostics, privacy, delete, Home) is reachable at the scrolled end (`settings-2x-end`).
4. Noted, not changed. The desktop-style scrollbar with arrow buttons appears in editor renders; on device UI Toolkit uses touch scrolling (UNVERIFIED, KI-002).

EditMode after the fix: 227/227 + 1 ignored (Unity-editor-tested).
