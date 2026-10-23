# Asset licence register

Every non-code asset that ships in the game is listed here with its origin and licence.
Rule: only these categories may be added without a separate decision:
1. assets **created in this repository**;
2. **CC0** (public domain) assets;
3. **AI-derived art with a provenance ledger row** (D-170 as accepted 2026-10-24, D-201): every such asset has exactly one row in the provenance ledger (`docs/direction/provenance_ledger_template.csv` format) recording tool, date, prompt summary, reference inputs and their licence, raw-output SHA-256, edits made and final SHA-256. Prompts must not name artists, studios, games or franchises. Round 1 checkpoint images (D-168) are never usable;
4. fonts under the **SIL Open Font License** that a DECISIONS entry approves (Fraunces and Nunito are approved; list them here when added).

Anything else needs a DECISIONS entry first.

| Asset | Where | Origin | Licence | Notes |
|---|---|---|---|---|
| **Placeholder (D-164/D-171): to be replaced, never production art.** Valley picture (sky, hills, river, landmark blocks, trees, markers) | `src/WalkGame.Unity/Assets/Shell/ValleyElement.cs`, `RegionScene.cs` | Created in-repo (2026-10-16, M6 Region visuals). Procedural UI Toolkit vector shapes; no image files. | Same as the repository | Palette constants are in `RegionScene.StageColor` / `HillColor`. Budget ≤ `RegionScene.MaxShapes` (86 = 5 backdrop + 8 landmarks × 9 + 9 build-site; incl. one ring per restoration moment). M6b (2026-10-16): per-kind landmark silhouettes (gate, waterworks, house, grove, wetland, observatory) and a sun, also created in-repo filled shapes per repaint, no textures, no animation. |
| **Old Trailhead slice art (D-204)**: 2 ground plates, 5 bridge/prop structure layers, Mira in-world sprite + 5 expression portrait busts (13 PNGs; D-206) | `src/WalkGame.Unity/Assets/Resources/Art/Trailhead/` | AI-derived (Grok Bot image generator, 2026-10-24) then split/cleaned/palette-pulled by the agent (`tools/art/trailhead_finish_pass.py`) | AI-derived with ledger row (D-170, D-201) | One row each in `docs/direction/art/provenance_ledger.csv`. Refs: round-2 checkpoint images only. Editor texture estimate 8.7 MB (≤ 12 MB budget). |
| Shell theme | `src/WalkGame.Unity/Assets/Resources/ShellTheme.tss` | Created in-repo (M5-U1) | Same as the repository | Imports Unity's default runtime theme (part of the Unity editor, not redistributed separately). |

No third-party image, audio, font or model files are in the repository as of 2026-10-16. Since 2026-10-24 the only image files are the D-204 AI-derived trailhead layers listed above.
