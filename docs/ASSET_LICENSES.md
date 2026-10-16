# Asset licence register

Every non-code asset that ships in the game is listed here with its origin and licence.
Rule: only assets **created in this repository** or **CC0** (public domain) may be added without a
separate decision. Anything else needs a DECISIONS entry first.

| Asset | Where | Origin | Licence | Notes |
|---|---|---|---|---|
| Valley picture (sky, hills, river, landmark blocks, trees, markers) | `src/WalkGame.Unity/Assets/Shell/ValleyElement.cs`, `RegionScene.cs` | Created in-repo (2026-10-16, M6 Region visuals). Procedural UI Toolkit vector shapes; no image files. | Same as the repository | Palette constants are in `RegionScene.StageColor` / `HillColor`. Budget ≤ `RegionScene.MaxShapes` (86 = 5 backdrop + 8 landmarks × 9 + 9 build-site; incl. one ring per restoration moment). M6b (2026-10-16): per-kind landmark silhouettes (gate, waterworks, house, grove, wetland, observatory) and a sun, also created in-repo filled shapes per repaint, no textures, no animation. |
| Shell theme | `src/WalkGame.Unity/Assets/Resources/ShellTheme.tss` | Created in-repo (M5-U1) | Same as the repository | Imports Unity's default runtime theme (part of the Unity editor, not redistributed separately). |

No third-party image, audio, font or model files are in the repository as of 2026-10-16.
