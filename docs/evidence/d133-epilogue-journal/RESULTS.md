# D-133: editor-rendered epilogue journal (grok0718)

The captures are editor-rendered: Unity 6000.3.26f1 PlayMode under Xvfb with Mesa llvmpipe, 540x1170. They are NOT device captures (KI-002).
State: `CompletedRegionFixture` (the real ingest pipeline; region completed). Capture: `WG_CAPTURE_SCREEN=EpilogueJournal [WG_CAPTURE_SCROLL=end] tools/unity/capture-valley.sh`.

- `journal-epilogue-top-BEFORE-...png`: the defect. The header read "9 of 10 chapters unlocked. Restoring the valley reveals the next one." after restoration, but chapter 9 (The Long Watch) comes from a season of tending.
- `journal-epilogue-top-...png`: the fix, "Tending the restored valley reveals the last one."
- `journal-epilogue-end-...png`: the D-129 epilogue (final entry, Nell's closing letter, "In the valley now" note), then Home.
- EditMode: 224/224 + 1 ignored on this branch (base c9b0b67), with `D133_progress_line_after_restoration_points_to_tending_not_restoring` passing.
