# M7-B results (2026-10-16): Health Connect change-tracking deletions (D-069)

Label: **Unity-editor-tested** build. Device behaviour: **UNVERIFIED** (KI-002).

| Check | Result |
|---|---|
| `dotnet test` | Domain 148, Application 325, Infrastructure 91 (41 Health Connect, 9 new): all pass |
| EditMode | 27/27 |
| Android arm64 IL2CPP debug build | Succeeded (30,988,590 bytes); `HealthConnectBridge$Index`, `$Api34`, `$BridgeError` in dex (`apk-inspection.txt`) |

New tests: deletion line mapping (same identity and original interval), revision always above the credited one (including a backwards clock), malformed deletion lines fail the fetch, window filtering and ordering, end-to-end reversal exactly once across re-delivery and restart, deleting a never-credited record is a no-op, and a stale re-delivery after deletion is not re-credited.
