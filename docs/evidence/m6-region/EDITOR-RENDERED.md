# Region screen: editor-rendered capture (2026-10-16)

`valley-editor-rendered.png` (540×1170) is **editor-rendered**, **not device-rendered**.
- **Renderer:** Unity 6000.3.26f1 in batchmode with `-force-glcore` under Xvfb, using Mesa **llvmpipe** software OpenGL 4.5 (LLVM 19.1.7).
- **How:** the UI Toolkit panel rendered into a RenderTexture, which was read back and encoded with `tools/unity/rgba-to-png.py`.
- **Scene:** a new game with open projects queued and 6 synthetic days of walking. One restoration is finished: Old Trailhead stands taller with a tree; the others are still ruined, and the hills are mostly dry. The valley drew 17 shapes, within the 88-shape budget.
- **Reproduce:** `tools/unity/capture-valley.sh [out.png]` (`WG_CAPTURE_SCREEN=Region|ReturnSummary|Home`). The capture test (`ValleyCaptureTests`) is ignored in normal `-nographics` runs.

What it shows: layout, palette, the valley picture and its text alongside each other, rendered by a real GPU-API path. What it does not show: phone GPU, density/DPI, touch, or the device font. Those remain UNVERIFIED (KI-002).

## M6b readability recapture (2026-10-16)

`valley-editor-rendered-m6b.png`: same scene, same capture path (Xvfb + glcore + llvmpipe), **editor-rendered, not device**. Ruined landmarks no longer read as identical flat bars. Each Region 1 landmark has its own two-shape silhouette:
- trailhead: signpost and board
- intake: weir and water wheel
- canopy: trunk and crown
- settlement: house and pitched roof
- wetland: pool and reeds
- observatory: tower and dome

A ruined landmark is drawn squat, with its top knocked off-centre and darkened, and rubble beside it. Restored stages stand at full height with a lit window. A sun is added; budget 86 shapes (`RegionScene.MaxShapes`). The earlier `valley-editor-rendered.png` is kept for comparison.
