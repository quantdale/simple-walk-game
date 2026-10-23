# Art checkpoint round 2 (Phase B, D-171 gate), 2026-10-17

**Status:** Produced and sent to Michael on 2026-10-17 at about 4:45 PM Asia/Manila. **Decided 2026-10-24 2:47 AM Asia/Manila:** Michael picked **A, Painted Storybook Valley** (D-200), accepted D-170 with scope (AI-generated art may ship with a ledger row per asset; round 1 images unusable) and decided the final layered art is agent-made from AI-generated art (D-201). The gate is closed; Phase C has started. The historical text below records the package as sent.

Michael has three open decisions:

1. **Pick a direction:** A, B or C (or amend one). The choice is recorded as a DECISIONS entry, and Phase C (style guide) starts after it.
2. **D-170, AI-assisted art licensing** (Proposed, not in effect): accept, accept narrowly, amend or reject.
3. **Who paints the final layered art.** The package states that our tools cannot produce layered, consistent, animatable final art (each `FEASIBILITY.md` §8).

The Unity 2D packages that concept A would need (2D Sprite, 2D Animation, PSD Importer; no URP) are deferred until the pick by D-177.

This file is a text-only record. The images are **not** in the repository. They are AI-generated concept art, not production assets, and fit neither allowed category in `docs/ASSET_LICENSES.md`. Do not commit them.

## Where the package lives (off-repo)

On the agents' shared box, at `/workspace/art-checkpoint-r2/`:

| Path | Content |
|---|---|
| `RECOMMENDATION.md` | Scoring table for A/B/C, a five-line summary, the recommendation and Michael's open decisions |
| `PROVENANCE.md` | Tools, date, prompts, reference chain and every accepted and rejected generation |
| `A-storybook/`, `B-papercraft/`, `C-diorama/` | Each has `gameplay.png`, `damaged_vs_restored.png`, `npc_mira.png`, `dialogue.png`, `ui_kit.png` and `FEASIBILITY.md` |
| `comparison.jpg`, `send/` | Contact sheet and the 16 JPEGs that were sent |
| `raw/`, `tools/` | Raw generator outputs (rejected ones marked `_rejected`) and the Pillow scripts that drew all UI and text |

Subject in all three: the Old Trailhead with the broken footbridge (the planned Phase D slice) and Mira the Trailkeeper. Before and after come from one split generation per concept, so they share a camera. Tools: Grok Bot's built-in image generator and Pillow. No paid services were used.

## The three concepts (summary of `RECOMMENDATION.md`)

| | A Painted Storybook Valley (**recommended**) | B Layered Paper-Craft Wonderland | C Painterly Miniature Diorama |
|---|---|---|---|
| Strength | Warmest, closest to bible §4–8, most expressive Mira | Cheapest and most consistent; best phone readability and performance | Best depth; adds a landmark (the lookout tower) |
| Risk | Painterly detail needs value control on phones | Reads more as a craft or kids' style, less cozy | Lighting consistency across stages; low-end phone cost; Mira small on screen |
| Art effort (slice / Region 1, person-days) | ~35 / 200–260 | ~27 / 150–200 | ~44 / 260–340 |
| Pipeline | Krita layered paint on a Blender camera guide; Unity 2D Animation + Sprite Skin rig; **no URP needed** (painted light, current built-in pipeline) | Rigid cut-out puppet | Same rig as A plus 2D lights, normal maps and depth of field |

Recommended borrowings that keep A's style: B's production discipline (separable parts, fixed pivots) and C's one background landmark per area.

## What may happen before the pick

Only style-neutral work: domain, persistence, ingestion, read models and seam hardening, plus tests. Not allowed: asset production, a style guide, an art-style UI rebuild, committing any AI image, adding style-dependent art packages (D-177), or Region 2 (D-011).
