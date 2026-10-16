# Region 1 polish: first run and the empty/early-game states (grok0215, 2026-10-17)

These PNGs are editor-rendered (software GL, NOT device). `WG_CAPTURE_STATE=fresh` (new in `ValleyCaptureTests`) captures each screen right after "Begin restoring", with no walks and nothing queued.

Reviewed (`before/`): Onboarding, Guide (step 1 of 5), Home, Projects, Region, Story and Progress. Discoveries always uses the completed-region fixture; its empty state is pinned by `DiscoveriesScreenTests`.

Found and fixed (Home, presentation only):
1. A brand-new world opened with "Nothing has changed since you were last here." There is no "last time" on first run, so the line is now skipped until anything has happened (a summary, vitality, or a project).
2. "Choose the first thing to restore." and "Nothing is being restored yet. Choose a project to begin." said the same thing twice; the empty-state line is kept.
`after/home-fresh.png` shows the result. Pinned by `HomeLayoutPolishTests.First_run_home_has_no_since_last_time_line_and_asks_to_choose_once`.

Reviewed and left as is: Onboarding, Guide, Projects (one clear "Start: Clear the old trailhead"), Region (every landmark "still damaged", with a calm note for the producer), Story (the prologue plus the next chapter's hint) and Progress ("Your first walk will show up here. There is no rush."). The zero-resource line stays because it introduces the three resources.

EditMode 207/207 (`editmode-results.xml`).
