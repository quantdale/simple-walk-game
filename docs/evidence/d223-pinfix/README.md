# D-223 evidence (Unity editor, software GL — NOT a device)

## Defect fixes
1. Region map: glyph-only pins + separate parchment label plates (`WhiteSpace.Pre`, wrap at spaces only).
2. Canopy orchids: small gouache understory (`OrchidWorldScale` 0.28) + subtle find glow.
3. Neon key fringe choked via `tools/art/green_key_finish.py` (D-214-style); `ArtFringeTests` on Mira/Tavi/Rowan/millstone/orchids.

## Suites (before merge)
| Suite | Result |
|-------|--------|
| EditMode (full) | **411 / 411** passed |
| PlayMode (Xvfb / software GL, full incl. captures) | **9 / 9** passed |
| PlayMode (nographics CI) | 4 passed + 5 capture skips (no GPU) |
| .NET (`dotnet test SimpleWalkGame.sln`) | **1871 / 1871** (Domain 162 + Infra 252 + App 1457) |

Best review captures: `shell-region-view`, `canopy-s4-flourishing`, `canopy-orchids-glow` (also mirrored to `/workspace/phase-d-send/*.jpg`).
