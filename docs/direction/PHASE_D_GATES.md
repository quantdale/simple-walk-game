# Phase D trailhead slice — Creative Bible §45–48 gate audit (2026-10-24)

Scope: the painted Old Trailhead slice (D-204 … D-212) on main `55311700`. Statuses: **PASS** (evidence shown),
**FAIL** (not met), **PARTIAL** (met inside the trailhead view, not across the app), **UNVERIFIED** (needs a device or real players).
All captures are **Unity editor captures (software GL), NOT device-tested** (KI-002). `E/` = `docs/evidence/d212-bone-rig/`
(the latest full capture set) unless noted.

## §45 Art approval gate

| Gate | Status | Evidence |
|---|---|---|
| Three original style explorations | PASS | Round 2 package A/B/C, `ART_CHECKPOINT_R2_2026-10-17.md` (images off-repo by rule) |
| One recommended direction with rationale | PASS | Same file + D-171; Michael picked A (D-200) |
| One coherent NPC design per concept | PASS | `npc_mira.png` per concept (off-repo); shipped Mira locked to the sheet (D-206, D-211) |
| Before/after restoration comparison | PASS | Concept: `damaged_vs_restored.png` (off-repo). In-engine: `E/trailhead-s0-forgotten-bridge-ruined` vs `E/trailhead-s3-restored` |
| Gameplay-and-dialogue mockup | PASS | Concept `gameplay.png`/`dialogue.png`; superseded by real captures `E/trailhead-dialogue-mira*` |
| Asset-production feasibility assessment | PASS | `FEASIBILITY.md` per concept; D-201 (agent-made final art) |
| No large-scale production before the checkpoint | PASS | Production started after D-200 (2026-10-24 2:47 AM UTC+8); Region 2 not started |

## §46 Vertical-slice acceptance gate

| Gate | Status | Evidence / gap |
|---|---|---|
| Runs in Unity | PASS (editor) | EditMode 380/380, PlayMode captures 2/2 (D-212). Device: UNVERIFIED |
| Real authored illustrated environment assets | PASS | 2 ground plates + 5 bridge layers + ripple, ledger rows with verbatim prompts (D-209) |
| A character appears and interacts naturally | PASS (editor) | Mira bone rig, idle + Help-restore reaction (D-212); tap Mira → dialogue `E/home-tap-mira-dialogue`; `E/trailhead-motion…gif` |
| Dialogue short, expressive, context-aware | PASS | 1–2 sentence lines by stage/bridge key, 5 expression portraits (D-206); `E/trailhead-dialogue-mira` |
| A real restoration project changes the environment | PASS | `proj.rebuild-trail-bridges` from real walking: `E/trailhead-s1-bridge-build-0of3` → `…-done` |
| Ruined and restored visibly distinct | PASS | `E/trailhead-s0-forgotten-bridge-ruined` vs `E/trailhead-s3-restored` (bare vs full crowns, murky vs clear water) |
| UI matches the art identity | **PARTIAL** | In-world dialogue/cards use the A palette and shapes; the text shell (first launch, Home, Region, return summary) is still the pre-A placeholder (`E/shell-*`) |
| The world dominates the screen | **PARTIAL** | Full-screen inside the trailhead; the app still boots to the text Home, and the world is one button away (`E/shell-home-visit-trailhead`) |
| Not a dashboard or visual novel | **PARTIAL** | Trailhead: world-first with taps (bridge, signpost, Mira). Home/Region text screens still read as a dashboard |
| Core state correctness preserved | PASS | No save/Domain/Application change in D-204…D-212; full EditMode suite green each PR |

## §47 Visual evidence gate

| Required capture | Status | Path |
|---|---|---|
| First launch / onboarding | PASS (text shell) | `E/shell-first-launch` (D-210) — not yet in direction A |
| Initial damaged world | PASS | `E/trailhead-s0-forgotten-bridge-ruined` |
| NPC interaction | PASS | `E/home-tap-mira-dialogue`, `E/trailhead-mira-reaction-help-restore` |
| Dialogue presentation | PASS | `E/trailhead-dialogue-mira`, `E/trailhead-dialogue-mira-text2x` |
| Project selection | PASS | `E/home-help-restore-goal-card` |
| Intermediate restoration | PASS | `E/trailhead-s1-bridge-build-1of3`, `E/trailhead-s2-functional` (real play) |
| Completed restoration | PASS | `E/trailhead-s1-bridge-done`, `E/trailhead-s3-restored`; S4 is key-driven (labelled) |
| Return-after-walking | PASS (text shell) | `E/shell-return-after-walking` (D-210) |
| Home | PASS (text shell) | `E/shell-home-visit-trailhead`, `…-text2x` (scrolled, D-210) |
| Region view | PASS (placeholder) | `E/shell-region-view` — procedural pre-A valley |
| Short gameplay/animation capture | PASS (editor, simulated time) | `E/trailhead-motion.unity-editor-capture.gif` (D-211/D-212) |
| Labelling concept vs editor vs device | PASS | Every capture carries an in-image caption; no device claims |

## §48 Final evaluation questions (agent assessment; no player test — UNVERIFIED with people)

| Question | Answer | Why |
|---|---|---|
| Recognisable as a cozy illustrated indie game? | Yes inside the trailhead; no on the text screens | See §46 UI rows |
| Charm and personality? | Yes (trailhead) | Painted plates, garlands/bench on completion, Mira's idle and reaction |
| Characters memorable and expressive? | Partly | Mira only; 5 expressions + reaction. Tavi/Rowan/Eli not yet drawn |
| Interact with the world naturally? | Yes (editor) | Tap bridge/signpost/Mira; touch on a phone UNVERIFIED |
| Walking produces meaningful visible change? | Yes | Steps → bridge build stages → stage plates (real-play captures) |
| Follow progress without reading paragraphs? | Yes inside the world | Bridge stages + goal-progress line; the Region view is still text-heavy |
| Worthwhile optional gameplay beyond dialogue? | **No (central gap)** | Inspect cards only; discoveries/expeditions exist only as text-shell screens |
| Leave without being penalised? | Yes | D-198: caps unchanged, calm welcome-back; nothing decays |

## Failures fixed in this round

- §47 missing captures (first launch, return after walking, Home scrolled to the trailhead, Region view, animation) → D-210, D-211, D-212.
- §46 "character interacts naturally": cut-out idle replaced by a weighted bone rig with a reaction → D-212.
- Mira sheet drift (loose side wisps) → D-211; motes were not rendering their intended settings (a D-208 line folded into a comment) → D-211.

## Open (cannot be closed by a small PR)

1. Text shell in direction A (Home, onboarding, return summary, Region) — §46 UI identity / world dominance / not-a-dashboard.
2. World-first boot (open on the painted world, text shell as an accessible layer) — §46 world dominance.
3. Optional in-world play beyond dialogue (e.g. a discovery to find at the trailhead) — §48 central gap.
4. Device verification of everything above (KI-002).
