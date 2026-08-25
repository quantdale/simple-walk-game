# Decision Log

This file records product and architectural decisions that future agents/maintainers should treat as current defaults until superseded by a later decision.

Use the format:

- **Status:** Proposed / Accepted / Superseded / Rejected
- **Decision:** what is being chosen
- **Rationale:** why
- **Consequences:** constraints created by the decision

---

## D-001 — Real-world activity is the primary gameplay

**Status:** Accepted

**Decision:** The core game is driven by real-world movement/activity rather than foreground tapping, combat, or conventional session-based play.

**Rationale:** The target player may not be willing to dedicate substantial active gaming time. The game should attach meaning to activity the player is already doing.

**Consequences:**

- app-closed progression is first-class;
- activity ingestion is a core subsystem;
- foreground mechanics may not become mandatory maintenance;
- product success cannot be measured only by session length.

---

## D-002 — Low required screen time is a hard product constraint

**Status:** Accepted

**Decision:** Core progression must remain coherent with seconds-long check-ins and optional longer sessions.

**Rationale:** The product is specifically designed for users who may be busy or not highly active gamers.

**Consequences:**

- automation is required;
- no frequent collection loops;
- project queues should auto-advance where safe;
- multi-day absence must be a supported normal state;
- every feature should be reviewed against an attention budget.

---

## D-003 — Persistent restoration is the main progression fantasy

**Status:** Accepted

**Decision:** Real-world activity restores a damaged world over time.

**Rationale:** Restoration creates a durable and visually legible representation of cumulative physical activity.

**Consequences:**

- world state is persistent;
- major project completion should create visible environmental change;
- Region 1 must have strong degraded-to-restored contrast;
- progress cannot be represented only through account XP/numbers.

---

## D-004 — Inactivity does not destroy major progress

**Status:** Accepted

**Decision:** Missing days or taking breaks does not destroy restored assets, completed projects, or major earned progress.

**Rationale:** Punishing absence conflicts directly with the low-pressure ambient product thesis and harms re-entry.

**Consequences:**

- no destructive world decay;
- no mandatory daily maintenance;
- momentum bonuses, if introduced, must be non-destructive;
- return UX should welcome rather than shame.

---

## D-005 — Offline-first MVP

**Status:** Accepted

**Decision:** Core progression and local world state do not require a backend or mandatory account.

**Rationale:** Offline reliability reduces complexity, protects the ambient use case, and avoids making network availability part of the activity trust chain.

**Consequences:**

- canonical state is stored locally;
- future sync must be additive and conflict-aware;
- no server dependency for project progression;
- cloud features require a later architecture/privacy decision.

---

## D-006 — No mandatory precise GPS for the core loop

**Status:** Accepted

**Decision:** Core activity progression uses health/activity data and does not require continuous precise route tracking.

**Rationale:** Continuous GPS increases battery/privacy cost and is unnecessary for the primary restoration loop.

**Consequences:**

- step/activity aggregates are preferred inputs;
- route-based features, if ever added, are optional;
- permissions should remain minimal.

---

## D-007 — Exactly-once reward crediting is a non-negotiable invariant

**Status:** Accepted

**Decision:** The same logical activity record/transaction may not generate progression more than once.

**Rationale:** Duplicate credit immediately undermines trust, balance, and save integrity.

**Consequences:**

- durable dedup identities;
- reward transaction IDs;
- idempotent application;
- replay/failure tests;
- activity checkpoint sequencing cannot bypass durable reward state.

---

## D-008 — Canonical state lives outside Unity presentation

**Status:** Accepted

**Decision:** The authoritative game rules/state live in a pure C# domain layer with no Unity engine dependency.

**Rationale:** Deterministic tests, offline simulation, migration, and exactly-once accounting should not depend on scene objects or frame lifecycle.

**Consequences:**

- presentation binds to canonical state;
- scene reload cannot alter progression;
- domain tests run outside Unity where practical;
- platform bridges cannot directly mutate world state.

---

## D-009 — Unity 6 LTS is the baseline presentation/runtime direction

**Status:** Accepted *(ratified by implementation: domain targets netstandard2.1 with zero engine references, ready for Unity 6 LTS hosting)*

**Decision:** Use Unity 6 LTS + C# for mobile runtime and optional 3D Visit World presentation, while keeping the domain independent.

**Rationale:** The product benefits from a richer optional 3D restored world, and the previous design direction is compatible with a C# deterministic domain.

**Consequences:**

- exact Unity/package versions must be locked during M1/M3 implementation;
- mobile build/runtime validation is required;
- a future superseding decision may choose another presentation technology without rewriting domain rules if boundaries are preserved.

---

## D-010 — Visit World is optional depth

**Status:** Accepted

**Decision:** Real-time 3D exploration is optional and cannot be required for baseline progression.

**Rationale:** The product must work for players who do not want long sessions or heavy foreground interaction.

**Consequences:**

- lightweight Region/Projects UI supports all core decisions;
- 3D content may unlock optional flavor/discoveries but not gate core restoration;
- 3D scene loads on demand;
- 3D load failure cannot break the game.

---

## D-011 — One complete region before expansion

**Status:** Accepted

**Decision:** Region 1 must be vertically complete and release-qualified before Region 2 production becomes a default priority.

**Rationale:** Multiple shallow regions would hide weaknesses in the core product loop and multiply content/technical debt.

**Consequences:**

- Region 1 gets complete project, discovery, expedition, ecology, settlement, and visual arcs;
- post-MVP expansion happens only after a decision gate;
- Region 2 is explicitly excluded from MVP execution campaigns.

---

## D-012 — Vitality is the primary activity-derived resource

**Status:** Accepted *(implemented: `ResourceType.Vitality` is canonical; 100 steps → 1 Vitality via conversion rule v1)*

**Decision:** Use a primary resource tentatively called `Vitality` to represent validated restorative activity.

**Rationale:** One dominant resource keeps the relationship between real movement and world restoration understandable.

**Consequences:**

- conversion rules are deterministic and versioned;
- secondary resources must have distinct roles;
- avoid economy sprawl;
- naming may change without changing the underlying model.

---

## D-013 — Project queue automatically preserves progress continuity

**Status:** Accepted

**Decision:** The player can queue restoration work and eligible progress can automatically continue into subsequent queued work.

**Rationale:** Requiring the app to be opened exactly when a project completes violates the ambient design.

**Consequences:**

- project completion mid-batch/offline must be deterministic;
- remaining progress rolls according to documented rules;
- queue-empty fallback must avoid catastrophic wasted activity.

---

## D-014 — Activity conversion rules are versioned

**Status:** Accepted

**Decision:** Store the conversion-rule version associated with credited activity/reward transactions.

**Rationale:** Balance changes must not cause historical activity to be silently revalued or replayed.

**Consequences:**

- old transactions remain auditable;
- conversion changes require migration/reconciliation decisions;
- tests include multiple rule versions when such changes ship.

---

## D-015 — Platform activity APIs are adapters, not domain dependencies

**Status:** Accepted

**Decision:** Android/iOS activity sources are isolated behind platform-neutral application ports.

**Rationale:** Platform behavior changes independently and should not infect game rules.

**Consequences:**

- fixtures can test the full downstream pipeline;
- Android/iOS can have different capability details;
- exact provider API versions are implementation-time decisions;
- platform-specific limitations are documented honestly.

---

## D-016 — Raw health data retention is minimized

**Status:** Accepted

**Decision:** Persist only the normalized/provenance information needed for deduplication, reconciliation, and game progress.

**Rationale:** The game does not need broad health history and should minimize privacy risk.

**Consequences:**

- no wholesale raw platform payload storage;
- diagnostics are redacted/bounded;
- future backend cannot receive raw health data without a new explicit decision.

---

## D-017 — Deterministic RNG for canonical outcomes

**Status:** Accepted

**Decision:** Expedition/discovery outcomes that use randomness derive from persisted deterministic seeds/state.

**Rationale:** Save/reload/retry must not reroll canonical results.

**Consequences:**

- RNG is injected;
- rendering randomness remains non-authoritative;
- algorithm changes may require version consideration.

---

## D-018 — Evidence states replace vague “done” labels

**Status:** Accepted

**Decision:** Features are classified as SPECIFIED, IMPLEMENTED, AUTOMATED VERIFIED, RUNTIME VERIFIED, DEVICE VERIFIED, or RELEASE QUALIFIED.

**Rationale:** Platform/mobile projects easily overstate readiness when code exists but runtime/device behavior remains unproven.

**Consequences:**

- release docs must state evidence precisely;
- editor success is not device verification;
- documentation drift becomes a quality issue.

---

## D-019 — Critical/High defects block release

**Status:** Accepted

**Decision:** No known Critical or High severity defect remains open at release qualification.

**Rationale:** The highest-risk failures affect progression trust, data integrity, platform usability, accessibility, or severe performance.

**Consequences:**

- scope cuts are preferable to shipping a known major integrity defect;
- defect severity influences roadmap priority.

---

## D-020 — No MVP backend/social/multiplayer complexity

**Status:** Accepted

**Decision:** The MVP does not include multiplayer, guilds, PvP, social feed, competitive leaderboards, server-authoritative progression, or live-service infrastructure.

**Rationale:** These systems do not prove the ambient-restoration thesis and would materially increase scope.

**Consequences:**

- architecture should not be pre-distorted around hypothetical live service needs;
- post-MVP additions require a new decision and product rationale.

---

## D-021 — No ad-dependent or gacha economy in MVP core

**Status:** Accepted

**Decision:** Core progression is not designed around ads, gacha, or premium currency.

**Rationale:** Monetization pressure would distort attention budgets and progression before the product loop is validated.

**Consequences:**

- MVP balance assumes earned activity progression;
- monetization, if later desired, receives a separate ethical/product/economy review.

---

## D-022 — Performance budgets are release gates

**Status:** Accepted

**Decision:** Launch, responsiveness, frame pacing, memory, battery/thermal behavior, and reconciliation cost are measured against documented budgets.

**Rationale:** A passive/ambient product that feels heavy on a phone contradicts its value proposition.

**Consequences:**

- mobile device evidence required;
- optional 3D has quality tiers;
- lightweight mode avoids retaining unnecessary world cost.

---

## D-023 — Documentation-first bootstrap

**Status:** Accepted

**Decision:** The new repository begins with comprehensive product/engineering contracts before implementation.

**Rationale:** The repository started effectively empty, allowing architectural and scope decisions to be made explicitly rather than reconstructed from accidental code structure.

**Consequences:**

- M0 documentation is the initial source of truth;
- implementation must update docs as evidence replaces proposals;
- proposed decisions (such as exact runtime/tooling) may be superseded when implementation evidence warrants it.

---

# Unresolved decisions

The following should be resolved at the relevant milestone rather than guessed prematurely:

1. Exact Unity 6 LTS editor/runtime version.
2. Exact local persistence implementation (snapshot+journal vs SQLite or another adapter).
3. Exact Android Health Connect SDK/API integration strategy and minimum supported OS/device policy.
4. Exact iOS HealthKit integration strategy and minimum supported iOS version.
5. Final Region 1 fiction/name/art direction.
6. Final Vitality conversion curve and safety bounds.
7. Whether Materials and Knowledge both survive MVP balance testing.
8. Notification categories and defaults after UX/device validation.
9. Asset delivery strategy for Visit World.
10. Whether cloud backup/sync enters post-MVP scope.

Resolve these through new decision entries with rationale and consequences; do not silently bake them into implementation.

---

## D-024 — M1 implementation stack: standalone .NET, netstandard2.1 + C# 9 domain

**Status:** Accepted

**Decision:** The deterministic core is implemented as a plain .NET solution (`SimpleWalkGame.sln`) independent of Unity: `src/WalkGame.Domain` and `src/WalkGame.Application` target `netstandard2.1` with `<LangVersion>9.0</LangVersion>`; `src/WalkGame.Infrastructure` targets the same profile with a `System.Text.Json` 8.x package reference; test projects and the headless CLI target `net9.0`.

**Rationale:** ROADMAP M1 requires a headless, Unity-free domain that a clean clone can build and test with the SDK alone. netstandard2.1 + C# 9 keeps the Domain/Application sources consumable by Unity 6 either as compiled assemblies or source.

**Consequences:**
- no C# 10+ syntax in Domain/Application (no record structs, no file-scoped namespaces there);
- Infrastructure may be replaced/adapted for Unity if its JSON stack differs;
- tests/CLI are dev-time only and never ship to device.

---

## D-025 — Save envelope: versioned JSON frame with SHA-256 payload integrity

**Status:** Accepted

**Decision:** Save files are `{ schemaVersion, savedAtUtc, payloadSha256Base64, payloadBase64 }`. The payload is opaque base64 UTF-8 JSON of the canonical state graph. Integrity is verified before any migration or deserialization; schema version gates run before migration; migrations transform a cloned payload so failure preserves the original node.

**Rationale:** Opaque framing decouples envelope stability from payload evolution; checksum-first ordering means corrupt data is rejected before it can be misinterpreted; clone-based migration satisfies the "never overwrite the recoverable copy" guardrail.

**Consequences:**
- payload inspection requires base64 decoding (acceptable for tooling);
- every persisted-shape change bumps `SchemaVersions.Current` and adds a registered sequential `ISaveMigration`;
- at schema v1 the migration chain is intentionally empty; the pipeline is proven by dedicated tests.

---

## D-026 — Atomic snapshot store with one-generation backup

**Status:** Accepted

**Decision:** `AtomicFileSaveStore` writes to a temp file with write-through + flush-to-disk, rotates the previous primary into `save.backup.json`, then replaces the primary. Load order is primary → backup; recovery policy lives in the application layer, file semantics in infrastructure.

**Rationale:** Snapshot+backup is the simplest durable pattern that satisfies M1's tested-recovery exit criterion without journal complexity; crash windows leave at most one stale generation behind, which reads ignore.

**Consequences:**
- backup depth is one generation (older history is not retained);
- storage cost is ~2× save size;
- netstandard2.1 lacks atomic-overwrite `File.Move`, so replace is delete+move with the backup guaranteeing recovery across the window.

---

## D-027 — Canonical state serializes directly via STJ contract modifiers

**Status:** Accepted

**Decision:** Domain state classes keep get-only collection properties (invariant-enforcing). The Infrastructure serializer options set `IncludeFields = true` (for `RngState`) and force `JsonObjectCreationHandling.Populate` on collection-typed properties via a `DefaultJsonTypeInfoResolver` modifier. No serializer attributes leak into Domain.

**Rationale:** Default STJ handling *replaces* get-only collections on load, silently dropping all projects/balances/ledger entries — discovered by roundtrip tests during M1. Populate keeps deserialization additive while preserving the domain's encapsulation.

**Consequences:**
- adding new collection-typed canonical state requires verifying the modifier still applies (covered by roundtrip regression tests);
- RngState must remain field-based or gain properties deliberately.

---

## D-028 — Exactly-once crediting keyed by caller-supplied transaction identity

**Status:** Accepted

**Decision:** Reward transactions carry a durable, caller-supplied stable ID (GUID in M1 tooling; platform-fingerprint-derived later in M2). The ledger rejects re-application by ID; balances only move inside `Apply`.

**Rationale:** Replay/crash/retry safety requires identity that survives restarts and cannot be regenerated differently; deriving IDs from activity-source fingerprints arrives with the M2 trust pipeline.

**Consequences:**
- callers who mint fresh GUIDs per retry defeat dedup — adapters must derive IDs deterministically from source records;
- ledger growth is unbounded (years of daily transactions remain small); bounded retention would weaken exactly-once guarantees and needs explicit design first.

---

## D-029 — Correction/deletion baseline: conservative clawback with net-applied accounting

**Status:** Accepted

**Decision:** Higher-revision redeliveries of an already-trusted logical record are corrections. Positive deltas credit the difference; negative deltas (and deletions) claw back only what the unspent Vitality balance allows, durably counting any unclawed remainder in ProcessedRecordLedgerState.UnappliedReversalVitality. Processed-record rows store **net applied** vitality (what the ledger actually saw), never the theoretical target, so dedup state can never outrun reward state. Deletions for unknown records and stale revisions are counted diagnostics, not state changes. Corrections require durable source record IDs; fingerprint-identity sources treat changed content as a new logical record by construction.

**Rationale:** ACTIVITY_PIPELINE §11 prioritizes state integrity and player trust over punitive clawbacks; completed world content must never be destroyed because a source corrected or deleted a small amount. Net-applied rows keep the validator invariant (processedTotal <= ledgerTotal) true without special cases.

**Consequences:**
- reversal remainders are revenue-safe but must be visible in diagnostics forever;
- a later positive correction after a clamped reversal credits against net-applied value, converging toward earned value;
- correction transaction IDs derive from identity + revision + applied amount (tx-corr1 namespace) and cannot collide with first-credit IDs;
- sources without durable record IDs cannot express corrections (documented adapter requirement for Health Connect/HealthKit work).

---

## D-030 — Bounded reconciliation horizon

**Status:** Accepted

**Decision:** Records ending more than 14 days before "now" (injected clock) are rejected with OutsideHorizon; records ending more than 10 minutes in the future are rejected as FutureTimestamp. The ingestion checkpoint watermark is clamped to now.

**Rationale:** ACTIVITY_PIPELINE §10 requires bounded historical reconciliation: a stale or hostile source dump must not be able to re-open arbitrarily old history, and future-dated records must not pre-mint rewards.

**Consequences:**
- genuinely older backfills need an explicit, separately-designed import path;
- the horizon constants live in domain policy and are covered by validation tests.

---

## D-031 — Fail-closed repository identity and single-writer isolation for agents

**Status:** Accepted

**Decision:** Every autonomous coding session must pass a fail-closed repository identity preflight and hold a single-writer lease before writing. Concretely: `.repo-identity.json` + `scripts/assert-repo-identity.{sh,ps1}` prove origin/CI identity equals `quantdale/simple-walk-game` (exit 86 otherwise); `scripts/writer-lease.{sh,ps1}` grants one atomic per-worktree lease (exit 87 when busy; override requires BOTH `--force` and an explicit operator env acknowledgement); `.githooks/pre-push` refuses any push whose remote tip is not contained in the pushed history (exit 88); CI re-runs the identity guard plus a proof suite over all guard behaviors; root `AGENTS.md` binds every harness and all `/goal` adapters inherit the preflight. Concurrent sessions use dedicated worktrees (`scripts/new-agent-worktree.sh`: one writer = one worktree = one branch).

**Rationale:** Two concurrent executor sessions once wrote this same work tree and interleaved/deleted each other's lineage (commits `b12f52c`, `67368e3`). Identity alone cannot prevent same-tree collisions, hooks alone can be bypassed, and nothing previously stopped an agent intended for the sibling repository `quantdale/walk-game` from operating here. The mechanisms cover each other's gaps: manifest identity (wrong repo), lease (same-tree writers), pre-push (remote races), CI (local bypass), AGENTS.md + adapters (harness-independent propagation).

**Consequences:**
- agents acquire/release the lease around write phases; second writers stop instead of racing;
- stale locks require explicit human recovery — never silent theft;
- force-push / hard-reset / clean remain forbidden conflict shortcuts without operator authorization;
- the guard tooling is under change-control like any invariant: modifying it to make it "stop complaining" is prohibited.
