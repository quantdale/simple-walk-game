# Screen tour: every shell screen, editor-rendered (2026-10-16, grok2121)

**Label: editor-rendered** (Unity 6000.3.26f1 PlayMode, Mesa llvmpipe software GL under Xvfb, 540×1170 at
scale 1.5). **Not device screenshots**; device rendering is UNVERIFIED (KI-002).

Produced by `tools/unity/capture-screen-tour.sh` (one `capture-valley.sh` run per screen) from branch
`agent/simple-walk-game/r1mvp-grok2121` based on main `33a72cf`. `TOUR_RUN.txt` is the run summary:
14/14 captures, each PlayMode result passed=1 failed=0, renderer llvmpipe. Each capture asserts it reached
the named screen before rendering.

| Screen (`ShellScreen`) | File | State used |
|---|---|---|
| Onboarding | `onboarding-editor-rendered.png` | fresh device, no save |
| Guide | `guide-editor-rendered.png` | first guide step after start |
| Home | `home-editor-rendered.png` | mid-game (queue + 6 synthetic days) |
| ReturnSummary | `returnsummary-editor-rendered.png` | same |
| Projects | `projects-editor-rendered.png` | same |
| Region | `region-editor-rendered.png` | same |
| Story | `story-editor-rendered.png` | same |
| Discoveries | `discoveries-editor-rendered.png` | completed region (`CompletedRegionFixture`) |
| Progress | `progress-editor-rendered.png` | mid-game |
| Settings | `settings-editor-rendered.png` | mid-game |
| ActivityStatus | `activitystatus-editor-rendered.png` | mid-game, development source |
| DeleteConfirm | `deleteconfirm-editor-rendered.png` | `delete:request` |
| BootError | `booterror-editor-rendered.png` | both save slots unreadable |
| (ReturnSummary) Celebration | `celebration-editor-rendered.png` | completed region |

Reproduce (about 1 min per screen; run detached):
`nohup tools/unity/capture-screen-tour.sh /tmp/tour > /tmp/tour.log 2>&1 &`

## Findings from the tour

1. BootError shows a raw identifier line, "Status: SaveUnreadable" (UX_DESIGN §5: no raw identifiers).
2. Projects buttons double the verb: "Restore Rebuild the trail bridges", "Restore Restore the river intake".

Both are fixed in a follow-up copy PR (D-109) and re-captured there.
