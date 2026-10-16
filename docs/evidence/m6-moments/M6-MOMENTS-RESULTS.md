# Return-summary restoration moments (2026-10-16), UX_DESIGN §4 and §19

Label: **Unity-editor-tested**. On-device rendering: **UNVERIFIED** (KI-002).

| Check | Result |
|---|---|
| EditMode | 39/39 (4 new `ReturnMomentTests`; the transformation scenario ran for real, with no inconclusive results) |
| PlayMode | 2/2 |

Behaviour:
- When the pending summary has a **current** Transformation item, the "While you were away" screen leads with "The valley changed while you were away." and shows the valley picture.
- Each landmark whose title appears in a transformation line gets a static ring. The alt text says how many are circled.
- Items older than 7 days read "Earlier: …".
- The primary action is still "Continue" (dismiss), and Home stays available. There is no animation and no blocking sequence (§19 minor/landmark moments, reduced-motion compliant by construction).

Limitation: summary items carry text only, so landmarks are matched by player title. A future summary schema with landmark ids would make this exact.
