# Add optional landmark letters (Nell Hartley's letters found at restored sites)

**Status:** Approved for implementation by the executing agent (D-118), on the same basis as D-103: Region 1 / shell scope. D-011 and the ROADMAP gate concern Region 2 only.

## Why

The ten D-076 chapters tell Millbrook's story at the scale of the valley. Players asked for more storyline depth tied to places. Short character notes found where a landmark is restored add a personal voice without new mechanics.

## What Changes

- `Region1Letters` (Application content): six letters from Nell Hartley, a Millbrook resident who left after the floods. Each is tied to one Region 1 project and names where it was found.
- `GameSession.GetLetters()` → `LettersReadModel`. A letter is found exactly when its project is `Completed`. This is a pure projection: no clock, no writes, monotonic. Unfound rows carry no text.
- Story journal: once one or more letters are found, it adds "Letters from Nell Hartley: N of 6 found." and each found letter in writing order. Nothing is shown or hinted before the first find.
- Discoveries: once one or more letters are found, it adds the same count line with "Read them in the Story journal."

## Non-goals

No reward, collectible pressure, notification, streak or timer. No content-fingerprint or save change. No Region 2 content.

## Compatibility

No save schema change, no content version change; existing saves show letters for projects they have already completed.
