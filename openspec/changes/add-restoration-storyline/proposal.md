# Add a restoration storyline (chapters gated by restoration milestones)

## Why

Millbrook Valley has landmarks, discoveries and a closure milestone, but no story *spine*: nothing tells the player what the valley was, why it fell quiet, or what their walking is bringing back. WORLD_AND_CONTENT §11 asks for "short story fragments tied to major restorations" and "a strong region completion beat". Discoveries are fragments of history, but they are not ordered and they don't build. Players who walk little (the product's core audience) need a reason to come back that isn't a number. A next chapter is that reason, provided it is never withheld as punishment.

## What Changes

- **Authored chapters** (`StoryChapterDefinition`) on the region: number, title, a 2–4 sentence body, and an unlock criterion made only of **restoration milestones**: required completed projects and, for the epilogue, completed post-completion care cycles. Never raw step counts or streaks.
- **Cumulative ordering:** chapter *n* unlocks only when its own criterion holds **and** chapter *n−1* is unlocked, so the story always reads in order, whatever order the player restores in.
- **Derived, not stored:** whether a chapter is unlocked, and when (the latest completion time among the milestones in its chain), is computed from canonical state that saves already carry. No save-schema change and no migration. Content version 4 → 5 is additive, so older saves classify as CompatibleOlder (D-059).
- **No guilt and no decay:** a chapter, once unlocked, can never be locked again. Restoration is monotonic, and the derivation is checked by tests. Locked chapters are teased with their title only and a gentle hint naming the next restoration. They never mention time, streaks or missed days.
- **Surfaces:**
  - an application `StoryReadModel` (chapters, unlocked flags, next hint);
  - a Unity **Story** screen (journal);
  - "New chapter: …" **chapter moments** in the durable return summary when a batch unlocks a chapter.
- **Content validation:** unique ids; contiguous numbers from 0; every required project exists; the final pre-epilogue chapter requires the closure milestone; bodies within the glance budget.
- **Cadence gate:** for every pacing profile, chapter unlock days are measured (`profile --story`), and tests bound the first chapter and the longest gap between chapters.

## Non-goals

Dialogue trees, voice, branching choices, localisation beyond keys, network delivery of story content, and per-chapter "read" tracking in the save. Read state, if ever added, belongs in local UX preferences.
