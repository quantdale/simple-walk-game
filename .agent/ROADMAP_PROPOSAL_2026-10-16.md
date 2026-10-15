# Phase-1 Assessment & Prioritized Roadmap Proposal (2026-10-16)

**Status:** PROPOSED — awaiting operator approval. NOT an active campaign. `.agent/EXECUTION_PROMPT.md` (M5-H1, COMPLETED) is unchanged.
**Assessed-From:** `97db0c6d846a23b1023c84c20dfd2b1ceeb790ff` (= origin/main at 2026-10-16)
**Environment:** Grok Bot box, Debian 13 x86_64, 8 vCPU Xeon, 16 GB RAM (~4–6 GB free), 116 GB free disk, /dev/kvm present, no GPU (/dev/dri absent), no Java, .NET SDK 9.0.319 installed user-locally.

## 1. Verified on this box (fresh clone)
| Gate | Result |
|---|---|
| `scripts/assert-repo-identity.sh` | OK quantdale/simple-walk-game |
| `dotnet build SimpleWalkGame.sln` | 0 errors |
| `dotnet test` | **505 passed / 0 failed** (Domain 148, Application 308, Infrastructure 49) |
| Simulation smoke (new/simulate 5d/validate --selftest) | PASS (300 Vitality credited; integrity-selftest PASS) |
| `tests/guards/run-guard-tests.sh` | 25 passed / 0 failed |
Not run here: coverage floor, save-growth, pacing, content-compat, dependency-health scripts (follow-up), hosted CI (KI-001).

## 2. Implemented vs verified
- AUTOMATED-VERIFIED (headless): M1, M2, M3-H, M4-H (Region 1), M5-H1 (UX state contracts, glance D-065), M8-H1; 14/15 IMPLEMENTATION_MASTER_PLAN changes applied.
- IMPLEMENTED, OPEN: `compact-activity-and-reward-ledgers` (F-07, 39 open tasks; save grows linearly ~540 B/day).
- NOT IMPLEMENTED / UNVERIFIED: any Unity project (D-035/KI-002), M5 runtime screens, M6 Visit World, M7 Health Connect/HealthKit, M8 device/lifecycle, M9.
- Doc drift: README/ROADMAP still cite 295 tests; actual 505.

## 3. Unity 6 / Android feasibility on this box
- Unity 6 Linux editor (x86_64, Ubuntu-targeted) can likely run in `-batchmode -nographics` on Debian 13 (unsupported distro; expect lib shims). Disk OK (~15–25 GB editor + Android module + SDK/NDK/OpenJDK).
- **Blockers:** (a) licensing — Personal requires Unity ID activation (manual .ulf / Unity Hub login by the user); Pro/Build Server needs a serial — operator action; (b) RAM — ~4–6 GB free is tight for editor import + Gradle/IL2CPP builds (expect OOM risk; swap or freeing memory advised); (c) no GPU — EditMode tests and Android APK builds fine headless, but PlayMode/visual (M6) and performance evidence are not meaningful here.
- Android: SDK/NDK/JDK installable (Unity-bundled versions preferred). KVM exists, so an x86_64 emulator is possible but no Health Connect on emulator parity and no physical device ⇒ M7 device gates remain UNVERIFIED.
- **Verdict:** FEASIBLE-WITH-CONDITIONS for M5 Unity shell scaffolding, EditMode tests and Android debug APK builds once the user provides a Unity license; NOT feasible for device/perf/M6 visual qualification.

## 4. Prioritized roadmap
1. **P1 — Close F-07 ledger compaction** (only open engineering change; headless; protects mature saves/commit cost).
2. **P1 — Doc/record hygiene**: test counts, master-plan §0, KI-008 lease self-healing (`SWG_SESSION_ID` default) — small.
3. **P1 — Unity unblock (operator)**: license activation + choose exact Unity 6 LTS version (closes D-035 question 1).
4. **P2 — M5-U1 Unity shell**: Unity project consuming Application read models via netstandard2.1 DLLs; Home/Return/Projects/Settings screens; EditMode tests; batchmode Android APK build.
5. **P2 — M7-A Android Health Connect adapter** (behind existing provider port), emulator smoke; device verification by user.
6. **P3 — M6 Visit World slice** (needs GPU machine/device).
7. **P3 — M8 runtime half + M9** (needs KI-001 billing fix + devices). KI-006 encryption decision before M9.

## 5. Proposed first implementation campaign
**C-F07 — compact-activity-and-reward-ledgers** (headless, fully verifiable here): execute the OpenSpec task list §1–§7 (maintained lifetime aggregates, in-memory index, retirement policy, enable retirement + bound, tests incl. exactly-once replay after retirement and dedup-never-outruns-reward on load, save-growth gate converted to a plateau assertion, docs/D-entry). Acceptance: full suite + guard + smoke + `verify-save-growth.sh` showing bounded size at d365/d730; no re-credit on replay of retired records. Then flip README/§0 counts.
Alternative if the user activates Unity first: M5-U1 scaffold (item 4).
