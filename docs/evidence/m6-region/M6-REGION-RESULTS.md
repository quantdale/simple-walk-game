# M6 Region restoration visuals (2026-10-16)

Label: **Unity-editor-tested** (batchmode `-nographics`). On-device rendering and visual quality: **UNVERIFIED** (KI-002; no GPU on the box, so pixels are never rasterized).

| Check | Result |
|---|---|
| EditMode | 35/35 (8 new `RegionSceneTests`) |
| PlayMode | 2/2 (new `ValleyPlayModeTests`: picture in a live `UIDocument` panel across frames, shipped theme, no unexpected logs, shape budget) |
| Android arm64 IL2CPP debug build | Succeeded (30,999,966 bytes) |

Design: an engine-free `RegionScene` is built only from `RegionReadModel`. `ValleyElement` draws it with Painter2D vector shapes:
- Landmarks are sized and shaped by stage (base height and number of trees) and coloured by stage, so state is never shown by colour alone.
- Hills blend from dry to green with completed restorations.
- The picture has an alt text, and the Region screen keeps its full text lines.

Performance and assets: no textures and no animation; at most 76 filled shapes per repaint (4 backdrop + 12 landmarks × 6). Repaints happen only on layout or scene change. Every asset is created in-repo (`docs/ASSET_LICENSES.md`).
