# Add the painted Old Trailhead slice (D-204, Phase D)

## Why
Direction A is approved (D-200) and AI-derived art may ship with a ledger row (D-170, D-201). Phase D needs the Old Trailhead and its
footbridge visible in Unity, with ruined-to-restored structural change and Mira's skippable dialogue, through the existing seams.

## What changes
- Unity Shell: `TrailheadArt` (pure key → layer mapping, fallbacks, reduced-motion rule), `TrailheadSceneView` (presenter for
  `lm.trailhead` and `npc.mira`), `TrailheadDialoguePanel` (UI Toolkit, text in-engine), editor import rules.
- Art: 9 PNGs under `Assets/Resources/Art/Trailhead/`, one provenance row each.
- Strings: `dialogue.action.skip` = "Skip".
- No Domain/Application/content/save-schema change. Not wired into the boot scene.
