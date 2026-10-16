# Story journal (D-076, storyline A4): evidence

**Unity-editor-tested.** Device runtime is UNVERIFIED (KI-002).

- `editmode-results.xml` / `editmode-run.log`: EditMode suite, 61/61 passed. That includes 5 `StoryJournalTests`:
  - prologue-only journal for a new game, with no locked text leaking;
  - no guilt or pressure copy;
  - a chapter moment in the return summary, with "Read the new chapter" placed after the primary Continue;
  - no journal link when a summary has no chapter moment;
  - screen-reader outline order.
- `journal-editor-rendered.png` (editor-rendered): the Story screen after a six-day synthetic walk, rendered in the editor with Mesa llvmpipe under Xvfb (`WG_CAPTURE_SCREEN=Story tools/unity/capture-valley.sh`). It is not a device screenshot.
- `capture-results.xml` / `capture-run.log`: that capture run. Machine and licence identifiers are redacted.
