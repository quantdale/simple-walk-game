# Region screen: editor-rendered capture (2026-10-16)

`valley-editor-rendered.png` (540×1170) is **editor-rendered**, **not device-rendered**.
- **Renderer:** Unity 6000.3.26f1 in batchmode with `-force-glcore` under Xvfb, using Mesa **llvmpipe** software OpenGL 4.5 (LLVM 19.1.7).
- **How:** the UI Toolkit panel rendered into a RenderTexture, which was read back and encoded with `tools/unity/rgba-to-png.py`.
- **Scene:** a new game with open projects queued and 6 synthetic days of walking. One restoration is finished: Old Trailhead stands taller with a tree; the others are still ruined, and the hills are mostly dry. The valley drew 17 shapes, within the 88-shape budget.
- **Reproduce:** `tools/unity/capture-valley.sh [out.png]` (`WG_CAPTURE_SCREEN=Region|ReturnSummary|Home`). The capture test (`ValleyCaptureTests`) is ignored in normal `-nographics` runs.

What it shows: layout, palette, the valley picture and its text alongside each other, rendered by a real GPU-API path. What it does not show: phone GPU, density/DPI, touch, or the device font. Those remain UNVERIFIED (KI-002).
