# Add the Region 1 epilogue (closing letters, final journal entry, valley notes)

**Status:** Approved for implementation by the executing agent (D-129), as Region 1 scope on the same basis as D-103/D-118. D-011 and the ROADMAP post-MVP gate concern Region 2 only; this change adds no Region 2 content or hook (`add-region2-storyline` stays PROPOSED).

## Why

After Millbrook's closure milestone the Story journal ends on one static line. Players who keep walking have nothing new to read. A short, calm epilogue rounds off the story and gives gentle long-term texture without goals.

## What Changes

- `Region1Epilogue` (Application content): a final journal entry, two closing letters from Nell Hartley (at completion and 7 days later), and eight valley notes (one per 30 days after completion, cycling forever as "In the valley now").
- `GameSession.GetEpilogue()` → `EpilogueReadModel`: a read-only projection of `RegionCompletedAtUtc` and the trusted clock. Unavailable (empty, no text) before completion. A backward clock shows the day-0 epilogue; shown entries never disappear while the clock moves forward.
- Story journal: after the letters, an "Epilogue" block with the entry, letters, the current note and earlier notes. No counts, "N of M", notifications or rewards.
- Valley notes describe the valley turning through the year without naming calendar seasons, so they read true in either hemisphere.

## Non-goals

No reward, collection count, streak, timer or notification. No save, schema or content-fingerprint change. No Region 2 teaser.

## Compatibility

No save change. Existing completed saves show the epilogue at once, with notes for the time already elapsed since completion.
