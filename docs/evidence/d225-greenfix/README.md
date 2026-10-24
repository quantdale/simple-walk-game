# D-225 evidence (Unity editor, software GL — NOT a device)

Root cause: an enclosed chroma-key pocket (arm/satchel gap) that the border flood-fill never reached. It shipped opaque, so the semi-alpha fringe gate never saw it. Fixed with `tools/art/key_pocket_clean.py` and gated by `ArtFringeTests` (zero-tolerance sweep of `Resources/Art` + cut-out idle composite).

| Check | Result |
|---|---|
| `hip-crop-before-after.png` (left D-224, right D-225) | green wedge gone, background shows through, no halo |
| Full-frame key-green px, canopy-s4-flourishing | 94 → 0 |
| canopy-s0-rowan / s2 / orchids-glow | 88 / 6 / 6 → 0 |
| EditMode (full) | **413 / 413** |
| PlayMode Xvfb (full) | **9 / 9** |
| PlayMode nographics | 4 passed, 5 skipped |
| .NET | **1871 / 1871** (162 + 252 + 1457) |
