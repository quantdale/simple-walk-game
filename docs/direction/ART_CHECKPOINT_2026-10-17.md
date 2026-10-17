# Art-direction checkpoint package (D-164 gate), 2026-10-17

> **Superseded (D-171, 2026-10-17 4:12 PM Asia/Manila).** Michael's Creative Direction Bible v1 (`CREATIVE_BIBLE_v1_2026-10-17.md`) replaced this round. None of A/B/C below was chosen, and recommendation B is withdrawn. The single art gate is now Phase B round 2: A Painted Storybook Valley (preferred), B Layered Paper-Craft Wonderland, C Painterly Miniature Diorama. This file stays as the record of what round 1 delivered.

**Status:** Delivered to Michael on 2026-10-17 at 3:30 PM Asia/Manila. **Awaiting Michael's choice.** The recommendation is **B**. Agents may not approve a direction (D-164). Until Michael picks one (or amends one), no mass asset production and no art-style-specific Unity UI rebuild may start.

This file is a text-only record. The images are **not** in the repository. They are AI-generated concept material and fall under neither allowed category in `docs/ASSET_LICENSES.md` (in-repo-created or CC0). Committing them would need a DECISIONS entry first (see the proposed D-170 licensing entry).

## Where the package lives (off-repo)

On the agents' shared box, at `/workspace/art-checkpoint/`:

| File | Content |
|---|---|
| `RECOMMENDATION.md` | Side-by-side scoring and the recommendation |
| `PROVENANCE.md` | Tool, date, prompt summary, source file and edits for every image, plus rejected generations |
| `A-painterly-2d/`, `B-iso-diorama/`, `C-lowpoly-3d/` | Each has `home.png` (gameplay screen), `dialogue.png` (Nell + dialogue with choices and Skip), `ruined_vs_restored.png` and `PIPELINE.md` (asset production, animation, ruined→restored approach, Unity 6 implementation, mobile performance, asset count, consistency risk, honest feasibility) |
| `comparison.png` | 3×3 contact sheet of all nine screens |
| `compose.py` | Pillow script that crops the generator output to portrait and overlays the mockup UI text |

Tool: Grok Bot's built-in image generator (no paid service). Mockup UI text was overlaid by script, not by the generator.

## The three concepts

| | A: painted layered 2D | B: cozy isometric diorama (**recommended**) | C: low-poly 3D tilt-shift |
|---|---|---|---|
| Look | Gouache-textured side-on valley with 4–6 parallax layers (in the vein of Ori, Alba, Gris) | The whole valley as one floating island under a fixed 2:1 isometric camera; sage/terracotta palette, dusk glow when restored | Faceted flat-shaded island seen from 45°, pastel light, tilt-shift depth of field |
| Ruined → restored | 2–3 pixel-registered state layers per location, dissolve-mask crossfade | Each location is a prefab with state variants (Ruined/Building/Restored/Thriving); module swaps with a construction tween on the same island | Mesh variants plus dissolve; terrain recolour driven by one vitality parameter |
| Production | Hand repaint in Krita; Unity 2D Animation rigs; about 170–200 sprites for Region 1 | Blender modular kits pre-rendered to iso sprites, Krita paintover; about 300 small sprites from about 60 models | Blender meshes, Rigify, blend-shape expressions; about 80–100 assets |
| Consistency risk | High unless one illustrator finishes everything | Low–medium (fixed camera/light rig makes consistency procedural) | Low |
| Mobile battery | Good | Best | Heaviest (shadows + DOF) |
| Main weakness | Not achievable at production quality without a human illustrator | First-pass Blender kits will look simpler than the concept images; portraits need illustration | Reads as generic "asset store"; faceted faces weaken the dialogue close-ups |

**Why B:** it is the only option that is both achievable with the free toolchain (Blender + Krita + Unity URP 2D isometric tilemap) and consistent across hundreds of assets. It also shows the brief's core loop best: the same island the player sees every day visibly fills in. Painted bust portraits are recommended for dialogue. **Runner-up:** A, if an illustrator can be funded or commissioned. **Not recommended:** C.

## Feasibility caveats (stated in the package; repeated here so they are not lost)

1. **AI concept images are not final art.** They are flat (no separable layers for parallax or state crossfades). Ruined and restored pairs are not pixel-registered (for example, A's intake structure differs between its two images). There is no consistent isometric grid or modular separation, and no limb separation for rigging. Every production asset has to be built or repainted.
2. **Portraits need illustration skill.** For any concept, dialogue portraits (about 1–2 characters × 5 expressions) need either a human illustrator or AI-assisted drafts with substantial hand cleanup. This is the one part of B that tools alone do not cover.
3. **Licensing decision required before any AI-derived asset ships.** Under `docs/ASSET_LICENSES.md` only in-repo-created or CC0 assets may be added without a DECISIONS entry. AI-generated or AI-derived images are neither, so none may be committed under `src/` or shipped until Michael accepts a licensing decision. The proposal is DECISIONS D-170, status *Proposed*, which is in the morning queue. CC0 kits (for example Kenney, Quaternius) are allowed under the existing rule if each one is recorded in the register.
4. **Concept A at production quality** is a blocker without a human illustrator: it would need one consistent hand for about 180 layered paintings.

## What happens next

- **Michael:** picks A, B or C (or amends one) and records the choice. Separately, accepts, amends or rejects the AI-art licensing proposal (D-170).
- **Agents, before the pick:** only style-neutral work. The Unity presentation adapter seam (D-169) binds the D-166/D-167 read models behind interfaces with a null presenter and a pluggable asset catalog that has a missing-asset fallback and no art. The text shell stays as it is.
- **Agents, after the pick:** an asset pipeline in the chosen direction with per-asset provenance, then the world-first Home, dialogue presentation, illustrated choices and visible transformations, implemented behind the D-169 interfaces.
