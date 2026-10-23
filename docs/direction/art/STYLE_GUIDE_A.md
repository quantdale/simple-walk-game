# Phase C style guide — Concept A, Painted Storybook Valley

> **Status:** Accepted, canonical (Phase C, bible §41). Direction A approved by Michael 2026-10-24 (D-200); AI-derived art may
> ship with one provenance ledger row per asset (D-170 accepted with owner scope); the agent makes the final layered art from
> AI-generated art (D-201). Landed by D-202. Written 2026-10-24 (Asia/Manila, UTC+8).
>
> **Governing source:** Creative Direction Bible v1 (`docs/direction/CREATIVE_BIBLE_v1_2026-10-17.md`), §41 Phase C list,
> with §45–48 gates as the definition of "ready for Phase D". Inputs: redesign brief 2026-10-17, art checkpoint round 2
> (A-storybook images, FEASIBILITY.md, RECOMMENDATION.md, PROVENANCE.md), and read-only `quantdale/simple-walk-game`
> `origin/main` (D-169 seam, D-170 as accepted, D-176 bridge keys, D-177 package deferral, KI-027).
> Helper files in this folder: `palette_storybook_A.gpl` (Krita/GIMP palette), `provenance_ledger_template.csv`,
> `trailhead_asset_list.csv`.

Bible §41 coverage map: camera §2 · proportions §6–7 · outlines §3 · texture §4 · palette §1 · lighting §5 · typography & UI
§9 · animation §8 · asset dimensions & naming §7 · render/export §10 · licensing §10.5/§11 · accessibility §9.5 · quality §12 ·
reusable production template §10.6 + helper CSVs.

---

## 0. Non-negotiables (one screen)
1. One fixed camera per location. Every restoration stage of a location is painted/registered on **the same canvas**, so stage
   art overlays pixel-for-pixel (§2.3).
2. Every stage change is **structural first, color second** (§1). A grayscale screenshot of stage *n* and *n+1* must still differ.
3. Light is **painted in**. No URP, no normal maps, no realtime 2D lights (built-in pipeline, per D-177). Only sprite tint and
   additive glow sprites.
4. Mira is drawn from **one locked model sheet** and one rig; portraits are rendered from the rig, never painted separately (§6).
5. Assets are addressed only by the existing D-169/D-176 keys (§7.1); presentation never writes game state.
6. No unrecorded or unprocessed generator output ships. Every AI-derived file is layered and cleaned up in Krita/GIMP (§10) and has exactly one ledger row (D-170, D-201).
7. Prompts never name artists, studios, games, franchises or trademarked characters (D-170 rule 3).

---

## 1. Palette per restoration stage (0–4)

### 1.1 Base swatches (bible §8, unchanged hexes)
| Name | Hex | Primary job |
|---|---|---|
| Warm canvas | `#F5EBDD` | UI panels, dialogue, paper base |
| Soft charcoal | `#3D4038` | text, darkest ink |
| Faded earth | `#A89B88` | ruined terrain |
| Dull moss | `#858D72` | early regrowth, stage 0–1 foliage |
| Meadow green | `#83A878` | restored grass/vegetation |
| Forest green | `#527A5B` | mature foliage, deep shade |
| Soft river blue | `#83B8C4` | flowing water |
| Honey gold | `#E5BA69` | sunlight, new timber, highlights, "tap me" |
| Clay orange | `#CB866C` | buildings, accents, Mira's vest |
| Dusty pink | `#DDA8AA` | flowers, decoration, cheeks |

Derived (kept in `palette_storybook_A.gpl`, flagged DERIVED; owner may veto): Ink brown `#5A3E2B` (main contour), Forest deep
`#3F6248` and Clay deep `#8E5240` (UI fills that pass contrast with canvas text), Haze beige `#C4BCAC` (stage-0 sky/air),
Murky water `#7A8070` (stage-0 water).

### 1.2 Stage table — color AND mandatory structural change
Repo enum (`RestorationStage`) ↔ bible name ↔ key suffix: 0 Ruined/Forgotten `@ruined` · 1 Stabilized/Awakening `@stabilized` ·
2 Functional/Recovery `@functional` · 3 Restored `@restored` · 4 Flourishing `@flourishing`.

| Stage | Dominant ramp (≈ share of painted area) | Accent budget | Value/saturation rule | **Structural change required (color-blind readable)** | Ambient motion |
|---|---|---|---|---|---|
| **0 Forgotten** | Faded earth, Dull moss, Haze beige, Murky water (~85 %) | ≤ 3 % Honey (one weak sun patch); no pink | Chroma capped ≈ 25 % of stage 4; low contrast far planes (haze) | Debris, broken/leaning structures, bare or half-bare crowns, sparse dry grass, gaps in paths, still water, no wildlife | Very slow drifting leaves only; water ripple speed 0.25× |
| **1 Awakening** | + Dull moss→Meadow mix in grass overlays; water lighter | Honey ≤ 6 %; first Dusty pink specks (≤ 1 %) | Chroma ≈ 45 %; haze lifts by one value step | Debris cleared, new posts/props *standing*, first sprouts and small flower clumps, path edges visible, budding crowns | Ripple 0.5×; 1 butterfly |
| **2 Recovery** | Meadow + Forest, Soft river blue replaces murky water | Honey/Clay ≤ 10 %, pink ≤ 3 % | Chroma ≈ 70 %; clear near/far value separation | Structures half-rebuilt and **usable** (e.g. half span), path continuous, full crowns on ≥ half of trees, reeds at banks, water visibly flowing | Ripple 1×; birds pass occasionally |
| **3 Restored** | Full palette; Forest green shade masses | Clay/Honey/pink ≤ 15 % | Chroma ≈ 90 %; warm window/sun glows allowed | Structures complete with rails/details; distinct materials (fresh timber vs old stone); wildlife present; signage legible as shapes | Ripple 1.25×; birds, insects, sway |
| **4 Flourishing** | Full palette, richest but harmonious | ≤ 20 % accents, never neon; no new hues outside palette | Chroma 100 % (palette max) | Mature growth, community decorations (garlands, bench, lanterns), worn-in path, NPC using the path, extra species | Ripple 1.5×; ≤ 60 particles; garland flutter |

Rules: stage art is authored with these ramps — not produced by a desaturation shader over stage-4 art (that reads as "filter",
bible §38). An optional global stage tint (SpriteRenderer color multiply, ≤ 8 % shift) is allowed only to smooth crossfades.
QA check: export each stage in grayscale; a reviewer must name the stage from structure alone (§12).

---

## 2. Camera and framing

### 2.1 View
Three-quarter elevated oblique, ≈ 35° look-down, no perspective convergence on vertical posts beyond slight painterly taper.
Light key from upper-left (sun ~10 o'clock) for every asset, every stage. Path/bridge run away from camera (bottom-center →
upper-center), as in the accepted round-2 diptych (horizontal bridges were rejected: don't fit 9:16 with Mira).

### 2.2 Fixed framing spec (per location)
| Item | Value |
|---|---|
| Target device frame | 1080×1920 portrait (9:16); safe-area aware |
| Master paint canvas | **2160×3840 + 15 % pan margin → 2484×4416 px** (2× of 1242×2208 scene) |
| World units | **PPU 200 at master (=100 at 1×)**; scene = 12.42 × 22.08 units |
| Unity camera | Orthographic, size 9.6 at 1920 px tall (pixel-perfect at 1×), pan clamp = margin only, no zoom in slice |
| Horizon line | 18 % from top of master canvas (constant for all stages) |
| Origin | Bottom-center of master canvas = world (0,0); every exported layer keeps its canvas offset |
| Primary landmark box | Bridge occupies x 35–65 %, y 25–75 %; nothing interactive in top 10 % / bottom 14 % (UI zones) |
| NPC anchor | Mira feet at `loc.trailhead` anchor (≈ x 50 %, y 72 % of view), in-world height 18–22 % of view height |

### 2.3 Pixel-for-pixel registration procedure
1. Grey-box in **Blender** (free): ground, river bed, bridge posts, signpost, log; one camera, locked; render `camera_guide.png`
   (line + depth) at master size. This file is versioned and shared by every stage.
2. All stages live in **one master file** `trailhead_bridge.kra` (Krita), one group per stage, same canvas size, never cropped.
3. Registration marks: 4 corner crosshairs on a hidden `_reg` layer; exported layers are full-canvas-offset (Krita export or
   PSD importer keeps position), then trimmed by Unity (tight mesh) — positions come from the importer, not hand placement.
4. Unchanging pieces (riverbed silhouette, horizon, far hills) are **one shared layer** used by all stages, so drift is impossible.
5. Check: difference-blend stage *n* over stage 0 on the `_static` layers → must be 0 px delta; overlay `camera_guide.png` at
   50 % on every stage before export (§12).
6. AI-assisted paint passes must use `camera_guide.png` (+ previous stage) as the **reference/control image**; any generation that
   moves a static element > 4 px at master is rejected, not "fixed by eye".

---

## 3. Linework
- Hand-inked look, rounded organic curves, tapered ends. Colors: Ink brown `#5A3E2B` (default), Soft charcoal `#3D4038`
  (characters' darkest accents, UI text), Forest deep `#3F6248` (foliage contours). Never pure black.
- Weight chart at **1× phone scale** (double at master): silhouette contour 3–5 px, internal major forms 2 px, detail 1–1.5 px,
  far plane 1.5–2 px and lighter (ink at 60 % opacity tinted toward haze). Characters' outer contour +1 px over props of same depth.
- Restrained internal detail; exaggerate contours selectively (hat brim, bridge planks ends). No uniform vector strokes.
- Krita brush pack (locked, versioned `brushes_A_v1.bundle`): `A_ink_taper`, `A_gouache_flat`, `A_wash_wet`, `A_grain_dry`.

## 4. Texture and materials
- Grain is painted **into each asset** (not a global overlay; bible §6). Grain amplitude ≤ 6 % luminance at 1×.
- Wood: dry-brush streaks along grain, warm honey on new timber, gray-brown + cracks on old. Foliage: clustered gouache dabs
  with 2–3 value steps, asymmetric crowns. Water: flat wash + painted ripple strip (UV scroll). Stone/soil: simplified planes.
- No photo textures, no 3D-rendered surfaces, no mixing of photoreal and painted assets.

## 5. Lighting (painted-in; no URP)
- Built-in pipeline, default sprite shader + (optional) one tiny custom unlit shader for water UV scroll (D-177: A needs no URP).
- Key light upper-left warm, fill cool from sky; contact shadows painted as soft Forest-deep/Ink washes at 25–35 % opacity.
- Stage light mood is painted: 0 hazy flat; 2 clear; 4 golden low sun. Window/lantern glows = separate additive sprites
  (`fx_glow_*`), toggled by stage; ≤ 3 visible at once.
- No normal maps, no realtime shadows, no post-processing bloom/DOF.

---

## 6. Mira — locked model sheet spec (`npc.mira`)

### 6.1 Drift found in round 2 and the resolution
Round 2 images (sheet, diptych, dialogue bg) varied: hair read as loose curls + braid on the sheet vs braid-only elsewhere; leaf
on hat missing at small size; vest pockets count/colour varied; scarf length and colour (moss vs green-teal) varied; staff top
(knot vs twine wrap) and satchel side varied. **Locked answers below override all concept images.**

| Feature | LOCKED spec |
|---|---|
| Hat | Very wide floppy straw sun hat, Honey gold `#E5BA69`, brim width = 1.9× head width, brim dips lower on her right; Clay orange `#CB866C` band; **one** Meadow-green leaf tucked on her **left** side of the band (always visible, enlarged at small sizes) |
| Hair | Dark auburn (`#7A3E2B` DERIVED). Short side-swept bangs; **one** thick 3-strand braid over her **left** shoulder to mid-chest, tied with a clay band. **No loose curls at the back** (resolves drift) |
| Face | Warm brown skin, freckles across nose (5–7 dots, fixed pattern), Dusty-pink cheeks, large round dark eyes with 1 highlight |
| Scarf | Long knitted scarf, Dull moss `#858D72` with Meadow ribbing; wrapped once, **one** tail over right shoulder, tail length ≈ 0.6× her height (animatable) |
| Shirt | Cream (canvas `#F5EBDD` darkened 6 %), sleeves rolled to elbow |
| Vest | Clay orange canvas work vest, **2 chest pockets + 2 hip pockets**, ink-brown stitching, open front |
| Satchel | Worn leather (Ink brown mid), crossbody strap from **left shoulder to right hip**, bag sits on **right** hip; mallet handle and rope coil poke out of the top, always |
| Legs | Brown knee-length trousers, chunky laced boots with honey laces |
| Staff | Tall carved wooden walking staff, held in **right** hand, top 15 % wrapped in twine with a **knot** at the top; staff top reaches ≈ hat-brim height |

### 6.2 Proportions & silhouette
- Height = 3 heads (head ≈ 1/3), compact sturdy torso, short simplified limbs, hands 0.4 head.
- In-world height 18–22 % of view (≈ 350–420 px at 1×). Portrait bust rendered at 2.5× rig scale.
- Silhouette test: black fill at 64 px tall must still read hat + staff + scarf tail; never let staff overlap hat outline in idle.

### 6.3 Expressions (face swap set; keys `portrait.npc.mira@<expr>`)
Repo already uses `neutral`, `glad`, `worried`, `proud`, `celebration`. Map: neutral (resting smile), glad (= "happy"),
worried (= "concerned/thoughtful", hand-on-chin bust optional pose), proud (warm closed-mouth smile, chin up), celebration
(eyes closed-happy, open laugh, arms up). Minimum for slice (bible §13/§33): neutral, glad, worried + celebration.
Face parts: eyes {open, half, closed-happy, look-side}, brows {neutral, worried, raised}, mouths {smile, talk A/B/C, flat-worry,
big-laugh}.

### 6.4 Animation clips (Unity 2D Animation + Sprite Skin — package add pending per D-177)
idle (breathe 3.2 s loop, scarf/braid sway, blink every 3–6 s random) · talk (mouth A/B/C loop 8 fps + small head bob) ·
expression change (0.15 s squash) · celebrate (1.2 s, skippable) · acknowledge (turn toward bridge + nod, plays on
`ProjectCompleted` for `proj.rebuild-trail-bridges`). Reduced motion: celebrate → expression swap only, no hat bounce; sway amplitude 0.

### 6.5 Other cast (placeholders — NOTES ONLY, no sheets in Phase C/D)
- **Rowan (`npc.rowan`)**, gardener: silhouette idea = broad-brim cloth hat + seed apron; Meadow/Forest dominant. Not designed.
- **Tavi (`npc.tavi`)**, tinkerer: goggles on head, tool belt; Clay/brass dominant. Not designed.
- **Eli (`npc.eli`)**, naturalist: tall, satchel of notebooks, binocular strap; River blue dominant. Not designed.
- Rule: each must differ from Mira in silhouette class (no second wide-brim hat), and gets its own locked sheet before any art.

---

## 7. Layered-asset spec

### 7.1 Asset keys (align with existing seams — do not invent new ones)
| Subject | Keys (from repo) | Notes |
|---|---|---|
| Trailhead landmark | `lm.trailhead@ruined/@stabilized/@functional/@restored/@flourishing`, subject fallback `lm.trailhead` | `RestorationStage` via `VariantKeyFor` |
| Bridge project prop | `proj.rebuild-trail-bridges@ruined`, `@build-0of3`, `@build-1of3`, `@build-2of3`, `@done` | D-176, content v6, 3 visible build stages |
| Trailhead clearing project | `proj.clear-trailhead@ruined`, `@build-0of1`, `@done` | **N = 1** (derived: `Region1Catalog` gives it no `visibleBuildStages`, default 1, so it has a single in-progress key; `ConstructionKeyFor` is only emitted for N > 1, `ProjectVisualKeyFor` yields `@build-0of1` while Active) |
| Portraits | `portrait.npc.mira`, `portrait.npc.mira@<expr>` | DialogueBeat.Expression |
| Global fallback | `fallback.missing` | must exist as a neutral painted card, never "placeholder geometry passed as art" |

The resolver's MissingKeys list = the to-make list; Phase D must end with zero Missing for the slice keys.
One key → one **prefab/part-set**, not one flat PNG: the catalog value is a part-set root whose child layers are toggled.

### 7.2 PSD/KRA layer naming
`<grp>/<subject>__<part>__S<stages>__<z>` e.g. `bridge/bridge__plank_07__S3-4__z320`, `env/ground__near_bank__Sall__z100`.
- Groups: `_reg`, `_guide`, `sky`, `far`, `mid`, `env` (ground/path), `water`, `bridge`, `props`, `veg`, `npc_mira`, `fx`.
- Stage tag: `S0`, `S1-2`, `Sall`, or bridge build tag `B0`..`B4` (ruined, build-0of3, 1of3, 2of3, done).
- `z` = sorting order inside sorting layer. Hidden/working layers start with `_` and never export.
- Character parts: `mira__<part>` with pivot hint suffix `__pv<x>_<y>` (normalized) e.g. `mira__arm_upper_R__pv0.50_0.92`.

### 7.3 Sorting layers (Unity)
`Sky` → `Far` → `Mid` → `Ground` → `Water` → `World` (Y-sorted: props, bridge, NPC) → `Foreground` → `FX` → UI (UI Toolkit, separate).
Y-sort pivot = object foot/contact point. Foreground occluders never cover a tappable target by > 30 %.

### 7.4 Pivots
Environment parts: bottom-center contact point. Bridge posts: base. Planks: own center (for drop-in overshoot). Crowns:
trunk attach point. Mira parts: joint centers (shoulder, elbow, hip, knee, neck, hat crown base, braid root, scarf knot).

### 7.5 Sizes / PPU
| Class | Master px (2×) | 1× px | PPU (master) |
|---|---|---|---|
| Sky/far plates | 2484×1600 (opaque, may be ½-res upscaled) | 1242×800 | 200 |
| Ground plate | 2484×2800 (opaque) | — | 200 |
| Bridge part (plank/post/rail) | 120–600 long | — | 200 |
| Trees (crown + trunk) | ≤ 900×1100 | — | 200 |
| Small props/flowers | ≤ 256² | — | 200 |
| Mira in-world part set | full body ≈ 840 px tall | ≈ 420 | 200 |
| Mira portrait bust | 1024×1024 render target | 512² shown | n/a (UI) |
| Icons | 256² master | 96dp | n/a (UI) |
Texture import: max size 2048, ASTC 6×6 (Android) / ASTC 6×6 (iOS), mipmaps off for world sprites, on for nothing.

### 7.6 Atlas & memory budgets (trailhead slice)
| Atlas | Size | Content | GPU (ASTC 6×6 ≈ 1.9 MB each) |
|---|---|---|---|
| `atl_trailhead_static` | 2048² | sky/far/ground shared + S0 overlays | 1.9 MB |
| `atl_trailhead_stages` | 2048² | regrowth overlays S1–4, crowns, flowers, reeds | 1.9 MB |
| `atl_trailhead_bridge` | 2048² | bridge kit + debris + signpost + log/bench | 1.9 MB |
| `atl_mira` | 2048² | parts + face swaps | 1.9 MB |
| `atl_fx` | 1024² | glows, leaves, birds, sparkles | 0.5 MB |
| `atl_ui` | 2048² | 9-slices, icons, portrait frames | 1.9 MB |
**Budget:** ≤ 12 MB GPU textures for the slice scene, ≤ 6 MB per additional visible area (region-aware loading keeps current +
neighbour only). Batches < 60, ≤ 4 full-screen alpha layers, particles ≤ 60 live, 60 fps target mid-range Android, 30 fps
floor low-end. Opaque plates use opaque import. All of this is **UNVERIFIED on device** until measured (KI-002 rule; editor
numbers are not proof). Reconciled: `docs/PERFORMANCE_BUDGETS.md` §9 now carries these numbers (12 MB slice / 6 MB per extra area, PPU 200 master = 100 at 1×); the six atlases above total ≈ 10 MB, leaving ≈ 2 MB headroom.

### 7.7 Unity packages (D-177; added in the package PR after this guide)
`com.unity.2d.sprite`, `com.unity.2d.animation`, `com.unity.2d.psdimporter`, Animation + Particle System modules; single reviewed
PR, with IL2CPP arm64 build proof. Krita exports `.psd` (PSD importer reads it); keep `.kra` as source of truth.

---

## 8. Animation conventions
- Environmental (bible §24): sway 2–4° amplitude, 3–6 s periods, random phase per instance; water UV scroll speed per stage (§1.2).
- Restoration transitions (bible §25), bridge per build step: debris sinks/fades 0.4 s → posts rise 0.5 s ease-out → planks drop
  in sequentially 60 ms apart with 6 % overshoot → rails draw on 0.4 s → stage-ramp crossfade of surroundings 0.8 s → Mira
  acknowledge. Total ≤ 3 s, skippable by tap. The saved state is never driven by animation (D-169: nothing waits on playback).
- Replay of changes earned while away plays once, oldest first, `ProjectCompleted` first per batch (D-175/D-176/D-180 behaviour).
- **Reduced motion** (passed by controller): transitions become a 0.3 s crossfade, no overshoot, no particles, ambient sway off,
  water ripple static texture.

---

## 9. UI treatment (UI Toolkit, already in project)

### 9.1 Visual language
Warm canvas parchment panels, soft irregular painted border (9-slice, ink-brown 3 px hand line, corner radius ~16dp feel),
subtle grain inside. UI is part of the painting but sits on top of the world; the world stays visible behind dialogue (bible §15).

### 9.2 Components
- **Dialogue box:** bottom sheet, max 34 % of screen height, portrait bust left (96–120dp) breaking the frame top edge, name tag
  on a Clay-deep ribbon, 1–2 lines (≤ 110 chars), up to 3 action buttons. Tap anywhere = advance; "Skip" always visible.
- **Project card:** thumbnail pair (current key art / `@done` preview, same framing crop), title, cost chip (steps/Vitality icon),
  1-line consequence, primary button. Before/after thumbnails must come from the registered stage art, never repainted.
- **Chips:** walk chip, resource chip, connection dot. **Buttons:** primary Honey gold fill + charcoal label (5.81:1),
  secondary canvas fill + Forest-deep border/label (5.84:1), destructive none in slice.
- **Tap highlights in world:** honey glow ring + gentle pulse (static ring under reduced motion), plus an icon badge (not color only).

### 9.3 Sizes
All touch targets **≥ 48×48dp** (hit area may exceed visual); ≥ 8dp spacing between targets. Bottom UI respects safe area.

### 9.4 Type
Display: **Fraunces** (OFL); body/UI: **Nunito** (OFL) — both free and **approved** (D-202); each is listed in `docs/ASSET_LICENSES.md` in the PR that adds its font files.
Body 16sp min (dialogue 17sp), labels 14sp min, display 22–28sp. Line length ≤ 40 chars on phone. Supports system font scaling
to 200 %: dialogue box grows and scrolls rather than truncating.

### 9.5 Accessibility & contrast (computed, WCAG 2.x)
| Pair | Ratio | Use |
|---|---|---|
| Charcoal `#3D4038` on canvas `#F5EBDD` | 8.95 | body text ✅ |
| Charcoal on Honey `#E5BA69` | 5.81 | primary button ✅ |
| Canvas on Forest deep `#3F6248` | 5.84 | name tags, secondary ✅ |
| Canvas on Clay deep `#8E5240` | 5.19 | ribbon tags ✅ |
| Canvas on Forest green `#527A5B` | 4.15 | ❌ for small text → use Forest deep |
| Canvas on Clay orange `#CB866C` | 2.49 | ❌ text; decoration only |
Non-text UI boundaries ≥ 3:1. State is never color-only (icons/shapes/labels). Screen-reader labels exist for every tappable
(alt text names stage, as RegionScene already does). Reduced motion honoured everywhere (§8).

---

## 10. AI-assisted production pipeline (D-170 accepted with owner scope; D-201)
Owner decision: final art is AI-generated, then split into layers and cleaned up by the agent in free tools. Approved generator:
Grok Bot's built-in image generator (free). The finishing pass below is mandatory and logged in the ledger. Note: round-2 FEASIBILITY §8 recorded that the generator outputs flattened ~1280×720 images, does not
hold a character exactly, and cannot repeat a scene pixel-exactly — the steps below exist to correct exactly those limits.

### 10.1 Steps
1. **Guide** — Blender grey-box → `camera_guide.png` + `depth.png` at master size (in-repo, human-made).
2. **Generate** (approved tool only) — per *layer class*, not whole scenes where possible: plate passes (sky, ground per stage),
   isolated objects on flat canvas `#F5EBDD` (bridge kit, trees, props), Mira parts reference only. Reference inputs limited to
   in-repo/CC0/owner-provided images or **round-2 checkpoint images** recorded in the ledger (D-170 owner scope) — i.e. the guide, the round-2 Mira sheet and the locked Mira sheet, and prior
   approved finished layers. Record seed, size, refs.
3. **Upscale** to master (free: Krita/GIMP scaling or an approved free upscaler; record it).
4. **Register** — place on master canvas over `camera_guide.png`; warp/transform to the guide; reject if static drift > 4 px.
5. **Split** into named layers (§7.2) in Krita: cut, rebuild occluded areas behind parts (paint the hidden plank ends, the
   arm under the satchel), clean alpha edges (no halos; 1 px edge choke).
6. **Finish pass (required, agent)** — repaint contour with locked `A_ink_taper` per weight chart; palette remap to `.gpl` with stage
   ramp; remove generator artefacts (text-like marks, extra fingers, mismatched details vs model sheet); paint grain pass.
   Log the operations. Rule: every contour re-inked and every fill re-coloured from the palette.
7. **Export** `.psd` from `.kra`; run `tools`-side checks (palette distance, size, naming, ledger) — scripts to write in Phase D.
8. **Import** into Unity (PSD Importer, atlas assignment), key in catalog, screenshot at every stage key.
9. **Review gate** — originality check by eye vs known works; self-review of the first batch of each kind against this guide and §12 (D-170 owner scope); Michael may ask to see any batch.

### 10.2 Prompt rules
Describe content, materials, medium (gouache, watercolour, ink), camera, palette hexes, stage. **Never** names of artists,
studios, games (including the bible's reference titles), franchises or characters. Always negative: text, letters, logos,
photo, 3D render. Prompts are stored verbatim with an id (`TH-ENV-S0-01`, `MIRA-PART-03`, ...).

### 10.3 Provenance ledger row (per asset) — `provenance_ledger_template.csv`
Columns: `asset_id, asset_key, file_path, kind, stage, tool, tool_version, terms_version_reviewed, date_utc8, prompt_id,
prompt_text, negative_prompt, seed, refs_in(path;license;sha256), raw_output_sha256, raw_output_offrepo_path,
edits(tool:operation;...), edit_source_file, human_edit_hours, author, reviewer, final_sha256, license, status`.
Superset of D-170 rule 4. Raw outputs stay off-repo; hashes link them. A later CI check (D-170 rule 6) fails on missing rows.

### 10.4 Fallback (if the policy is ever narrowed)
If D-170 is later narrowed to concept/reference only, steps 1 and 4–9 stay and step 2 becomes painting in Krita over the guide;
already-shipped AI-derived assets are found and replaced through the ledger (D-170 rule 7).

### 10.5 Licensing
Allowed categories are those in `docs/ASSET_LICENSES.md`: in-repo-created, CC0, AI-derived with a ledger row, approved OFL fonts.
Audio CC0 only. **Round-1 checkpoint images (D-168) are never usable**, not even as references. **Round-2 images may be used as
generation references** (their prompts named no artists/studios/games); each use is recorded in `refs_in` of the ledger row.
Round-2 images do not ship as-is: every shipped asset goes through §10.1.

### 10.6 Reusable asset-production template (per asset)
```
asset_key: proj.rebuild-trail-bridges@build-1of3
parts: [bridge__post_01..04 B2-4, bridge__plank_01..04 B2-4, debris__* B0-1]
master_file: trailhead_bridge.kra   group: bridge   stage tags: B0..B4
camera_guide: guides/trailhead_camera_guide.png (sha)
palette ramp: S2 (Recovery)   line weights: §3 chart   pivots: §7.4
prompt_id(s): ...   ledger rows: ...
QA: §12 checklist ticked by <author>, reviewed by <reviewer>
```

---

## 11. Old Trailhead + bridge — Phase D asset list (stage 0–4)
Full row-level list with keys, stage tags and atlas in `trailhead_asset_list.csv`. Summary:

| Group | Assets | Stages |
|---|---|---|
| Static (Sall) | sky plate S0 + S4 (crossfade 1–3), far hills, riverbed silhouette, near/far bank ground base | all |
| Ground overlays | dry patches S0, regrowth masks S1/S2/S4, worn path S4, path-edge stones S1+ | per stage |
| Water | murky S0–1, clear S2+, ripple strip (speed per stage), shore reeds a–c S2+, lilypads S3+ | per stage |
| Trees (4 types) | trunk ×4 shared; crown bare/budding/full ×4 | S0/S1/S2+ |
| Bridge kit | posts ×4 (broken + new), plank strips ×8, rail sections ×4, rope rails, debris ×5, garlands ×2 | B0..B4 |
| Signpost | leaning broken S0, upright + new arrow boards S1+ | |
| Log/bench | fallen log S0–2, bench S3+ | |
| Small veg | flowers a–f (pink/honey), grass tufts ×6, shrubs ×3 | S1+ placement list in data |
| Fauna/FX | birds 4-frame, butterfly 2-frame, leaves, sparkles, glows ×3 | S1+ |
| Mira | ~24 parts, face swaps (4 eyes, 3 brows, 6 mouths), 5 clips, portrait renders ×5 | — |
| UI | 9-slice panel, dialogue frame, name ribbon, buttons (primary/secondary/pressed), chips, project card frame, ~12 icons, highlight ring, `fallback.missing` card | — |

Bridge build tag ↔ visible state: **B0 `@ruined`** collapsed span + debris · **B1 `@build-0of3`** debris cleared, new posts staked
· **B2 `@build-1of3`** posts + near half span · **B3 `@build-2of3`** full span, no rails · **B4 `@done`** rails + rope. Garlands
appear only when the bridge is `@done` **and** `lm.trailhead` is Restored or Flourishing (both conditions). Landmark stage (`lm.trailhead@...`) drives surroundings independently.

---

## 12. Consistency QA checklist (tick per asset batch)
- [ ] Key matches repo key exactly; resolver returns `Exact` (not fallback) in editor.
- [ ] On master canvas; `_static` difference vs stage 0 = 0 px; guide overlay matches (≤ 4 px).
- [ ] Palette: ≥ 95 % of pixels within ΔE 6 of `.gpl` swatches/stage ramp; accent share within §1.2 budget.
- [ ] Grayscale test: stage identifiable from structure alone; adjacent stages differ structurally.
- [ ] Line weights per chart at 1× (zoom 100 % on a 6" phone frame); no pure black, no uniform vector stroke.
- [ ] Light from upper-left; painted shadows consistent.
- [ ] Mira: every LOCKED feature in §6.1 present and on the correct side (hat leaf left, braid left, satchel right hip, staff right hand).
- [ ] Silhouette at 64 px reads; tappables not hidden > 30 % by foreground.
- [ ] Layer names/pivots/sorting per §7; no `_` layers exported; alpha edges clean.
- [ ] Atlas & GPU budget within §7.6; batches noted.
- [ ] UI: targets ≥ 48dp, contrast table passes, reduced-motion variant exists.
- [ ] Ledger row complete, hashes match, prompt free of names; finish pass recorded.
- [ ] Captures labelled: concept / editor capture / device-tested (bible §47).

## 13. What Phase D must produce against the gates (§45–48)
- §46: Unity scene with real illustrated layered trailhead, Mira present + tappable, short context dialogue, bridge project chosen
  and advanced through the real progression (or isolated dev fixture), visible B0→B4 and S0→S4 changes, UI in this skin, world
  dominant (UI ≤ 34 % of screen when dialogue open), save/restore intact (no schema change).
- §47: captures for every bridge key and landmark stage key, dialogue, project selection, return-after-walking replay, Home,
  Region; a short recording; labels per checklist.
- §48: review questions answered in the Phase D report; any "no" blocks Phase E.
