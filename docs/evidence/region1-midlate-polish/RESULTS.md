# Region 1 polish: mid-game and late-game (grok0242, 2026-10-17)

These PNGs are editor-rendered (software GL, NOT device). New capture states in `ValleyCaptureTests`: `WG_CAPTURE_STATE=mid` (seeded `CompletedRegionFixture` pipeline stopped at day 30) and `late` (day 70); `WG_CAPTURE_DAY` overrides. 20,000 steps a day, one project queued whenever idle.

Reviewed (`before/`): Projects with many options at day 30 (top and scrolled to the end), Story partway through (5 of 10 chapters, 3 of 6 letters; top and end), Progress at a long-run save (day 70: 16 of 19 restorations, 14,000 vitality), Home at day 70.

Found and fixed (Projects, presentation only, D-127):
1. With nine restorations done, the project being restored and the two ready-to-start options sat below the whole restored history; a player had to scroll past nine finished rows to see what they could do. Rows are now ordered: being restored, waiting, ready to start, restored.
2. Restored rows repeated "(300 of 300 vitality)". They now say just "restored".
`after/projects-mid.png` and `after/projects-mid-end.png`. Pinned by `MidGamePolishTests` (EditMode).

Reviewed and left as is: Story (clear count line, chapters then letters; reads well), Progress (short, honest, no guilt), Home (one underway line, story line with next hint).

Observed, not fixed: in `before/story-mid-end.png` there is a slightly larger gap above the third letter. The line list has no blank entry; it looks like editor text measurement under software GL. Device rendering is UNVERIFIED (KI-002).

EditMode 215/215 (`editmode-results.xml`, Unity-editor-tested).
