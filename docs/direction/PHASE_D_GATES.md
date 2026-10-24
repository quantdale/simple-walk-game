# Phase D trailhead slice — Creative Bible §45–48 gate audit (2026-10-24)

Scope: the painted Region-1 places (Old Trailhead D-204…D-215, River Intake D-216…D-218, Canopy Grove D-222) on main. Statuses: **PASS** (evidence shown),
**FAIL** (not met), **PARTIAL** (met inside the trailhead view, not across the app), **UNVERIFIED** (needs a device or real players).
All captures are **Unity editor captures (software GL), NOT device-tested** (KI-002). `E/` = `docs/evidence/d212-bone-rig/`
(the latest full capture set) unless noted; `F/` = `docs/evidence/d215-shell-a/`; `I/` = `docs/evidence/d216-intake/`; D-214 refreshed the trailhead set in `docs/evidence/d214-fringe/`.

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
| A character appears and interacts naturally | PASS (editor) | Mira bone rig, idle + Help-restore reaction (D-212); tap Mira → dialogue `E/home-tap-mira-dialogue`; `E/trailhead-motion…gif`. Tavi at the intake (D-217 locked BODY-02 + bone rig + reduced-motion): see `docs/evidence/d217-tavi/intake-*` |
| Dialogue short, expressive, context-aware | PASS | 1–2 sentence lines by stage/bridge key, 5 expression portraits (D-206); `E/trailhead-dialogue-mira` |
| A real restoration project changes the environment | PASS | `proj.rebuild-trail-bridges` from real walking: `E/trailhead-s1-bridge-build-0of3` → `…-done`. Intake: `proj.river-intake` jammed → restored `I/intake-s0-gate-jammed-tavi` → `I/intake-gate-restored-plate-glow` |
| Ruined and restored visibly distinct | PASS | `E/trailhead-s0-forgotten-bridge-ruined` vs `E/trailhead-s3-restored` (bare vs full crowns, murky vs clear water) |
| UI matches the art identity | PASS (editor) | D-215: every text-shell screen is an A parchment card (ink-brown border, Fraunces/Nunito, Forest-deep primary) over the painted stage plate: `F/shell-first-launch`, `F/shell-home-visit-trailhead*`, `F/shell-return-after-walking`, `F/shell-region-view`. Deviation: primary is Forest deep, not Honey (charcoal on honey 5.81:1 misses the shell's 7:1 target) |
| The world dominates the screen | PASS (editor) | D-215: the app opens on the painted trailhead (once per launch; text shell first only under a screen reader, D-215 test); shell screens keep ≥ 42 % painted world above a compact card |
| Not a dashboard or visual novel | PASS (editor) | World-first with taps; shell screens are cards over the world. D-219 Home More; D-221 Region is map pins + compact card (not a text dashboard) |
| Core state correctness preserved | PASS | No save/Domain/Application change in D-204…D-212; full EditMode suite green each PR |

## §47 Visual evidence gate

| Required capture | Status | Path |
|---|---|---|
| First launch / onboarding | PASS | `F/shell-first-launch` (D-215, direction A) |
| Initial damaged world | PASS | `E/trailhead-s0-forgotten-bridge-ruined` |
| NPC interaction | PASS | `E/home-tap-mira-dialogue`, `E/trailhead-mira-reaction-help-restore` |
| Dialogue presentation | PASS | `E/trailhead-dialogue-mira`, `E/trailhead-dialogue-mira-text2x` |
| Project selection | PASS | `E/home-help-restore-goal-card` |
| Intermediate restoration | PASS | `E/trailhead-s1-bridge-build-1of3`, `E/trailhead-s2-functional` (real play) |
| Completed restoration | PASS | `E/trailhead-s1-bridge-done`, `E/trailhead-s3-restored`; S4 is key-driven (labelled) |
| Return-after-walking | PASS | `F/shell-return-after-walking` (D-215) |
| Home | PASS | `F/shell-home-visit-trailhead`, `…-text2x` (scrolled; D-210/D-215) |
| Region view | PASS (editor) | D-223: glyph pins + label plates (no mid-word wrap); `docs/evidence/d223-pinfix/shell-region-view` |
| Short gameplay/animation capture | PASS (editor, simulated time) | `E/trailhead-motion.unity-editor-capture.gif` (D-211/D-212) |
| Labelling concept vs editor vs device | PASS | Every capture carries an in-image caption; no device claims |

## River Intake (D-216) — labelled editor captures (`I/` = `docs/evidence/d216-intake/`)

| Moment | Status | Evidence |
|---|---|---|
| Walked from the trailhead, jammed gate, Tavi present | PASS (editor) | `I/intake-s0-gate-jammed-tavi` |
| Gate mid-rebuild (G1) from real play | PASS (editor) | `docs/evidence/d218-intake-stages/intake-gate-rebuilding-g1` |
| Stabilized + restored gate + find | PASS (editor) | `docs/evidence/d218-intake-stages/intake-s1-gate-restored-plate-glow` |
| Functional (WetlandDrainage, real play) | PASS (editor) | `docs/evidence/d218-intake-stages/intake-s2-functional` |
| Restored + flourish accents (CanopyWalkway, real play) | PASS (editor) | `docs/evidence/d218-intake-stages/intake-s3-restored` |
| Flourishing plate (key-driven) | PASS (editor, labelled) | `docs/evidence/d218-intake-stages/intake-s4-flourishing-keydriven` |
| Tap Tavi → dialogue (1× and 2× text) | PASS (editor) | `I/intake-dialogue-tavi`, `I/intake-dialogue-tavi-text2x` |
| Restored gate + optional find glowing (static, reduced motion) | PASS (editor) | `I/intake-gate-restored-plate-glow` |
| Tap the brass plate → find card (1× and 2× text) | PASS (editor) | `I/intake-find-card`, `I/intake-find-card-text2x` |
| Flourishing plate (key-driven) | PASS (editor, labelled) | `I/intake-s4-flourishing-keydriven` |
| Texture budget ≤ 12 MB | PASS (editor) | ~4.2 MB (`D216_TEXTURE_BYTES`) |
| Region 2 | NOT STARTED | — |

## §48 Final evaluation questions (agent assessment; no player test — UNVERIFIED with people)

| Question | Answer | Why |
|---|---|---|
| Recognisable as a cozy illustrated indie game? | Yes inside the trailhead; no on the text screens | See §46 UI rows |
| Charm and personality? | Yes (trailhead) | Painted plates, garlands/bench on completion, Mira's idle and reaction |
| Characters memorable and expressive? | Partly | Mira, Tavi, Rowan (D-222 locked body + bone idle, neutral portrait). Eli not yet drawn |
| Interact with the world naturally? | Yes (editor) | Tap bridge/signpost/Mira; touch on a phone UNVERIFIED |
| Walking produces meaningful visible change? | Yes | Steps → bridge build stages → stage plates (real-play captures) |
| Follow progress without reading paragraphs? | Yes inside the world | Bridge stages + goal-progress line; Region map pins show stage by structure (D-221) |
| Worthwhile optional gameplay beyond dialogue? | **Yes (three finds, editor)** | D-216 brass plate; D-220 millstone; D-222 understory orchids at Canopy Grove (`disc.understory-orchids`, `docs/evidence/d222-canopy/`). Expeditions still text-only |
| Leave without being penalised? | Yes | D-198: caps unchanged, calm welcome-back; nothing decays |

## Failures fixed

- §46 UI identity / world dominance / not-a-dashboard partials → D-215 (direction-A shell, world-first boot).

- §47 missing captures (first launch, return after walking, Home scrolled to the trailhead, Region view, animation) → D-210, D-211, D-212.
- §46 "character interacts naturally": cut-out idle replaced by a weighted bone rig with a reaction → D-212.
- Mira sheet drift (loose side wisps) → D-211; cream/white edge fringes on the hat brim, arm, staff and neck → D-214; motes were not rendering their intended settings (a D-208 line folded into a comment) → D-211.

## Open (cannot be closed by a small PR)

1. ~~Text shell in direction A~~ and ~~world-first boot~~ → D-215. ~~Painted Region map + lighter Home~~ → D-219.
3. ~~Optional in-world play beyond dialogue~~ → D-216 + D-220 + D-222 (orchids). Residual: more finds, expeditions in-world.
4. ~~Third painted place (Canopy Grove / Rowan)~~ → D-222. Residual: East Wetland / Eli, Settlement, Observatory.
4. Device verification of everything above (KI-002).
