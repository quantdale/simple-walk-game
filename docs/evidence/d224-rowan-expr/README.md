# D-224 evidence (Unity editor, software GL — NOT a device)

## Delivered
1. Rowan dialogue expression plates: `portrait_rowan_{neutral,wistful,glad,proud,curious}` (face-ellipse ECC + rim choke).
2. Live SpriteSkin bone weights authored on `Rig/rowan_rig.psb` (hips/spine/head; spine+head neck blend). Runtime cut-out idle kept for silhouette (weighted prefab asserted in EditMode).
3. ArtFringeTests covers all Rowan expression portraits + prior character/find layers.

## Suites (before merge)
| Suite | Result |
|-------|--------|
| EditMode (full) | **411 / 411** |
| PlayMode (Xvfb software GL, full) | **9 / 9** |
| .NET | **1871 / 1871** (162+252+1457) |

## D-223 verify (items 1–2)
Re-inspected `shell-region-view` + `canopy-s4-flourishing` from main/`#225`: glyph pins + whole-word plates; orchids small gouache understory. Also refreshed here.
