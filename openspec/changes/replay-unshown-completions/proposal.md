# Replay every unshown completion, whatever its timestamp (KI-028; D-180)

## Why
Completions are stamped with the device clock. Presentation memory (D-175) kept only a
"shown through" instant, so after a clock was corrected backward a later completion got an
older stamp than that instant and was never replayed (KI-028). The stream also sorted it
before the prerequisite it depends on.

## What changes
- Every change event carries a stable `CompletionId` (the completed project's id, or
  `region`); all events of one completion share it.
- Presentation memory file format v2 adds `shownCompletionIds` (bounded, pruned to the loaded
  game's completions). v1 files still load: the missing field is derived once from the old
  timestamp mark. The file stays outside the save, is erased by "Delete all local data" and is
  not exported.
- Resume replays every completion not yet recorded as shown (falling back to the timestamp
  rule only when nothing is recorded), then records it.
- The stream never places a project before a completed prerequisite.

No save, schema, content or crediting change.
