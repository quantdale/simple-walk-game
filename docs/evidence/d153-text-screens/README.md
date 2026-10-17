# D-153 text-screen captures (editor-rendered, NOT device)

`tools/unity/capture-text-screens.sh docs/evidence/d153-text-screens` (Unity 6000.3.26f1 batch mode, llvmpipe under Xvfb, 540x1170, panel scale 1.5), 2026-10-17, on main `ab6cb8b` (after D-152).
State `payoff`: fresh start, first project queued, one 2,000-step first walk.

- `home-payoff-editor-rendered.png` — Home in D-152 order: credited line, payoff line (6% restored, ~28,000 steps to go), invested line directly under it, activity, story, then "Unspent: …" last.
- `home-payoff-2x-editor-rendered.png` — the same at 2x text.
- `returnsummary-payoff-editor-rendered.png`, `settings-editor-rendered.png` — unchanged screens, recaptured so the set matches main.
- `TEXT_RUN.txt` — per-capture exit code and renderer (all rc=0).

Supersedes `docs/evidence/d151-text-screens/` for Home (that set predates the D-152 reorder).
Label: editor-rendered. Device rendering UNVERIFIED (KI-002).
