# M8 UX/accessibility: headless half (2026-10-16)

**Status: Unity-editor-tested** (EditMode, Unity 6000.3.26f1 batchmode). Real TalkBack, OS font scaling on hardware, and screen-reader focus frames are **UNVERIFIED** (KI-002, no KVM).
Suite: `src/WalkGame.Unity/Assets/Tests/EditMode/AccessibilityHeadlessTests.cs` (D-073).

| Item | What changed | Proven headlessly |
|---|---|---|
| Large text | `ShellView.Render(..., textScale)` scales title, body and button heights. The bootstrap passes `AccessibilitySettings.fontScale`, clamped to 1–2, and redraws on `fontScaleChanged`. | At ×2 on 6 screens: title 56, every label ≥ 36, buttons ≥ 112 high, all text wraps, no fixed widths. Clamp: 0.5/NaN → 1, 3.5 → 2. |
| Screen reader | `AccessibleOutline` (engine-free reading order) and `ScreenReaderBridge`, which publishes an `AccessibilityHierarchy` to `AssistiveSupport` only while a screen reader is on. Button nodes invoke the same action ids. | On 6 screens: header first; text before choices; every stop labelled; no content keys announced; the valley picture is read by its alt text; errors are announced as "Problem: …". Hierarchy has one node per stop with mapped roles; the primary action has a hint. |
| Empty states | Removed the generic 14 px "Nothing here yet". The screen's own guidance line is now the empty state, at body size. | Home, Activity and Return summary: the empty copy explains the next step (> 25 chars), is body size, and the screen has an action (no dead end). |
| 7-/30-day returns | Test seam `ShellBootstrap.CreateSession(dir, port, clock)`. | Walked while away (7 and 30 days): opens on the summary, primary action is Continue, which leads Home, with no guilt words. Idle 7 and 30 days: lands Home calmly, no guilt words, banked Vitality and completed projects never decay. |

Not new here: reduced motion is met by construction (the shell and valley picture have no motion; `ShellView` and `RegionScene` docs). "No network" is moot because the game is offline-only.
