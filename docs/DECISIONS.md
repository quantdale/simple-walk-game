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

**Status:** Accepted *(POLICY kept; implementation inert — corrected by D-057, 2026-09-30)*

**Decision:** Any canonical outcome that uses randomness must derive from persisted
deterministic seeds/state. Rendering-side randomness must never decide a reward.

**Rationale:** Save/reload/retry must not reroll canonical results.

**Consequences:**

- RNG is injected;
- rendering randomness remains non-authoritative;
- algorithm changes may require version consideration.

**CORRECTION (D-057).** As originally written this read as a description of a *live*
mechanism. It was not one. `DeterministicRng` is fully implemented (xoshiro256\*\* with
SplitMix64 seeding, rejection-sampled `NextInt64`), persisted in `GameState.Rng`,
round-tripped field by field by the save codec, printed by `dump`, and covered by eight
unit tests — and **no production or tool code draws from it**. Grepping `src/` and
`tools/` for `NextUInt64|NextInt64|NextDouble` returns only the definitions.

The practical consequence is that **`StartNewGame(ulong seed)` cannot currently affect
any outcome**: the seed is written to the save and read back, and that is all. Two
profiles run with different seeds produce identical canonical state.

The **policy is correct and is kept**. What was missing was a consumer, not a policy. The
mechanism is retained as persisted infrastructure for the first canonical consumer, whose
obligations are now stated explicitly: it must add an end-to-end determinism test
covering the save/load round trip, because that will be the first time `Rng` is read, the
first time the seed matters, and the first opportunity to regress determinism. The
current inert behaviour is pinned by `SeedDoesNotAffectCanonicalStateTests` so that the
first consumer must revisit seed semantics deliberately rather than silently redefining
what a seed means.

A property test's use of the generator as a **data generator** is not canonical
randomness; `SeededProgressionPropertyTests` now names its local `testDataRng` and says
so, because that file is the single most misreadable reference in the repository.

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

1. Exact Unity 6 LTS editor/runtime version. *(resolved 2026-10-16, D-035: Unity 6000.3.26f1 (6.3 LTS), Personal licence; install/activation pending, KI-002)*
2. Exact local persistence implementation (snapshot+journal vs SQLite or another adapter). *(resolved by D-025/D-026: versioned JSON envelope with SHA-256 integrity, atomic snapshot + one-generation backup)*
3. Exact Android Health Connect SDK/API integration strategy and minimum supported OS/device policy. *(open — M7, physically blocked)*
4. Exact iOS HealthKit integration strategy and minimum supported iOS version. *(open — M7, physically blocked)*
5. Final Region 1 fiction/name/art direction. *(fiction resolved: Millbrook Valley, WORLD_AND_CONTENT; art direction still open — M6)*
6. Final Vitality conversion curve and safety bounds. *(curve resolved: conversion rule v1, 100 steps → 1 Vitality, integer floor, versioned. Safety bounds remain open — the `pathological` profile in D-046 measures ≈686 effective steps/Vitality under D-030 clamping, and no diminishing-returns design exists, so R-008 is untouched)*
7. Whether Materials and Knowledge both survive MVP balance testing. *(open — and now materially different: neither resource has any production consumer or cap, see the D-032 discharge note recorded under D-046)*
8. Notification categories and defaults after UX/device validation. *(categories/defaults resolved by D-042: opt-in master, four categories, optional reminder; "after UX/device validation" remains open — M5/M7)*
9. Asset delivery strategy for Visit World. *(open — M6)*
10. Whether cloud backup/sync enters post-MVP scope. *(open; ROADMAP post-MVP gate explicitly does not pre-authorize it)*

Resolve these through new decision entries with rationale and consequences; do not silently bake them into implementation.

*(Status annotation added 2026-09-29 during the documentation-truth campaign: this list was
presented as uniformly unresolved even though four entries had been settled by accepted
decisions, which is exactly the documentation-drift failure R-020 describes.)*

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
- correction transaction IDs derive from identity + revision + applied amount (
tx-corr1 namespace) and cannot collide with first-credit IDs;
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

---

## D-032 — Producer capacity is a bounded pending-output store (schema v2)

**Status:** Accepted *(implemented and automated verified: ProducerSimulationTests, GameStateValidationTests, MigrationV1ToV2Tests)*

**Decision:** `ProducerDefinition.CapacityUnits` is the size of that producer's bounded pending-output store, in whole units. Offline production mints `min(remainingStoreRoom, rate × elapsed)` into the store (`ProducerRuntimeState.StoredMilliUnits`, integer milli-units); surplus time beyond the room creates no waste and no value. Whole units auto-deliver into canonical resource balances every tick (never claimed manually). When a downstream resource-level cap refuses delivery, undelivered units stay parked in the producer's store up to its capacity and flush automatically on any later tick call, including zero-elapsed ones. Because minting happens before delivery within a tick, one tick can never produce more than the store's free room regardless of elapsed time; combined with `OfflineAdvancer.MaxProducerInterval` this bounds long absences explicitly. Producer checkpoints are monotonic at every callable boundary: the public `TickProducers` path refuses backward clocks (emitting ClockSkewIgnored) instead of backdating `LastTickUtc`, and unlock stamps the checkpoint at the completion instant so no retroactive production exists.

**Rationale:** GAME_SYSTEMS §5 specifies `produced = min(capacityRemaining, rate × eligibleElapsedTime)`; the previous implementation ignored `CapacityUnits` entirely and tests simulated caps via resource limits. A per-producer parking store keeps the anti-busywork rule (no claiming), gives multi-producer same-resource behavior an unambiguous rule (independent stores; the resource cap arbitrates delivery), and makes "capacity reached" mean one thing everywhere including diagnostics.

**Consequences:**
- Save schema bumped to v2; registered sequential migration `m1-to-v2-producer-stored-milli-units` promotes v1 `carryMilliUnits` (always < 1000) into `storedMilliUnits`. Representative v1 fixtures decode through the real chain; re-encode/re-decode is stable.
- With no resource cap set, delivery always succeeds and the store only ever holds sub-unit remainders — capacity binds when content defines resource caps or sinks (M4 concern). **This deferral was never discharged:** as of 2026-09-29 M4 shipped without defining any resource cap or sink, `ResourceBalances.SetCap` has no production call site (tests only), and `TryConsume` in `src/` only ever consumes Vitality. The bounded-store overflow, no-waste and parked-flush branch is therefore exercised solely by synthetic unit tests and never by the real simulation — all committed pacing reports show `producer capped-store days: 0`. Tracked as a candidate M5/M6 content campaign; see D-046.
- Validator enforces `0 <= StoredMilliUnits <= CapacityUnits × 1000`, locked producers hold nothing, and content validator rejects capacities unrepresentable in checked milli-unit math.

---

## D-033 — Return summaries are durable, typed, bounded re-entry state

**Status:** Accepted *(implemented and automated verified: ReturnSummaryDurabilityTests, M3AmbientProgressionAcceptanceTests)*

**Decision:** Canonical `GameState.PendingReturnSummary` (new additive field, schema v2 payload; absent on old payloads decodes to null which is exactly "nothing pending") stores the committed-but-not-yet-acknowledged summary. Every committing mutation composes its simulation events INTO the pending summary BEFORE persisting, via `ReturnSummaryComposer`: deterministic priority (transformation → actionable decision → production/notice → aggregate), text-level dedupe across merges, and a hard 12-item cap inside the glance budget. Items carry typed kinds (`SummaryItemKind`). A single derived `PrimaryNextAction` ("Queue the next restoration project." when actionable items exist; otherwise null = nothing needs attention). `AcknowledgeReturnSummary()` clears it idempotently and may never alter earned progression. Boot-time advancement summaries regenerate deterministically from checkpoints, so a crash before presentation loses nothing either way; ingestion/credit summaries rely on the durable copy.

**Rationale:** UX_DESIGN §4 makes the summary the central re-entry mechanism; the previous implementation aggregated events into ephemeral strings built AFTER the save, so a crash between commit and display lost the explanation of progress the player had already earned.

**Consequences:**
- Presentation renders read models (`ReturnSummaryReadModel`, Home/Projects/Region snapshots); no UI owns canonical state.
- The item cap evicts lowest-priority entries when new meaningful events arrive while stale ones are still unacknowledged; replayed activity adds notices at most, never fabricated progress claims (acceptance-tested).

---

## D-034 — One platform-neutral activity-source seam; development injector isolated behind it

**Status:** Accepted *(implemented and automated verified: IngestFromSourceTests, walk CLI replay proof)*

**Decision:** `Application.Activity.IActivityRecordSource` is the single seam for activity provenance (fixtures today; Health Connect/HealthKit adapters in M7 behind the same interface). `GameSession.IngestFromSource(source, windowStart, windowEnd)` fetches normalized records and pushes them through the unchanged `IngestActivityBatch` trust pipeline — there is no separate M3 crediting path. The deterministic `SyntheticWalkingSource` lives in `WalkGame.Application.Development`, namespaces all records `dev.synthetic-walking`, derives stable per-day source IDs (replay-safe), and enters production builds only if a composition root explicitly constructs it — which the documented roots never do. `tools/simulation walk --replay` re-feeds an identical window through fresh sessions and fails loudly if anything credits twice.

**Rationale:** Campaign gap: the simulator's multi-day loop bypassed validation/dedup/identity by calling CreditActivity directly. Keeping `credit`/`simulate` as labeled low-level diagnostics while making `walk` the acceptance harness preserves both developer convenience and trust-pipeline honesty.

**Consequences:**
- Retry/replay after restart is safe by construction (durable processed-record ledger), proven by tests rather than asserted.
- Production adapter work (M7) must implement the same narrow port; sources without durable record IDs cannot express corrections (unchanged D-029 requirement).

---

## D-035 — M3 presentation boundary delivered headless; Unity project deferred to a runtime-enabled session

**Status:** Accepted *(implementation reality of this campaign)*

**Decision:** This campaign delivers the complete presentation CONTRACT — Home/Projects/Region read models, queue/auto-advance/manual-start operations, durable summary acknowledgement, and the bootstrap-relevant seams (save store, clock, activity source, session) wired exactly as a Unity composition root will wire them — and proves the full player story through those application boundaries with automated acceptance tests. Creating `src/WalkGame.Unity` is deferred: no Unity 6 LTS editor exists in this execution environment, so a committed Unity project could not be opened, imported, compiled, or PlayMode-tested here, and shipping unverifiable editor YAML would violate the evidence rules (D-018).

**Rationale:** "Never fabricate editor/device evidence" (campaign §3, AGENTS.md honesty gates). Headless-verifiable M3 scope is complete; runtime-only gates are recorded UNVERIFIED, not silently omitted.

**Consequences:**
- **Resolved 2026-10-16:** Unity **6000.3.26f1** (Unity 6.3 LTS, released 2026-10-08, changeset `de8167e7440c`, supported to Dec 2027), **Personal** licence. Michael delegated the choice. Alternatives rejected: 6000.6.x is not LTS, and 6000.0 LTS reaches end of support in Oct 2026. Install readiness is in `docs/UNITY_INSTALL.md`. The editor is not yet installed or activated (KI-002 open).
- *(historical)* The exact Unity 6 LTS version remained unresolved decision #1; the next campaign with an installed editor starts at the Unity shell + EditMode/PlayMode gates, not at domain/application rework.
- Any future presentation technology must consume the same read-model/use-case boundary; nothing in Domain/Application references Unity.

---

## D-036 — M4 canonical state is additive under save schema v2; absent means "nothing yet"

**Status:** Accepted *(implemented and automated verified: M4BackwardDecodingTests, SaveCodecRoundtripTests)*

**Decision:** Discovery runtimes (`RegionState.Discoveries`), expedition runtimes (`RegionState.Expeditions`), region progression stages (`EcologyStage`/`SettlementStage`) and the completion markers (`IsCompleted`/`RegionCompletedAtUtc`) are added as additive schema-v2 payload fields with strict absent-means-default decoding: a missing entry always means "not yet discovered / not yet available / baseline stage / not completed". Entries appear only after their first canonical transition, so pre-M4 payloads and fresh saves share identical semantics without a schema bump or registered migration. `GameStateValidator` validates every present entry against authored content (unknown IDs, review-flag/timestamp consistency, availability-before-completion ordering, arc bounds, completion/milestone consistency).

**Rationale:** The v1→v2 producer migration precedent showed migrations must change semantics; none of the M4 fields reinterpret any existing persisted value, so a bump would add risk without protecting anything.

**Consequences:**
- Backward-decoding tests strip all new properties from a real v2 payload and prove decode + validate + re-encode stability.
- If a future field ever needs non-default history interpretation, it requires a versioned migration like m1-to-v2 — this decision does not create a general exemption.

---

## D-037 — Discoveries and expeditions ship at an M4 headless boundary

**Status:** Accepted *(implemented and automated verified: M4ProgressionMechanicsTests, M4Region1AcceptanceTests)*

**Decision:** A discovery is defined by stable ID, category, title/body/provenance keys, optional location key, and exactly one deterministic trigger: completion of one designated project. Unlock is derived from canonical completion effects, fires at most once, and reviewed state is separate presentation convenience (`MarkDiscoveryReviewed`, idempotent, never gates progression). An expedition is a stable route definition (title/description keys, required projects, required landmark stages, optional one-time integer reward) with deterministic availability (all required projects completed) and completion (all required landmark stages reached) hooks evaluated inside the same canonical effects pipeline; rewards apply cap-clamped in the same state transition as the completion timestamp. Neither system has foreground interaction at M4; both continue while the app is closed.

**Rationale:** GAME_SYSTEMS §7/§8 list richer trigger models (activity thresholds, seeded rolls, visit interactions) that cannot be honestly exercised or validated without runtime presentation. The chosen boundary keeps definitions durable and validation complete while leaving those extensions open.

**Consequences:**
- Adding trigger kinds later extends `DiscoveryDefinition`; the durable unlocked/reviewed state shape is final.
- Expedition cancellation/retry rules are intentionally absent: there is nothing to cancel while routes resolve automatically.

---

## D-038 — Region progression uses two discrete arcs plus one closure project; post-completion is evergreen

**Status:** Accepted *(implemented and automated verified: M4ProgressionMechanicsTests, GameStateValidationTests)*

**Decision:** Region-level restoration state is represented by strictly-ascending discrete stage arcs — ecology and settlement — where each arc stage names the single project whose completion advances it, and by an explicit closure milestone project (`CompletionMilestoneProjectId`). Arcs advance monotonically inside canonical completion effects (never continuous simulation); reaching closure sets `IsCompleted`/`RegionCompletedAtUtc` exactly once and nothing ever resets it. Producers, discoveries and flourishing stages keep operating after closure.

**Rationale:** WORLD_AND_CONTENT §8/§9 and GAME_SYSTEMS §9/§10 demand explainable, presentation-bindable progression without city-builder micromanagement; discrete stages satisfy the attention budget and remain fully validator-checkable.

**Consequences:**
- Arc stage counts are bounded (≤10) and validated ascending with resolvable unlockers.
- `GameStateValidator` rejects completion claims inconsistent with content (flag without milestone, milestone completed without flag, timestamp without flag).

---

## D-039 — Region 1 pacing is measured by deterministic profile simulation; provisional targets documented, low-profile long tail accepted for M4

**Status:** Accepted *(automated verified: `tools/simulation profile`, reports committed under `docs/evidence/m4/`, M4Region1AcceptanceTests byte-determinism)*

**Superseded in part by D-062 (2026-10-09):** the roster costs and the measured completion days quoted below are the v2 numbers. D-062's rebalance moves them (high 97→77, moderate 242→192, irregular 139→109, weekend-heavy 201→158, pathological 29→22, and `inactivity` from never-closing to day 376). The low-profile long tail quoted here is retained unchanged and re-affirmed there.


**Decision:** Pacing evidence comes from the `profile` CLI verb: a deterministic auto-player (catalog-order queue choice only when fully idle; auto-advance on) drives fresh saves through the real ingestion pipeline under four fixed step patterns — low 3,000/day, moderate 8,000/day, high 20,000/day, irregular [26k,15k,2k,18k,6k,22k,9k]/week — to completion or horizon, printing a stable report (completion day, per-chain vitality/completion days, decisions, queue-empty days, capped-store days, discovery/expedition pacing, final arcs, validator cleanliness). Measured results (seed 42): high completes day 97, moderate day 242, irregular day 139; low completes 12,000 of ~19,400 vitality within 400 days (wetland chain done day 387; forest/research open). Provisional targets: high ≤120 days, irregular ≤250, moderate ≤300 — all met; low-profile completion beyond one year is recorded as an accepted characteristic of a movement-proportional economy, not hidden. Foreground pressure stays minimal everywhere: exactly one queue decision per project (19 total), zero-to-one queue-empty days, zero producer cap waste.

**Rationale:** The campaign forbids inventing thresholds just to pass tests. Publishing measured distributions with explicitly provisional targets keeps balance work honest and gives future tuning (more activity categories, cost scaling) a regression baseline.

**Consequences:**
- Content cost changes require regenerating `docs/evidence/m4/pacing-*.txt`.
 - The low-profile long tail is the top input for any future pacing campaign; it is a design characteristic, not a defect against the no-punishment rule (no progress decays).

---

## D-040 — Recovery commits preserve the last healthy generation

**Status:** Accepted *(implemented and automated verified: PersistenceFaultInjectionTests, SessionPersistenceHardeningTests, M8H1HardeningAcceptanceTests)*

**Decision:** `ISaveStore` gains `WriteAtomicPreservingBackup`: an atomic commit to the primary slot that never rotates the current primary into the backup. The boot-recovery path (`GameSession.Continue` → recovered-from-backup) uses it exclusively. A known-corrupt primary may therefore NEVER displace the last valid backup generation, and after a recovery commit at least two decodable generations exist (backup N−1, primary N′). Interruption windows during recovery leave either the exact pre-recovery state or a strictly better one.

**Rationale:** The previous recovery path reused `WriteAtomic`, whose first step promotes whatever sits in the primary slot — including bytes just proven corrupt — into the backup, destroying the last healthy copy as a safety net; a crash inside that same window could then lose every valid generation.

**Consequences:**
- normal (non-recovery) writes keep rotation semantics unchanged;
- reads classify access-denied/inaccessible paths as IoFailure with detail — "no save found" means genuinely absent; persist paths surface access failures as the documented IOException type;
- boot surfaces specific decode/validation failure reasons instead of a generic unreadable message;
- stale crash temporaries are removed at store construction.

---

## D-041 — Producer runtime rows are mandatory canonical state

**Status:** Accepted *(implemented and automated verified: GameStateValidationTests coverage via validator rule; MatureSaveMigrationTests)*

**Decision:** `GameStateValidator` requires exactly one runtime row for every content producer, mirroring the existing project-runtime rule. Missing rows are corruption and fail closed at load.

**Rationale:** Producer rows are created for the full content set at game start; a silently missing row would permanently disable that producer's production and unlock path while everything else appeared healthy. Unknown EXTRA rows were already rejected; absence was the unguarded direction.

**Consequences:** payloads produced by any factory since M1 always satisfy the rule; hand-crafted or damaged saves missing a row are rejected with a specific diagnostic instead of silently degrading.

---

## D-042 — Durable local UX preferences and onboarding progress live in a separate versioned store, never in canonical game state

**Status:** Accepted *(implemented and automated verified: LocalPreferencesStoreTests, OnboardingAndPreferencesTests, M5H1ShellAcceptanceTests, M5H1ContractHardeningTests)*

**Decision:** Onboarding stage/completion, reduced-motion/haptics/sound flags, notification opt-in + per-category toggles + optional daily reminder time, and diagnostics visibility are **durable local UX state**. They are persisted in a dedicated `IUxPreferencesStore` (file implementation `LocalPreferencesStore`: single JSON file, versioned envelope schema v1, atomic temp+flush+replace write, NO backup generations) and never enter `GameState` or the canonical save envelope.

**Rationale:** Ownership audit (M5-H1 Workstream A): none of these values affect game rules, reward math, queue behavior, or content completion, so canonical ownership would be misclassification; but they must survive restarts, so they cannot be ephemeral. A separate physically-isolated record makes preference corruption unable to touch progression and makes "preference writes leave canonical save bytes identical" provable byte-for-byte. The value of the data is low: one atomic file without backup chains is proportionate; worst case is re-setting preferences, never gameplay loss.

**Consequences:**
- load policy is explicit: absent → NotFound→defaults; malformed/JSON-invalid/wrong-typed/pre-history version → Malformed→defaults; future version → FutureVersion, payload never interpreted; v1 keys merge over documented defaults so missing keys mean default;
- preferences NEVER block boot: any damage degrades to defaults while gameplay loads normally;
- setters fail with stable code `ux.preferences-store-missing` when no store is wired instead of silently diverging;
- onboarding progression is forward-only and idempotent; reaching Complete requires a first project to exist in canonical queue/active/completed state — earned only through real project operations (`EnqueueProject`/`ActivateQueuedProject`), so denial of activity permission can never trap onboarding;
- notification QUIET HOURS are delegated to the operating system (UX_DESIGN §13 mandates respecting system settings); this store persists category toggles and an optional reminder time-of-day only;
- canonical presentation-affecting state that already exists elsewhere (queue auto-advance) is surfaced in `SettingsReadModel` marked canonical — never duplicated into preferences.

---

## D-043 — Activity connection/permission status is an adapter-owned projection behind one platform-neutral port; status never mutates progression

**Status:** Accepted *(implemented and automated verified: ActivityStatusProjectionTests, M5H1ContractHardeningTests, M5H1ShellAcceptanceTests scenarios 2/7/8)*

**Decision:** One narrow port, `IActivityConnectionPort`, reports ephemeral platform truth (permission state incl. partially-granted/revoked, availability, refresh timestamps, adapter-owned technical detail). Player-safe classification is a PURE projection (`ActivityStatusProjector`) over that snapshot plus two canonical facts: processed-record existence, and the durable last-ingestion outcome. The six standing states use UX_DESIGN §5 vocabulary with documented precedence: denied/revoked → permission-denied; not-requested → permission-needed; unsupported provider → source-unavailable; transient adapter failure or durable fetch-failure → refresh-temporarily-failed; connected with zero processed records → waiting-for-first-data; otherwise connected-current. "Data processed successfully" rides separately as a last-outcome fact so standing state and event outcome are never conflated.

**Rationale:** Before M7 delivers Health Connect/HealthKit adapters, the shell needs honest, testable player-facing source state without native SDKs or fake device claims. Future adapters implement exactly this port; conformance is pinned by table tests today.

**Consequences:**
- raw exceptions/messages can never become ordinary player copy: read models carry enums, counts, timestamps only; adapter technical detail surfaces exclusively through the bounded diagnostics projection;
- status reads are side-effect free and byte-neutral (proven against save bytes);
- external revocation/grant changes are representable at any moment by swapping snapshot values; earned progress is untouched;
- a failed adapter fetch durably records `IngestionOutcomeState` with `SourceFetchFailed` and an error category equal to the exception TYPE name, in the same atomic discipline as successful batches, then rethrows — callers keep existing semantics while the shell gains cross-restart "temporarily unable to refresh";
- no HC/HK SDK, OS permission dialog, or device verification exists in this scope; those remain UNVERIFIED runtime items.

---

## D-044 — Support diagnostics expose only privacy-safe operational facts in one bounded projection

**Status:** Accepted *(implemented and automated verified: DiagnosticsReadModelTests, M5H1ContractHardeningTests leak sweep)*

**Decision:** `DiagnosticsReadModel` is the single support-oriented surface: boot outcome classification (including recovered-from-backup and the structured codec/state-validation failure category of the last failed decode, retained monotonically so a successful backup recovery cannot erase primary-failure evidence), applied migrations, schema version, region identity, ingestion checkpoint watermark plus age in days (reported only after a real batch sets it), processed-record count, lifetime credited vitality, forever-visible unapplied-reversal vitality (D-029), the bounded last-batch counter row, the preferences-load outcome, and optionally the adapter-owned bounded technical detail string. Display gating uses the local DiagnosticsVisible preference.

**Rationale:** ACTIVITY_PIPELINE §19 and RISK_REGISTER R-022 require redacted bounded diagnostics; players and support need operational facts after crashes, which demands durability across restarts and availability even when boot FAILED.

**Consequences:**
- no raw records, payloads, stack traces, or exception messages generated by this codebase ever appear; error categories are stable type/class names;
- every text field from adapters/stores is truncated (300 chars max) and lives behind the explicit diagnostics gate;
- reading diagnostics is side-effect free and available pre-boot-success (schemaVersion 0 / empty identity until state exists);
- aggregates stay permanently bounded (single counter row) honoring PERFORMANCE_BUDGETS memory/save rules.

---

## D-045 — Systemic performance pass: cached content/validation, decode fast path, durability-preserving commit I/O

**Status:** Accepted *(automated verified: full test suites, guard proof suite 25/25, `bench` phase harness, longhaul/profile/walk outputs byte-identical to committed evidence)*

**Decision:** Four optimizations on the boot/ingest/commit hot paths, none changing canonical semantics:
1. `Region1Catalog.Create()` returns one process-wide instance. Every definition is constructor-frozen (get-only properties), so rebuilding a 20 KB immutable content graph per session — once per boot and once per simulated app-closed day — was pure duplicate work.
2. `GameSession` validates content at most once per distinct content instance (`ConditionalWeakTable`, atomic per-key `GetValue`). Invalid content still fails closed on every construction; valid graphs skip re-walking reachability/cycle analysis.
3. `SaveCodec.Decode` deserializes current-version payloads directly from payload bytes. The JsonNode DOM materialization is now paid only when a migration actually needs to transform the tree (v1→v2 path unchanged).
4. Commit I/O keeps `Flush(flushToDisk:true)` as THE durability barrier and drops only `FileOptions.WriteThrough` (redundant behind an explicit FlushFileBuffers on a single buffered write); the backup rotation copy streams instead of buffering the whole save in memory.

**Rationale:** Phase-level measurement (`tools/simulation bench`) showed decode ≈6× encode (DOM double-parse) and ~0.13 ms/session of duplicated catalog+validation work. Longhaul 365-day wall time improved ≈27% (≈6.95 s → ≈5.1 s median) with identical final state bytes, ledger sizes and pacing reports; decode −51%, session construction −58%.

**Consequences:**
- rejected: rename-chain backup rotation (would widen the primary-empty crash window and force boot-fallback semantic changes in the durability core for ~1 flush/commit);
- rejected: skipping the boot persist when no events fired (producer integer milli-unit truncation makes tick granularity observable; risk without measurable gain);
- deferred: processed-record/reward-ledger compaction for save-size bounding (PERFORMANCE_BUDGETS §14–15) — late deletions target ledger rows without horizon checks, so compaction changes deletion semantics and requires a product decision;
- deferred: STJ source generation — projected single-digit ms/boot gain does not justify serializer-mode drift risk while reflection mode remains correct;
- `bench --save <dir> [--iterations N] [--days N]` stays available as the regression-measurement entry point.

---

## D-046 — Pacing evidence covers all seven GAME_SYSTEMS §13 profiles; intermittent engagement is recorded as an open balance finding

**Status:** Accepted *(automated verified: seven reproducible reports, SHA-256 byte-identical on re-run; the four pre-existing M4 reports unchanged)*

**Decision:** `tools/simulation profile` resolves its `--profile` name through a `PacingProfile` table covering all seven profiles named by GAME_SYSTEMS §13 and repeated by R-013: `low`, `moderate`, `high`, `irregular` (pre-existing), plus `weekend-heavy`, `inactivity` and `pathological` (added). A profile resolves a day index to a synthetic walking quantity, or 0 to model a day on which the source returned **no record at all** — never a zero-quantity record, which the trust pipeline rejects by design. The four original profiles keep their exact positional rotation so the evidence committed under `docs/evidence/m4/` still reproduces byte-for-byte. `weekend-heavy` keys off the real calendar weekday rather than array position so it stays correct for any `--at` instant.

**Rationale:** Only four of the seven profiles existed, so R-013's mitigation was half-implemented and GAME_SYSTEMS §13 was unmet. Two of the new profiles changed the balance picture:

- `pathological` completes the region on **day 29** from 3,184,633 ingested steps yielding 21,844 Vitality — ≈**686 steps/Vitality** against the nominal 100. The D-030 pathological-quantity clamp is doing real work; "100 steps → 1 Vitality" is a ceiling, not a guarantee.
- `inactivity` (silent weekends plus a fully silent day 100–144) **does not reach the closure milestone within 400 days** — forest and research chains stay incomplete while both arcs reach 4/4.

**Consequences:**
- committed evidence `docs/evidence/m4/pacing-weekend-heavy.txt`, `pacing-inactivity.txt`, `pacing-pathological.txt`, each reproducible by SHA-256;
- the intermittent-engagement shortfall is **recorded, not silently re-balanced** — changing chain costs or Vitality awards would invalidate the four previously committed M4 pacing reports and the D-039 provisional targets, which is a product-balance decision belonging to the M5/M6 milestone;
- silent days still run the full canonical day loop (offline advancement, queue policy, production), so an absent player is simulated rather than skipped.

---

## D-047 — Architectural layering and the exactly-once invariant are CI-enforced, not aspirational

**Status:** Accepted *(automated verified: 6/6 architecture assertions green on the real tree; every rule proven to fail against a deliberately-broken fixture; `walk --replay` reports `vitality credited: 0` / `duplicate records ignored: 16`)*

**Decision:** Two new mechanical gates replace documented-but-unenforced intentions.

1. `scripts/verify-architecture.sh` enforces the layering: Domain references nothing and targets `netstandard2.1`; Application references only Domain; Infrastructure references only Application + Domain; no engine namespace appears anywhere in `src/`; Domain and Application perform no filesystem access. Comments are stripped before the filesystem scan, so prose such as "no separate code path." cannot produce a false positive. It is the countermeasure for R-017 and implements the mechanically-verifiable subset of TECHNICAL_ARCHITECTURE §23.
2. CI now runs `walk` then `walk --replay` and **asserts** the replayed window credits zero and reports 16 duplicates. The repository's flagship invariant previously had no CI gate whatsoever. CI also runs `longhaul --days 365` (§7 offline simulation was documented but never executed in CI) and a Release `dotnet publish` build smoke (TESTING_AND_RELEASE §16 gate 9).

**Rationale:** §16 listed ten target CI gates and implemented six; R-017's "dependency checks" mitigation bullet had no enforcement behind it, and the exactly-once property that defines this product was proven only by locally-run evidence documents.

**Consequences:**
- a `dotnet format --verify-no-changes` gate was **deliberately not adopted**: it fails against the existing tree and adopting it would force mass reformatting churn in files unrelated to any risk. The tree is held at 0 build warnings instead;
- each rule was verified to *fail* against a deliberately-broken throwaway fixture (injected `UnityEngine` using, `File.AppendAllText` in Application, a `net9.0` Domain, a Domain→Infrastructure reference) — a gate that has only ever passed proves nothing;
- `verify-architecture.sh` exits 2 with a clear message when run outside the checkout, rather than silently scanning the wrong tree.

---

## D-048 — Performance regression scenarios and save-size growth are measured and gated; the remainder is device-blocked

**Status:** Accepted *(automated verified: 5 of 10 PERFORMANCE_BUDGETS §19 scenarios measured headlessly, 1 by explicit surrogate, 4 deliberately not simulated; save-growth series reproduces exactly across independent runs; 0 validator violations at maximum content)*

**Decision:** `tools/simulation scenarios` implements the headless-measurable subset of PERFORMANCE_BUDGETS §19 and the §15 save-growth obligation that nothing previously performed. It emits two line classes:

- `SCENARIO-RESULT` carries wall-clock and status. **Not gated** — machine-dependent, and TESTING_AND_RELEASE §17 treats flaky timing assertions as defects.
- `SCENARIO-SIZES` carries save bytes, ledger sizes, credited totals and resource balances. Deterministic for a fixed seed and content, and therefore gated by `scripts/verify-save-growth.sh`, which runs the harness twice and requires exact reproduction.

**Rationale:** §19 named ten scenarios with none implemented, and §15 stated a CI obligation that was never met. Growth existed only as prose inside `docs/evidence/m8-h1/`.

**Consequences:**
- §19 is now **5/10 measured headlessly, 1/10 by surrogate (background/resume, which repeated boot-from-disk is *not*), 4/10 not simulated (Home idle, Visit World enter/traverse/exit)**. No figure was invented for the four device-only scenarios; a fabricated number would be worse than an acknowledged gap;
- scenario 6 is measured for its **canonical** cost only — a landmark stage transition in canonical state, never its rendered frame cost;
- save growth is measured **linear and unbounded at ~525-530 bytes per ingestion day** (day 7: 10,344 B; day 365: 202,644 B; day 730: 394,401 B), corroborating the ~554 B/day figure in `LONG_HORIZON.md`. Region completes at day 365 and stays stable through day 730, so post-completion growth is pure ledger accumulation — the shape D-045 deferred;
- the same run **quantifies the D-032 deferral**: at day 730 a save holds 2,604 Materials and 638 Knowledge with no consumer anywhere in `src/`, so producer capacity still can never bind;
- adding project secondary-resource requirements is explicitly NOT done here. It alters progression balance, and `ContentValidator`'s critical-path reachability would have to prove a project is *schedulable* given what its unlock dependency actually produces, or it can certify a reachable graph that deadlocks in play. This campaign produced the measurement series such a change must be judged against, and stopped;
- the harness also corrects a misleading claim: an incremental `dotnet build` reports 0 warnings, but a clean `--no-incremental` build reveals 8 pre-existing xUnit analyzer warnings in test code. Production code is genuinely at 0 warnings. TESTING_AND_RELEASE now records the distinction so "0 warnings" is never cited without qualification;
- evidence: `docs/evidence/performance/SCENARIOS.md`.

---

## D-049 — The complete CI gate is mechanically reproducible locally while hosted runners remain blocked

**Status:** Accepted *(full ci.yml body reproduction at `3b5d3e0`: 12 ok / 2 hosted-only SKIP / 0 failed; 295/295 tests; all four blocked runs confirmed `steps: 0` via gh)*

**Decision:** `scripts/verify-ci-steps.sh` parses `.github/workflows/ci.yml` mechanically and executes every `run:` step body locally, in checkout order, under the GitHub Actions Linux default shell (`bash --noprofile --norc -eo pipefail`), honoring `continue-on-error` as WARN, exporting `GITHUB_REPOSITORY` and a CI-style `RUNNER_TEMP`, and reporting hosted-only `uses:` steps as explicit SKIPs backed by asserted local facts. The extractor is guarded both directions: extracted run/uses counts must equal the workflow's declared occurrences, and an empty extracted body is a hard failure — parser drift can only abort loudly, never report a hollow success.

**Rationale:** no workflow run has progressed since `33293682455` (2026-08-30); the account-level billing/spending block (runs annotated "recent account payments have failed or your spending limit needs to be increased") holds C1–C3 and HEAD runs at `steps: 0`, and the repository's documented position (TESTING_AND_RELEASE §16) is that hosted CI is an external blocker awaiting an account owner while every gate remains locally reproducible. Release confidence cannot wait indefinitely on billing support, and what is needed is not the hosted *platform* but the hosted *gate* — and that gate is defined by the text of `ci.yml`.

**Consequences:**
- the reproduction tool now lives beside the workflow it reproduces; its hollow-pass guard proved necessary during its own development — the first two runs falsely reported 12/14 ok because step keys at 8-space indentation were parsed against 6-space patterns, and every "empty script" exited 0 (miss recorded in `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md`);
- a local reproduction is a reproduction, not hosted CI: Linux-runner environment behavior remains unverified until the block lifts, and the first hosted green run must be diffed against this package's recorded results;
- `uses:`-step assertions and the publish-apphost existence check follow MSYS `-x` semantics locally; equivalence with the Linux ELF apphost check was verified by inspection, not execution;
- evidence: `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md`.

---

## D-050 — Release-evidence registers (known issues, privacy review) are living documents, headless-scoped

**Status:** Accepted *(all mechanical claims verified by recorded commands at `e2ea00a`; runtime/device halves explicitly reserved UNVERIFIED)*

**Decision:** The two §18 release-package artifacts that can be produced without runtime, devices, or hosted CI are established as durable, versioned documents instead of one-off release attachments: `docs/KNOWN_ISSUES.md` (severity register per §15; any newly accepted-open defect, gap, or verification hole gains an entry; Critical/High entries block release qualification by rule) and `docs/PRIVACY_REVIEW.md` (every claim backed by a re-runnable command: zero network APIs in sources, single runtime NuGet dependency, save-content inventory, host-injected storage paths, test-enforced privacy-safe diagnostics). Both are scoped to the headless lanes; no runtime/device privacy or permission claim is made.

**Rationale:** M9 deliverables list both artifacts; neither requires the blocked lanes; a living register makes externally blocked items (KI-001 hosted CI, KI-002 runtime) impossible to forget and locally accepted risks (plaintext health-adjacent saves) impossible to hide. Encoding privacy mechanically (grep-verifiable absence of network surface, test-enforced diagnostics safety) matches the repository evidence culture far better than prose assurances.

**Consequences:**
- two Medium findings are now formally open and must be dispositioned before M9: no explicit privacy requirements section in PRODUCT_SPEC (KI-005) and plaintext at-rest save data with OS encryption as the only shield (KI-006);
- tooling/process items KI-007 (fixture hook env coupling) and KI-008 (stranded writer leases from killed foreground suites) join the backlog with evidence;
- when the runtime/device lanes unblock, both documents require a second pass; until then they bound every privacy claim to the headless code;
- evidence: `docs/PRIVACY_REVIEW.md`, `docs/KNOWN_ISSUES.md`.

---

## D-051 — The build toolchain is pinned, shared settings are declared once, and a warning is an error

**Status:** Accepted *(verified at the F-12 hardening pass: `dotnet build --no-incremental` 0 errors / 0 warnings; `dotnet test` 295/295 unchanged; `scripts/verify-architecture.sh` 11 passed / 0 failed with the netstandard2.1 rule proven to fail on a broken fixture; `scripts/verify-ci-steps.sh` 12 ok / 0 warned / 0 failed / 2 hosted-only SKIP)*

**Decision:** three repository-root files become the single declarations site for the
build toolchain. `global.json` pins the SDK to `9.0.100` with `rollForward: latestFeature`
and `allowPrerelease: false`; `Directory.Build.props` carries the universal settings
(`Nullable`, `TreatWarningsAsErrors=true`, `WarningLevel`, `Deterministic`) and is imported
explicitly by `src/Directory.Build.props`, which layers the production C# 9.0 language pin
on top; `Directory.Packages.props` enables central package management and declares every
package version once. `ci.yml` installs the pin with `actions/setup-dotnet`'s
`global-json-file:` instead of a hardcoded `9.0.x`, and `scripts/verify-ci-steps.sh`
asserts that the local SDK satisfies `global.json` — failing loudly if the pin file is
missing, if the workflow stops declaring it, or if no installed SDK satisfies it. The eight
pre-existing xUnit analyzer warnings were **fixed** (assertion shape only) and the
warnings-as-errors policy was then switched on for every project, with no `NoWarn` and no
exemptions.

**Rationale:** §14 requires a "documented toolchain version" and it was satisfied only by
prose ("9.x today"), which is not checkable. With hosted CI blocked (KI-001), local
reproducibility is currently the *only* reproducibility story this repository has, so an
unpinned SDK and a build where "0 warnings" depended on incremental caching were not
acceptable debt. The policy ordering matters and was deliberate: the warnings-as-errors
switch was only turned on *after* the eight known warnings were genuinely fixed, because
suppressing them to make a policy satisfiable is exactly the failure mode the
unenforced-warnings arrangement had already produced. `latestFeature` rather than an exact
pin keeps onboarding honest — a contributor without the exact patch builds fine, while a
contributor on a different major gets a self-explanatory refusal instead of a subtly
different binary.

**Consequences:**
- each shared setting now has exactly one declarations site; adding a `Version` to any `PackageReference` is an error (`NETSDK1206`), so the three test projects can no longer drift apart;
- `dotnet build` means the same thing with and without `--no-incremental` for new commits. The old caveat ("never cite 0 warnings without `--no-incremental`") remains true of **evidence recorded before this change** and was left in place there deliberately;
- **the architecture gate was rewritten, not left alone.** It previously grepped `<TargetFramework>netstandard2.1</TargetFramework>` out of each csproj, so centralising the property would have silently hollow-passed the rule on all three layers. It now reads the **effective** MSBuild property (`dotnet msbuild <proj> -getProperty:TargetFramework`), fails closed if that read is unavailable, and self-checks the reader against a project that is legitimately `net9.0`. Proven by two broken fixtures: moving `WalkGame.Domain` to `net9.0` in the csproj fails the rule, and moving it to `net9.0` via `src/Directory.Build.props` (csproj emptied of the property) fails it identically — which is the case a grep would have missed. The healthy count remains 11 passed / 0 failed;
- `TargetFramework` deliberately stays local to each production csproj. It is the one property whose declaration site is itself part of the architecture contract, and keeping it visible keeps the gate simple and honest; `LangVersion` did not survive the same reasoning because the net9.0 projects legitimately need C# 10+ syntax (file-scoped namespaces in the test suite);
- `TreatWarningsAsErrors` applies to the test projects too. An alternative that gated production only was rejected as a needless asymmetry: a warning in a test is precisely how a test rots;
- `dotnet format` remains deliberately **not** adopted as a style gate. The eleven over-length lines in `ci.yml` are pre-existing (identical count before and after this change, zero introduced) and the `run:` bodies cannot be reflowed without changing what CI executes, which is the exact drift `verify-ci-steps.sh` exists to prevent. That decision is not reopened here and is recorded rather than silently ignored;
- the eight analyzer fixes are recorded in `docs/evidence/quality/CAMPAIGN-2026-09-30.md`;
- a fully hermetic build (container-pinned toolchain) is still disproportionate for a single-developer-quality repository and stays on the deferred list.

---

## D-052 — Boot addresses both save generations before concluding no save exists, and the outcome taxonomy is exhaustive

**Status:** Accepted *(verified at the F-01 pass: 8 new application-layer regression tests; CLI reproduction before/after; `dotnet test` 303/303 — Domain 105, Infrastructure 48, Application 150 — green; `verify-architecture.sh` 11/0; pacing evidence 7 reproduced / 0 drifted; save-growth verified; guard suite 25/0)*

**Decision:** `GameSession.Continue()` addresses the primary and backup generations
**independently**. `StartStatus.NoSaveFound` is produced only when **both** probes classify
as absent, and that path performs no write. A decodable backup is preferred over reporting
absence for **every** reason the primary is unavailable, including the primary file being
absent entirely. The cause is then modelled, not the symptom: a new
`SaveRecoveryCause` (`None` / `PrimaryDamaged` / `PrimaryMissing`) and a new
`DiagnosticsBootOutcome.RecoveredFromMissingPrimary` distinguish a crash-window primary
loss from a damaged primary, while `StartStatus` deliberately gains no new value — to a
caller, recovering a world is recovering a world, and the finer distinction belongs in the
support projection. In the same pass `StartStatus.StateInvalid` and
`DiagnosticsBootOutcome.StateInvalid` were wired up for the decoded-but-invariant-violating
case, making every declared outcome value reachable and the taxonomy non-overlapping;
`tools/simulation` now returns exit 2 (documented "domain/validation failure") for that
case instead of 3 ("save missing/unreadable"). Recovery commits continue to route through
the existing `ISaveStore.WriteAtomicPreservingBackup`; no store method was added. The
recovery notice is parameterised on the cause, because "your latest save was damaged" is
untrue when the file was simply missing, and an inaccurate notice on a recovery is how a
player concludes their world was corrupted and starts over.

**Rationale:** the store had been correct all along — `AtomicFileSaveStore.ReplaceFile`
promotes a generation with a delete-then-move pair and its own comment documents that a
crash between the two steps leaves the previous generation in backup, leaving the primary
**transiently absent**. That is a reachable state, and the boot flow classified it as "no
save exists" without reading the healthy backup. Any composition root treating "no save" as
a fresh profile then overwrote the only surviving copy: total loss of the player's world,
in the flagship product invariant. The deepest lesson is not the missing `if`; it is that
**a mitigation existing is not a mitigation being exercised**. `WriteAtomicPreservingBackup`
(D-040) protected the last healthy generation perfectly — on a code path no caller took,
and a store-layer test asserted the exact on-disk state while the application layer had
none. That is why the new tests are application-layer, driving `Continue()`.

**Consequences:**
- the behaviour change is visible and deliberate: an invalid-state save now exits 2, not 3. `MatureSaveMigrationTests` asserted the old misclassification for two payload-surgery cases (an unknown content id decodes cleanly, then fails validation) and was updated to the precise class — the fail-closed and untouched-bytes assertions were kept unchanged;
- a *deliberately* deleted primary now presents to a player as a recovery rather than an error. That is the correct default for a game whose promise is "your world is never destroyed", and the distinction is available to support via `BootRecoveryCause` / `RecoveredFromMissingPrimary`;
- the player-facing no-leak sweep is now exhaustive by construction — `GetDiscoveries`, `GetExpeditions` and `GetPendingReturnSummary` were unwatched, i.e. the one test whose purpose is preventing a leak class had three unwatched surfaces (F-17);
- boot-outcome evidence stays in the session object rather than being persisted: a second write during a recovering boot is the wrong trade, and no other session would ever read it;
- no store write sequence, no save schema, no envelope framing, no migration chain was touched. Rollback is a plain revert;
- multi-generation history and a write-ahead journal remain **deferred**, not open: they reduce the probability of the state, whereas this change makes the state recoverable. Recorded in the master plan's deferred list so the next agent does not re-investigate;
- `docs/KNOWN_ISSUES.md` gains a "Closed" section and an explicit note that the register held **no entry** for this Critical-class data-loss path despite it being present, reproducible and unrecoverable. A register that cannot hold a Critical defect is itself the finding.

---

## D-053 — The activity acceptance window is anchored to trusted time, and deletion markers obey the same eligibility rules

**Status:** Accepted *(supersedes D-030's constants and reference point; verified at the F-02/F-03/F-09 pass: 17 new tests in `TrustedTimeWindowTests`, `dotnet test` 322/322 — Domain 105, Infrastructure 48, Application 169 — green; all seven `docs/evidence/m4/pacing-*.txt` reports still reproduce **byte-for-byte**; `verify-architecture.sh` 11/0; save-growth verified; guard suite 25/0)*

**Decision:** `ActivityValidationPolicy.Validate` now takes a `TrustedTime` reference
instead of a bare `nowUtc`, and stays `static`, side-effect free and **O(1) per record**
with no dependency on `GameState` — required, because `verify-architecture.sh` rule 5
keeps Domain free of ambient state. `TrustedTime` is computed **once per batch** in
`IngestActivityBatch` and carries the injected clock, the latest durably trusted record
end, and the reconciliation watermark. The window is:

| Bound | Value |
|---|---|
| lower | `min(watermark, latest trusted end)` − **`TrustedWindowFloor` = 45 days**, falling back to `now` when a profile has no trusted progress at all |
| upper | `max(now, watermark, latest trusted end)` + `MaxFutureSkew` = 10 minutes |

The eligibility-first restructure also applies the **same eligibility rules to deletion
markers**, which previously bypassed validation entirely and could push the watermark
arbitrarily far forward. `DeletionsIgnored` / `StaleRevisionsIgnored` are retained as
*policy* outcomes (unknown logical record, stale revision); eligibility failures are now
*rejections* with a named reason.

**Rationale:** D-030's window was anchored to raw device wall clock, and both of its
anchors were wrong for the situations this product exists for. A player returning after a
holiday had their **entire** provider backlog rejected as `OutsideHorizon` (F-02) — the
14-day bound was introduced to protect the offline-first thesis (D-003/D-004) and instead
destroyed it. A device clock **minutes** fast made every record future-dated and stopped
crediting altogether, with no player-visible explanation (F-03); `GAME_SYSTEMS.md` §15
already required not to rely solely on device wall clock. The problem was never the width
of the constants: a wider fixed window is still wrong for a fast clock and still too narrow
for a 45-day absence. **The anchor was the defect.**

**The 45-day floor — how it was chosen.** It answers "how far *before* the last thing we
trusted will we still believe a record?". It comfortably exceeds the longest absence the
product treats as normal (a 30-day return is the acceptance case, `MASTER_PLAN` §10) and
stays far below unbounded device history. It does **not** bound the length of an absence:
records *after* the last trusted end are inside the window regardless of how far back that
trusted end is, which is precisely what makes a 30- or 45-day absence creditable.

**Why both bounds use `min`/`max` rather than a single anchor.** The lower bound takes the
*earlier* durable marker so a watermark pushed forward by a late deletion cannot silently
expose older history; the upper bound takes the *later* so a fast clock degrades the
anti-future bound instead of disabling crediting, and so a backward clock jump can never
pull the bound below the already-trusted end. The watermark itself remains monotonic and
never regresses.

**Consequences:**
- **`ReconciliationHorizon` (14 days) is retained as a name only** and is no longer used by
  validation, so the historical decision stays locatable in review while the value it
  described is not silently resurrected. `TrustedWindowFloor` is the live constant.
- **Two re-scoped tests, neither deleted.** `RecordBeyondReconciliationHorizon_IsRejectedWithoutSideEffects`
  now uses a 60-day-old record, because a 15-day-old record is *correctly accepted* under
  the new window — keeping the old fixture would have turned an anti-abuse requirement into
  a dead test. A companion test asserts the other half: a 30-day-old record **is** accepted.
  `ActivityRedTeamTests`' `junk-stale` fixture moved from −40 to −120 days for the same
  reason. `TemporalAnomalyTests.HorizonAndSkewBoundaries` now re-derives both edges from
  the policy constants and uses a fresh profile per case, because accepting an edge record
  advances the watermark and would otherwise move the window mid-test.
- **An already-trusted out-of-window record is reported as a duplicate, not a rejection**,
  guarded by a revision check. Without it, replaying a long horizon would report the
  oldest days as `OutsideHorizon` and the flagship exactly-once evidence
  (`M4Region1AcceptanceTests`) would read as refused activity. The revision guard is what
  makes it incapable of minting value: an out-of-window record with a **higher** revision
  is still rejected, so it can never reach the correction path.
- **A deletion marker skips exactly one eligibility rule: the quantity checks.** A marker
  carries no activity by definition and every well-formed marker in this repository has
  `Quantity == 0`; applying `ZeroQuantity` would reject every deletion ever issued and
  silently disable the correction policy — the opposite of "the same eligibility rules".
  Provenance, unit, temporal extent and both window bounds all still apply.
- **This departs from the change's own `design.md` in one place.** The design suggested a
  deletion marker with an empty provider namespace would remain an "ignored" *policy*
  outcome; the change's **spec** says a marker with "a missing provider namespace" is
  "rejected under the corresponding eligibility reason". The spec is normative, so the spec
  wins. Both classifications are equally safe (neither reverses value nor moves the
  watermark) and the spec's is strictly more informative. Recorded here rather than
  diverging silently, per the master plan's rule 11.
- **The batch accounting identity is now asserted in a test** with every counter exercised
  at once: `accepted + rejected + duplicates + corrections + deletionsApplied +
  deletionsIgnored + staleRevisions == total received`. Per-reason counts remain **on the
  result object only**; persisting them is the follow-up diagnostics change and is
  explicitly out of scope here.
- `DateTimeOffset` arithmetic is not saturating, and `MinValue − 45d` throws. `TrustedTime`
  guards both operations against the distance available to the representable limit, so a
  profile whose trusted progress sits at the minimum degrades to "reject everything"
  instead of throwing during validation. (Found by the new tests, not by inspection.)
- **No conversion, clamp, dedup identity, correction policy or save schema changed.** All
  seven byte-reproducible pacing reports are unchanged, which is the strongest available
  evidence that the window change did not perturb simulation behaviour.
- Still **open and not claimed**: real Health Connect / HealthKit backlog-on-reconnect
  behaviour and backfill limits are the M7 adapter lane and remain device-blocked
  (`KI-002`). The reconciliation watermark is still *evidence*, not a driver of scheduled
  query windows; making it a driver is the adapter's job and stays deferred.

---

## D-054 — Secondary resources carry authored caps and a real consumption decision

**Status:** Accepted *(verified at the F-06 pass: 29 new tests — 17 in `SecondaryResourceEconomyTests` (Domain), 12 in `SecondaryResourceEconomyIntegrationTests` (Application); `dotnet test` 351/351 — Domain 122, Infrastructure 48, Application 181 — green; all seven `docs/evidence/m4/pacing-*.txt` reports still reproduce **byte-for-byte**; `verify-architecture.sh` 11/0; save-growth verified; guard suite 25/0)*

**Decision:** Region content gains two pieces of economy contract. (1) A per-resource
**cap** map on `RegionDefinition`, applied by `GameFactory.NewGame`, with
`ContentValidator` rejecting (a) any resource a producer can emit that has no authored cap,
(b) a non-positive cap, and (c) a cap whose derived store capacity is unrepresentable.
`GameStateValidator` reports a balance above its content cap and **never clamps it on
load** — the same fail-closed posture every other invariant takes. (2) An optional
per-resource **requirement** on `ProjectDefinition`, consumed all-or-nothing via
`TryConsume` when the project is *activated*, before any Vitality is streamed. Activation
with an unmet requirement returns a new stable code
`project.resource-requirement-not-met` naming the resource and the shortfall, and consumes
nothing. Auto-advance checks the head of the queue and **stops** rather than promoting a
later project past it, so Vitality keeps banking.

**Authored Region 1 values:** caps **Materials 400 / Knowledge 300**; requirements
**Restore Market Hall 40 Materials** (settlement chain) and **Calibrate Survey Rig 20
Knowledge** (research chain) — two distinct chains, two distinct currencies.

**Why 400/300 and 40/20 — measured, not guessed.** Both caps were run to a 730-day
`longhaul`: Materials settles at exactly 400 and Knowledge at exactly 300, i.e. **both
caps bind**, because production outruns consumption. A cap a real profile never reaches
would be decoration. The requirement sizes were chosen empirically against the committed
pacing evidence: 60 Materials / 40 Knowledge pushed the settlement chain out by up to 6
days and the research chain by up to 9 (high 97→101, pathological 29→38) and drifted
**five of seven** profiles. At **40 / 20 all seven profiles reproduce byte-for-byte**, so
the mechanic is real (a player must wait for production) and the balance cost is zero.

**Vitality is deliberately NOT capped — a documented departure from the task list.**
Task 1.5 asked for a Vitality cap "at or above the maximum balances the committed evidence
produces". Implementing it would have been a **data-loss bug**, not a balance:
`RewardLedger.Apply` *ignores* the value `balances.Add` returns and records the **full**
transaction amount in the ledger, and Vitality has no producer store to park refused units
in. A Vitality cap would therefore silently discard real walking credit — the exact defect
class this campaign exists to remove. The cap requirement is scoped to resources a producer
can emit, and no producer emits Vitality. Recorded here rather than implemented.

**The real defect, restated.** The cap/parking machinery was fully implemented, fully
tested (`ProducerSimulationTests` proved parking and zero-elapsed flush both work), and
**completely unreachable in production**: `SetCap` had zero call sites outside tests, so
`GetCap` always returned `long.MaxValue`, so the `hitResourceCap` branch never fired and
two currencies grew forever with nothing to spend them on. The `ProducerProduced` event
also conflated "the producer's own store filled" with "the resource is at its cap"; the two
are now separate flags, because only the second is relieved by the player spending
something, and only the second needs telling about — otherwise "nothing happened" and
"your output is waiting" look identical.

**Consequences:**
- **No pacing evidence was regenerated.** The change anticipated it, but sizing the
  requirements empirically made it unnecessary. All seven reports are byte-identical,
  which is strong evidence the mechanic was added without perturbing balance.
- `EnsureActiveSlot` now removes the queue entry only after the affordability check, so a
  stalled project is not silently dequeued.
- `ProjectRow.RequirementsAffordable` and `ProducerRow.CapacityLimited` are computed from
  canonical state at read time and are pure projections; the shell renders, the domain
  decides.
- The requirement is a gate the player *chooses* to satisfy. Vitality is not: a Vitality
  shortfall is not an error, it banks until affordable. Keeping those two visibly
  different is deliberate.
- Content validation now requires that a requirement be satisfiable from the region's own
  production within the 400-day design horizon, so a project cannot be authored
  permanently unactivatable. The check uses production *rate*, not store size: parked units
  flush repeatedly, so total deliverable is bounded by rate × horizon.
- `ProducerSimulationTests.Tick_ResourceCapExhausted_ParksSurplus_InBoundedStore` now
  asserts `HitResourceCap` rather than the old conflated `HitCapacity`; the parking and
  flush assertions are unchanged.
- `docs/RISK_REGISTER.md` R-014 and `docs/GAME_SYSTEMS.md` §3/§6 updated. No new
  `KNOWN_ISSUES` entry: nothing open was invalidated.
- **Still open and not claimed:** whether a cap value is the right *number* for a real
  player is a balance judgement no headless test can settle; the seven profiles are
  unchanged, but a cap that binds at day ~300 tells us nothing about how it feels at day
  30. A UX pass on the capped-storage message is the M5 shell's call.

---

## D-055 — The durable return summary rotates per kind, items carry currency, and attention expires

**Status:** Accepted *(verified at the F-05 pass: 13 new tests in `ReturnSummaryRotationTests`; `dotnet test` 364/364 — Domain 122, Infrastructure 48, Application 194 — green; all seven `docs/evidence/m4/pacing-*.txt` reports still reproduce **byte-for-byte**; `verify-architecture.sh` 11/0; save-growth verified; guard suite 25/0)*

**Decision:** `ReturnSummaryComposer` retains items under a **per-kind bound table**
(Transformation 5, ActionableDecision 3, Production 2, Notice 1, Aggregate 1 — summing to
exactly the existing 12-item hard bound, which is unchanged). When a kind is full its own
oldest item is retired, so newer facts of the same kind can appear. A lower-priority item is
**never** promoted above a higher-priority one to make room. `PendingSummaryItemState`
gains an additive `FirstCommittedAtUtc`; items carried over keep their **original** commit
time. `GetHome()` claims attention only while the summary holds at least one **current**
item, where current means committed within `ReturnSummaryReadModel.CurrencyThreshold` =
**7 days**; `HasPendingSummary` stays true regardless, so a historical summary is still
readable.

**Rationale.** The summary only ever grew. Every batch merged new items into the pending
ones, the merged list was sorted by kind, and the first 12 survived; nothing was ever
removed except by an explicit acknowledgement. So for a player who does not tap "dismiss" —
precisely the busy player this product is designed for — the summary saturated within
roughly the first dozen region events, after which every new Transformation item was
dropped in favour of the oldest ones and **no Production, Notice or Aggregate item was ever
shown again**. The player who engaged least got the least useful information, and Home
compounded it by claiming `RequiresAttention` whenever any summary existed.

**The 7-day threshold.** Long enough that a player who opens the app most days still sees
"something is new"; short enough that a summary left unacknowledged for a week stops
shouting. The queue-idle-with-banked-Vitality branch is deliberately **not** gated by
currency, so that signal's semantics are unchanged.

**Consequences — three real defects found and fixed while implementing:**

1. **The sort was never stable.** `Finalize` called `List<T>.Sort` with a kind-only
   comparator and then claimed "insertion order within a kind" in a comment. `List.Sort` is
   not stable, so presentation order within a kind was arbitrary while the code asserted
   otherwise. The ordering is now explicit: kind ordinal, then input index.

2. **An unsound validator rule was removed, not added.** Task 2.4 asked for a check that an
   item's commit timestamp is not after the summary's generation timestamp. That comparison
   **assumes a monotonic clock**, and this repository explicitly does not assume one
   (`ACTIVITY_PIPELINE` §12; the backward-clock handling in D-053). Adding the check made
   three acceptance suites fail closed on a perfectly valid state — booting a save with a
   clock earlier than the save's own generation is a supported, tested scenario. This is
   exactly the "unsound invariant that bricks mature saves" shape the validator exists to
   prevent, so any comparison between two persisted timestamps is now treated as unsound and
   item currency is carried for **presentation only**.

3. **Currency had to be measured against *now*, not against the summary.** The first
   implementation computed `IsCurrent` from the summary's own `GeneratedAtUtc`. A summary
   that is not recomposed keeps its original generation time, so a month-old item reported
   as brand new forever and the attention signal could never expire — the change would have
   been inert. `FromState` now takes the reading instant and `GameSession` passes the
   injected clock. Found by the Home attention test, not by inspection.

- **No schema bump and no migration.** The new item field is additive and an absent value
  decodes to "as current as the summary itself", which is correct: those items genuinely are
  the most recent thing that happened to the player. A pre-D-055 save is never invalidated.
- **No item text, event mapping, acknowledgement operation or canonical progression
  changed.** All seven pacing reports — whose text includes summary output — are
  byte-identical, which is direct evidence that nothing about what is *said* moved.
- **Still open and not claimed:** whether 7 days is the right currency threshold, and
  whether rendering historical items dimmed rather than absent is the right presentation,
  are UX judgements. The mechanical properties are tested; the feel is the M5 shell's call.

---

## D-056 — Every public use case is covered, and an enumeration guard stops the next gap shipping

**Status:** Accepted *(verified at the F-11/F-17 pass: `dotnet test` 383/383 — Domain 122, Infrastructure 48, Application 213 — green; `git diff --stat -- src tools` **empty** (no production file modified by this change); `verify-architecture.sh` 11/0; guard suite 25/0)*

**Decision:** Two of `GameSession`'s public mutating operations — `ReorderQueue` and
`DequeueProject` — had **zero** test references anywhere in the repository. Both mutate and
persist durable queue state, and they are exactly the two operations a drag-to-reorder
queue UI and an "un-queue" button would wire up first. Both are now covered, and a
**reflection-based enumeration guard** fails the build if any public, parameterless,
non-void, non-mutating operation on `GameSession` has no reference anywhere under
`tests/`. `ReturnSummaryComposer.WithNotice` gained direct tests that assert its output as
*content* rather than as a count.

**Rationale.** Writing tests for the two known gaps fixes those two and nothing else. A
gap nobody looks for is a gap that grows, and this repository has now hit the same class of
gap twice — a store-level guarantee no application-layer caller exercised (D-052), and a
mechanism that was tested in isolation but unreachable in the product (D-054). The guard
is the part that generalises. It reads only names and source text: it never **invokes** an
operation, so it cannot mutate durable state or throw for the wrong reason.

**The guard was proven to fail, and its first failure mode was found by doing that.**

| Step | Result |
|---|---|
| Healthy tree | guard green |
| A public parameterless non-void use case added to `GameSession`, untested | **guard FAILED**: `- UntestedPublicUseCaseProbe (HomeReadModel UntestedPublicUseCaseProbe()) — expected a test in tests/WalkGame.Application.Tests/` |
| Probe removed | guard green again |

**The self-reference hole.** The first proof attempt *passed* — vacuously — because the
guard test's own XML doc comment named the probe, and the guard scans the test sources.
A guard that can be satisfied by a comment about the guard is not a guard. The probe's
name was removed from the test file and the proof was re-run, and the transcript is
recorded in this decision log rather than beside the code precisely so that it cannot
reintroduce the hole.

**Two real behaviours pinned that the task list had wrong.** Task 2.3 described
`DequeueProject` on an *Active* project as "entry removed from the queue; status
unchanged". The actual contract is that it is **refused** with `NotQueued`, because
activation has already removed the project from the queue. That is the better contract —
an Active project holds invested Vitality, and refusing the call is what stops that money
being orphaned — so the test pins the real behaviour and the discrepancy is recorded here
rather than "fixed" to match the prose. Task 2.4's "returns to the unallocated bank" is
similarly only true once the queue is idle; while a project is Active the Vitality is
correctly held as that project's progress, and the test asserts that conservation
explicitly (120 invested + 200 more = 320 in, 300 consumed, 20 banked, nothing stranded).

**Consequences:**
- **No production code was modified.** `git diff --stat -- src tools` is empty, which is
  itself the evidence that the tests describe the system as it is rather than reshaping it
  to fit the tests.
- The Region 1 content graph has exactly **one** entry project, so three projects cannot be
  enqueued through the public API without completing a chain. The queue tests therefore seed
  canonical state directly. That is a property of the shipped content, not a workaround
  for a defect, and it is worth knowing before a second chain is authored.
- The guard's exclusion rules are prefix-based (`Set*`, `Enqueue*`, `Dequeue*`, `Activate*`,
  `Reorder*`, `Mark*`, `Advance*`, `Acknowledge*`, plus the boot/ingest entry points) plus
  an explicit, reason-carrying `AllowList`. A consciously untested operation is therefore a
  reviewable edit rather than a silent exemption.
- The durable, reflection-driven no-leak sweep (every read model walked
  automatically) remains the job of `surface-ingestion-rejection-reasons` task 5.1, as that
  change specifies. This change only completed the explicit list, which the D-052 work had
  already extended to the three previously-unwatched surfaces.

---

## D-057 — Dormant surface is recorded, not wished away: the seed, the generic result, and three dead error codes

**Status:** Accepted *(verified at the F-15/F-21 pass: `dotnet test` 386/386 — Domain 124, Infrastructure 48, Application 214 — green; all seven `docs/evidence/m4/pacing-*.txt` reports byte-identical; save-growth verified; `verify-architecture.sh` 11/0; guard suite 25/0)*

**Decision:** Four pieces of surface that read as live but were not are resolved, and one
guard is added so the class cannot regrow. The guiding rule: **dormant surface is
recorded honestly, not wished away, and not justified by inventing a use for it.**

1. **Canonical randomness is kept, and D-017 is corrected.** `DeterministicRng` remains —
   the *policy* is right and the mechanism is genuinely needed by the first canonical
   consumer. What changed is the honesty: D-017 no longer describes a live mechanism, the
   type and the `GameState.Rng` field both state that no consumer exists today, and the
   inert behaviour is pinned by a test. Task 1.4's prohibition was respected: the RNG was
   **not** wired into any progression, content or simulation path. Adding a random
   mechanic to justify existing code would have been inventing a feature to excuse a
   dependency, which is strictly worse than a documented one.

2. **`DomainResult<T>` is removed.** Zero production and tool references; the only uses were
   two lines in its own unit test. It advertised a supported shape for a value-returning
   use case that does not exist. When such a use case arrives it will choose its result
   type against a real requirement, not inherit a guess. Removal (rather than retention)
   was chosen because a type nobody uses is a claim the codebase cannot keep.

3. **`ErrorCodes.NotAvailable`, `.QueueEmpty` and `.InvalidArgument` are removed.** Each was
   declared and returned by nothing, so a reader of `Results.cs` reasonably concluded the
   system distinguished those three conditions somewhere. Before removal it was confirmed
   that **no persisted value, wire format or localisation key keys on any of the three
   strings** — grepped across `docs/`, `scripts/` and the save/JSON paths. The
   `DomainResult.From` test that used two of them as arbitrary codes now uses live codes;
   the behaviour it pins is unchanged.

4. **`ErrorCodeCoverageTests` prevents recurrence.** It reflects over `ErrorCodes` and fails
   if any declared constant is referenced by no production path, reporting **all** unused
   codes in one message so a single run lists everything to fix. An empty, reason-carrying
   `ReservedAllowList` lets a platform adapter land a code ahead of the operation that
   returns it without the guard blocking legitimate in-flight work — a conscious,
   reviewable edit rather than a silent exemption.

**The guards were proven to fail, and doing so is how their own failure modes were found.**

| Guard | Healthy | Broken fixture | Result |
|---|---|---|---|
| `ErrorCodeCoverageTests` | green | an unused code added to `ErrorCodes` | **FAIL**, naming `ScratchProbeNeverReturned ("scratch.never-returned")` |
| `UseCaseCoverageGuardTests` (D-056) | green | an untested public use case added to `GameSession` | **FAIL**, naming the member and the expected test directory |

The D-056 guard's **first** proof attempt passed *vacuously*, because the guard test's own
doc comment named the temporary probe and the guard scans the test sources — a guard
satisfiable by a comment about the guard is not a guard. That self-reference is now
documented in the guard itself, and both proof transcripts are recorded in this log
rather than beside the code, precisely so they cannot reopen the hole.

**Consequences:**
- **No save schema change and no migration.** The removed types and codes were never
  persisted, so no mature-save fixture was needed and no rollback consideration arises.
  This is the direct payoff of confirming persistence-independence *before* removing.
- All seven pacing reports and the save-growth series are unchanged, which is the expected
  result of touching declarations that no code path reads — and is the evidence that the
  removals were genuinely inert rather than subtly load-bearing.
- `SeededProgressionPropertyTests` names its generator `testDataRng` and says in a
  one-line comment that it is property-test INPUT, never canonical game randomness. The
  rename is identifier-only; every assertion is unchanged and the suite is green.
- The seed honesty test drives a 24-day profile through the **real** ingestion pipeline for
  two different seeds and compares canonical state deeply with the RNG field removed, so
  it asserts the player-visible claim rather than one incidental field.
- **Still unclaimed:** whether a seed *should* eventually affect outcomes is a product
  decision for the first mechanic that needs randomness, not a code fix. Nothing here
  forecloses it; it only stops the repository from implying the mechanism is live today.

---

## D-058 — Content validation derives its numeric bounds from the simulation's real arithmetic, and checks itself

**Status:** Accepted *(verified at the F-08 pass: 11 new cases in `ContentValidationTests`; `dotnet test` 397/397 — Domain 135, Infrastructure 48, Application 214 — green; all seven `docs/evidence/m4/pacing-*.txt` reports **byte-identical**; save-growth verified; `verify-architecture.sh` 11/0; guard suite 25/0)*

**Decision:** `ContentValidator.MaxMilliUnitsPerDay` is no longer a hand-picked
`1_000_000_000_000`. It is now **derived** from the production tick's formula by
`ProducerArithmetic` (Domain, next to `ProducerDefinition`), and `MaxProducerInterval`
itself moved into that helper so the simulation and the validator read ONE source.
`ContentValidator.CheckNumericBoundsAgainstSimulation` re-derives the bound on every
validation and reports a named violation if the published constant ever drifts from it.

**The defect, restated.** `OfflineAdvancer.TickProducers` computes:

```
(cappedTicks / TicksPerDay) * rate  +  (cappedTicks % TicksPerDay) * rate / TicksPerDay
```

The sub-day remainder is strictly less than `TimeSpan.TicksPerDay` (8.64e11), so
representability requires `rate ≤ long.MaxValue / (TicksPerDay − 1)` ≈ **1.07e7**. The old
bound of 1e12 was therefore wrong by roughly **five orders of magnitude**: any rate
between ~1.07e7 and 1e12 passed validation and then overflowed in production. C# arithmetic
there is unchecked, so the product **wrapped** rather than threw, a wrapped value drove
`StoredMilliUnits` **negative**, and `GameStateValidator` then failed the save closed on the
next boot. A player's world became unloadable from a number content validation had
explicitly approved. The old comment stated the correct intent — "keep products
representable" — and simply asserted a number that did not achieve it.

**Why a self-check, and why it is a real check.** A bound that merely *looks* right is not a
bound. If someone hand-edits the constant, or the simulation's maximum interval changes,
the two drift silently and validation starts approving content the simulation cannot
represent — which is precisely the failure mode being fixed. The self-check also asserts
**which term binds** (currently the sub-day remainder, by five orders of magnitude), so a
future change to `MaxInterval` that flipped the binding term would be reported as a stale
derivation rather than passing unnoticed.

The check is exercised by a test that passes a deliberately perturbed published bound and
asserts the violation names **both** values. `MaxMilliUnitsPerDay` is `static readonly`
rather than `const` precisely because a compile-time constant cannot reference the
simulation's interval, and hardcoding the number is the failure being removed.

**Consequences:**
- **The production tick formula was not touched.** The seven byte-identical pacing reports
  are the proof, since every profile is driven day-by-day through the real pipeline. Task
  3.3 is satisfied by evidence, not by assertion.
- **No Region 1 value changed.** Shipped rates remain 1200 / 1500 / 2500 milli-units/day —
  three orders of magnitude below the corrected bound, and roughly 2500× *below* the old
  one. This was latent authoring risk, not a live defect, and the new bound does not change
  any existing world's behaviour.
- **Two validator rules are now demonstrably unreachable through the constructors.**
  `ProducerDefinition` already rejected a non-positive capacity and `RegionDefinition` a
  non-positive cap, so `ContentValidator`'s own `capacityUnits <= 0` and cap-positivity
  checks can only fire on a definition that bypasses its constructor. They are retained as
  defence in depth, and the tests now prove the CONSTRUCTOR guard that actually runs rather
  than pretending the validator rule is reachable.
- **Authoring bounds are now documented where a content author will find them**
  (`WORLD_AND_CONTENT.md`), and `TECHNICAL_ARCHITECTURE.md` §23 records that the content
  validator is the release gate for numeric representability, not only for references.
- **Still unclaimed:** whether a producer rate near the corrected bound produces *sensible*
  game behaviour is a balance question no representability test can answer. The bound is
  "cannot overflow", not "is a good number".

---

## D-059 — Saves are gated against the authored content version they were created for, and retired IDs resolve to authored tombstones

**Status:** Accepted *(verified at the F-04 pass: 21 new cases (19 `ContentVersionGateTests` + 2 codec round-trip); `dotnet test` 417/417 — Domain 135, Infrastructure 49, Application 233 — green; all seven pacing reports byte-identical; save-growth verified; `verify-architecture.sh` **12**/0 (rule 6 discovered the new CI-invoked script); `verify-ci-steps.sh` **13 ok / 0 failed / 2 hosted-only SKIP** over 15 steps; guard suite 25/0)*

**Decision:** Three things, none of which is a schema migration.

1. **`GameState.AuthoredContentVersion`** records the content version the profile was
   **created** against, set once by `GameFactory.NewGame` and never rewritten. Additive:
   absence decodes to `null` and is evaluated as the OLDEST supported version, so no
   `SchemaVersions` bump and no `ISaveMigration` were needed.
2. **Boot classifies the save↔content relationship** as `ExactMatch`, `CompatibleOlder`,
   or `NewerThanBuild`, reported on every boot. A save **newer** than the build fails
   closed with a distinct `StartStatus.ContentIncompatible` /
   `DiagnosticsBootOutcome.ContentIncompatible` and is **never rewritten**.
3. **Authored tombstones** (`ContentTombstone`) let a retired identifier stay
   RESOLVABLE instead of becoming an unresolvable reference. Region 1's set is
   deliberately **empty** — the mechanism and its guards are introduced, and nothing is
   retired.

**The two version axes are different things.** `SchemaVersions` is the **payload wire
format**, migrated by the `ISaveMigration` chain. `AuthoredContentVersion` is the
**authored content graph the payload refers to**, which is never migrated. They are
reported independently and a test pins that: a save on a *current* schema but authored
against a *newer* content version is rejected with `ContentIncompatible` and carries
**no** codec failure and **no** migrations. Conflating them would misreport a content
break as a schema problem, and the corrective action — "migrate the payload" — is the
wrong one.

**Why this was urgent.** `RegionDefinition.ContentVersion` existed, was set to 2, and was
read by **nothing**. Canonical state fails CLOSED on unknown content — correct behaviour,
and precisely what makes a content rename dangerous. Rename one project or drop one
discovery, and every existing save fails validation on boot, both generations are
unreadable, and the one-generation backup holds the same invalid content so it fails too.
R-018 listed four mitigations; two were enforced, the tombstone policy did not exist, and
**nothing detected the drift before it destroyed a profile.**

**A tombstone grants NOTHING.** "Resolves" must not mean "still works". A tombstoned
project may not sit Active, a tombstoned producer may not unlock or hold output, and a
tombstoned expedition may not be recorded as completed — each is a distinct violation, and
each is covered. An identifier that is neither live nor tombstoned **still fails closed**,
so tombstones cannot become a general escape hatch for unknown content.

**Consequences:**
- **A content-drift gate now exists and runs in CI.** `scripts/verify-content-compatibility.sh`
  proves the shipped content validates and that mature saves generated at five different
  ages all decode and resolve against it, reporting each save's authored content version.
  It is wired into `ci.yml` deliberately: `verify-ci-steps.sh` executes the workflow
  verbatim (D-049), so a script CI does not call is not a gate. `verify-architecture.sh`
  rule 6 automatically picked it up and raised the architecture count from 11 to 12 by
  checking its exec bit and LF line endings — the rule working as designed on a new script.
- **The gate was proven to fail on a deliberately drifted fixture** (a save referencing
  `proj.renamed-away`), and its diagnostics were IMPROVED as a result: the first proof
  reported only "no verdict", which would have sent a reader hunting for a harness bug
  instead of a content break. It now surfaces the actual validator line.
- `validate` reports `contentVersion=<save>/<build> content=<verdict>` alongside the schema
  version and violation count, and prints the shipped tombstone set; `dump` prints the
  recorded authored content version. Reporting only the schema made a retired-identifier
  failure look like a schema problem.
- `Region1Catalog.Create(IEnumerable<ContentTombstone>?)` was added so tests can prove the
  mechanism against the real content graph. The parameterless overload production uses is
  untouched, and passing no tombstones is exactly the shipped content.
- **Still unmitigated and honestly recorded:** a drift that shipped through an
  *intermediate* build — a profile created against a version that was then retired without
  a tombstone — is detected at boot but cannot be recovered, only refused. And detecting
  drift is not the same as preventing it; the tombstone discipline is a human review
  obligation, which is why the gate exists and why `AGENT_EXECUTION_GUIDE.md` now states
  the rule.

---

## D-060 — The CLI's integrity self-test writes through the key it read, and expected failures carry their own exit code
**Status:** Accepted *(verified at the F-16 pass: `dotnet test` 417/417 green; every exit code confirmed end to end by hand; `verify-ci-steps.sh` green including the simulation-smoke step that runs `validate --selftest`; `verify-architecture.sh` 12/0; guard suite 25/0)*

**Decision:** `IntegritySelfTest` now determines the payload property name actually
present and reassigns through **that same name**, reports the observed and expected decode
status **separately**, and keeps the tamper minimal (one flipped byte of the decoded
payload). Expected verb failures moved from `InvalidOperationException` to
`CliScenarioException`, which carries a documented exit code; exit 4 "unexpected error" is
reserved for a genuine defect in the tool. A user-supplied `--save` whose **parent**
directory does not exist is now a usage error (exit 1), not "no save found" (exit 3).

**The defect.** The self-test read `root?["payloadBase64"] ?? root?["PayloadBase64"]` but
always **assigned** `payloadBase64`. On a PascalCase envelope that added a *second* key
differing only in case, producing a duplicate-key document, which the codec classified as
`MalformedEnvelope` — not `ChecksumMismatch`. A **false release-blocker from the tool whose
entire purpose is to prove integrity.** The failing branch was dead today only because
`SaveJson` always writes camelCase; dead-until-casing-changed is exactly the state a latent
defect sits in.

**Proven, and the proof had to be at the logic level.** A PascalCase envelope cannot reach
the self-test through the CLI: `validate` boots first, boot cannot decode it, and the
successful backup boot rewrites `save.json` in camelCase before the self-test reads it. So
the defect was proven where it actually lives, in the write-back keying:

| implementation | tampered document's keys |
|---|---|
| OLD (always `payloadBase64`) | `SchemaVersion, SavedAtUtc, PayloadSha256Base64, PayloadBase64, payloadBase64` — **five** |
| NEW (through the observed key) | `SchemaVersion, SavedAtUtc, PayloadSha256Base64, PayloadBase64` — **four** |

**The exit-code contract, verified end to end.**

| scenario | outcome | exit |
|---|---|---|
| missing save | `NoSaveFound` | 3 |
| corrupt save, no backup | `SaveUnreadable` (malformed envelope) | 3 |
| payload tampered, checksum left stale | `SaveUnreadable` (integrity check failed) | 3 |
| content tampered, checksum recomputed, invariant violated | `StateInvalid` | 2 |
| unknown verb / missing flag / bad `--save` parent | usage | 1 |
| valid save; `dump`; `validate --selftest` | ok | 0 |

The two tamper rows are the same act with different outcomes, and that is correct: the
checksum is an **integrity** check against corruption, while `GameStateValidator` is the
**invariant** gate. Conflating them would make one of the two exits unreachable.

**One observation recorded rather than absorbed.** A save rewritten wholesale with its
checksum recomputed passes validation — inflating the Vitality balance yields
`violations=0`. That is the **documented threat model**, not a new defect:
`PRIVACY_REVIEW.md` states the save is "plaintext JSON (base64 payload, SHA-256 for
*integrity only*)", and `KI-006` already records the save as plaintext at the application
layer. Adding an authenticity mechanism (a signature or MAC over the payload) is a
security-design change the roadmap does not ask for, so it belongs with the open `KI-006`
product/security decision rather than being smuggled in here.

**Consequences:**
- `StartStatus.ContentIncompatible` (D-059) maps to exit **2** alongside `StateInvalid`:
  both mean "this build cannot safely represent or validate this save".
- Every `StartStatus` now has an explicit switch arm, including the three that return
  early, so a new member is *noticed* rather than silently falling into a generic message.
- `Ack`, `Dump` and `Validate` validate the `--save` parent. `New` deliberately does not
  (it creates the directory), and `scenarios`/`bench` create their own work subdirectories;
  both are confirmed unaffected.
- `--selftest` output now states which key was tampered and what the codec reported, so a
  failure distinguishes "tamper not detected" from "tamper detected but classified as
  something else" — two completely different faults that a bare `false` conflates.

---

## D-061 — A completed region is a conclusion, and continued activity has an authored use

**Status:** accepted (2026-10-08) — closes F-20 / OpenSpec change
`define-post-completion-evergreen-state`.

**The defect.** After the closure milestone, Home reported
`QueueEmptyWithBankedVitality` — "queue a restoration project" — for as long as the player
kept walking, with `RequiresAttention = true` and no completion condition anywhere. The
player who engaged most got the permanent false alarm, and the bank grew without bound
(reproduced: 60,650 Vitality banked with 19/19 projects complete). No test asserted the
Home surface after region completion at all.

**Decisions, and why each is the small one:**

1. **The ending is derived from the completion flag, not from content counts.** `GetHome()`
   suppresses the banked-Vitality reason when `RegionState.IsCompleted` is true and reports
   a new `HomeAttentionReason.RegionComplete` instead. `RequiresAttention` is **false** for
   it: the state is concluded, not outstanding. Deriving "no work is left" by counting the
   roster would have created a second source of truth that could disagree with the flag.
2. **Suppression would have been silence, so it is a classification instead.** `None`
   already means \"nothing has happened yet\"; a finished player needs the shell to be able
   to say *when* the valley was restored, so `HomeReadModel` carries the completion instant.
3. **The sink is authored content, not a mechanic.** A new content kind,
   `PostCompletionProgramDefinition`, holds one repeatable Region 1 program: **Evergreen
   Tending**, 1,000 Vitality per cycle, enabled by the closure milestone and by nothing
   else. It is deliberately **not** a `ProjectDefinition`: it must never make the roster
   read \"19 of 20 complete\", never be queued, and never gate anything.
4. **The validator exemption is written down in one place.** Post-completion programs are
   exempt from entry-reachability — they are reachable only from the closure milestone —
   and remain subject to every other rule. Stated in the validator so the tempting
   \"fix\" for a newly authored program (deleting it because reachability flagged it) is
   visibly the wrong move.
5. **Continued activity is applied, not merely displayed.** `OfflineAdvancer` applies the
   banked balance to the program by integer division: whole cycles complete, the remainder
   stays invested. O(1) regardless of how long the player has been walking, and the
   unspent balance is bounded by one cycle instead of growing forever.
6. **Materials and Knowledge stay declared lifetime scores.** The proposal allowed either a
   sink or an explicit declaration. Both already have authored caps and real pre-completion
   consumers (D-054); adding a second post-completion consumer for them was balance change
   without a balance question, so the region-surface contract says in writing that they are
   scores after completion. **Not** presented as progress toward anything.

**Consequences:**
- The attention signal and the summary's primary next action are both completion-aware, so
  no surface can propose queueing work that no longer exists.
- Existing completed saves are repaired without migration: they already carry
  `IsCompleted`, and the banked progress is absorbed on the next evaluation.
- Save size grows by two additive `long` fields (~65 bytes); the growth gate still passes
  and the pacing reports still reproduce byte-for-byte, because no profile's closure day
  changes.
- **What this does not prove:** that the ending is emotionally satisfying. The mechanical
  properties (no unsatisfiable instruction, a defined sink, truthful surfaces, permanence)
  are tested headlessly; ending quality is a UX judgement recorded as outside this change's
  evidence.

---

## D-062 — Closure is an authored criterion, and the roster is rebalanced so every balanced profile can finish

**Status:** Accepted (2026-10-09) — implements OpenSpec change
`close-inactivity-pacing-shortfall` (F-19, F-13). Content version 2 → 3.

**The defect.** The `inactivity` profile — weekdays 4,000/9,000 alternating, silent weekends,
days 100–144 entirely silent — earned 16,420 Vitality in 400 days against a 19,400-Vitality
roster: short by 2,980, which is exactly why the `forest` and `research` chains never
completed and the region never closed. R-007 and R-013 both realised for the product's own
target player, and the harness reported the shortfall while exiting 0, so it sat open for a
milestone.

**Decision 1 — closure becomes authored content.** `RegionDefinition.Chains` authors the six
restoration chains (replacing a hardcoded table inside the CLI, so one grouping is shared by
the criterion, the harness and the region surface), and
`RegionDefinition.ClosureCriterion` states what "finished" means. `ContentValidator` requires
the criterion to name a real milestone, to demand at least a strong majority of the chains,
and to be reachable from entry content; `GameStateValidator` refuses a completion no route
explains; `profile` reports closure against the criterion and exits 2 naming the unfinished
chains, so a shortfall is a gate failure rather than a report line.

**Decision 2 — the remedy is a rebalance, and the weaker closure routes were rejected by
measurement.** A criterion a weekday-only player can meet is met EARLIER by an engaged player,
because engaged players complete more content sooner. Measured: with a five-of-six-chain
criterion the `high` profile closes on day 81 with the research chain unrestored — the
survey, the observatory and the closure discovery, the part of the valley the ending is
written about — while the weekday-only profile still does not close. So the roster was
rebalanced instead: 19,400 → 15,350 Vitality (−20.9%), entirely inside the M4-authored
10,600, because the five original M1/M3 development-seed definitions are preserved VERBATIM
(the M3 acceptance proof asserts the exact Vitality boundaries those costs produce).

| chain | before | after |
|---|---:|---:|
| trail | 1,400 | 1,000 |
| water | 2,350 | 1,750 |
| settlement | 3,700 | 2,900 |
| wetland | 4,150 | 3,450 |
| forest | 5,000 | 4,550 |
| research | 2,800 | 1,700 |

**Measured effect** (all seven committed reports regenerated, each diff reviewed):

| profile | before | after | documented target |
|---|---:|---:|---|
| high | day 97 | day 77 | ≤120 |
| moderate | day 242 | day 192 | ≤300 |
| irregular | day 139 | day 109 | ≤250 |
| weekend-heavy | day 201 | day 158 | ≤300 |
| pathological | day 29 | day 22 | ≤400 |
| inactivity | **never** | **day 376** | ≤400 |
| low | never | never | exempt — documented long tail |

Every profile that closes still closes with the whole roster and inside its target; no
profile's ending was made skippable to buy another profile a shorter day.

**Decision 3 — `low` stays a long tail, now as an explicit exemption.** A sustained
3,000-steps-per-day walker earns 12,000 Vitality in 400 days and needs roughly 510 days for
the roster. `PacingContract` names the profile exempt BY DECISION and the report prints
`closure criterion: NOT MET`, so the outcome is stated rather than hidden. Ratified against
D-039, which already recorded the long tail as an accepted characteristic of a
movement-proportional economy.

**Decision 4 — the reports state requested AND eligible activity.** `IngestResult.EligibleSteps`
carries the post-clamp total; the report prints both figures plus the achieved ratio, and the
ratio is derivable from them by division.

**Consequences:**

- Saves recorded against content version 2 classify as `CompatibleOlder` (D-059) and keep
  their whole history; nothing is migrated.
- A profile that paid the v2 cost of a project whose v3 cost is lower holds more Vitality than
  the new cost. Accepting that state for a COMPLETED project is deliberate: refusing it would
  fail a mature profile closed on the definitions it was earned against. An incomplete
  project may not hold more than its cost — that would be unspendable.
- `tools/simulation` no longer carries its own chain table; the grouping is authored content.

**F-13 — the wrong conversion figure, recorded as wrong.** ROADMAP §M4 and RISK_REGISTER
R-013 both stated the `pathological` profile showed "≈686 steps/Vitality against the nominal
100". Recomputed from the committed evidence: the profile requests 3,184,633 steps, the
per-record clamp reduces the eligible total to 2,184,633, credited vitality is 21,844 —
**exactly 100.0** eligible steps per Vitality. The clamp discards surplus VOLUME; it does not
reduce the credit rate. The wrong figure came from dividing the pre-clamp total by the
credited vitality while labelling it "activity ingested". Both documents now carry the
recomputation, and the report cannot reproduce the error because it prints both totals.

**Still unproven:** whether the region now finishes too QUICKLY for the players it was tuned
for is a product judgement. The mechanical contract (every balanced profile closes, inside
its documented target, with the whole valley) is tested; the feel is not.

---

## D-063 — The reason activity was not credited is durable, and the source's health is classified from evidence

**Status:** Accepted (2026-10-09) — implements OpenSpec change
`surface-ingestion-rejection-reasons` (F-14, F-17).

**The defect.** `IngestResult.RejectionCounts` — a correct per-reason breakdown — was
produced on every batch and then thrown away with the result object. The only durable
evidence was `LastIngestionOutcome`, whose row carried the AGGREGATE rejected count, so
`OutsideHorizon` (the player's entire backlog was lost) and `ZeroQuantity` (a source returned
a zero record, entirely normal) looked identical to the only person who could act on it.
Beside it, `IngestionCheckpointUtc` was durable evidence the pipeline recorded and then used
only to print an age in whole days, which floors to zero — so "polled three hours ago" and
"not polled in three weeks" both read 0.

**Decisions:**

1. **Ordinal-keyed breakdown.** `IngestionOutcomeState.RejectionCountsByReason` maps the
   ORDINAL of `ActivityValidationStatus` to a count. Member names would couple the durable
   shape to a rename, and provider text is forbidden outright; an ordinal is stable, small,
   and maps back to a classified status on read. An unrecognised name cannot vanish: the
   count is kept so the breakdown still sums to the recorded total.
2. **`null` and empty are different facts.** A save written before the field decodes to
   `null` ("not recorded"); a clean batch records an EMPTY map ("ran, rejected nothing").
   Collapsing them would let an older build's silence masquerade as a clean batch.
3. **The dispositions the batch already computed are now durable** — deletion markers
   ignored, stale revisions ignored, and quantity-clamped records. Computing evidence and
   discarding it is the defect class this closes.
4. **A failed fetch preserves the previous batch's facts.** A failed fetch is not evidence
   that what was earned was never earned, so the row keeps its prior counters and credited
   progress and adds `LastFetchAttemptedUtc` without a matching success.
5. **Source health is classified, and the classification fails closed.**
   `ReconciliationState` (never attempted / current / stale / failed-since-last-success) is
   derived from durable timestamps only. The threshold is 3 days, published in
   ACTIVITY_PIPELINE §14. When the injected clock reads behind the newest durable evidence
   the state is `Stale`, never a reassuring `Current`.
6. **The leak sweep enumerates surfaces by reflection.** The hand-maintained list had six
   surfaces and missed three; the sweep now walks every public parameterless session member
   that returns a read model, asserts all ten known surfaces are present, and allow-lists the
   two support-only surfaces where a bounded, gated adapter string may appear. It is proven
   to fail against a deliberately leaking fixture before it ships.

**Consequences:**

- `GameStateValidator` checks the new durable fields: counters non-negative, the breakdown
  summing to the recorded rejection total, and accepted not exceeding received. Each violation
  names the offending record.
- **Deliberately NOT validated:** `VitalityCredited` non-negativity. It is a NET APPLIED
  delta, and a correction batch legitimately claws value back — a non-negativity rule there
  would fail closed on a valid negative correction. Recorded because the rule was written,
  observed to brick `SessionIngestionTests.CorrectionDown_...`, and removed for that reason.
- Save growth is unaffected: the breakdown is one entry per eligibility reason, not per record.
  No schema bump, no migration.

**Still unverified:** the runtime/device lanes (KI-002) — a support engineer cannot yet SEE
this projection in a shell that exists.

---

## D-064 — The coverage floor, the dependency gate and the release lane are gates, not aspirations

**Status:** Accepted (2026-10-09) — implements OpenSpec change
`extend-ci-and-release-automation` (F-18).

**The defect was coverage, but the class is "a claim nothing enforces".**
`coverlet.collector` was referenced by all three test projects and invoked by no command in the
repository; there was no dependency advisory check, no SBOM/inventory, no Dependabot config, no
tag-triggered release lane, and no tooling for the release evidence package
(TESTING_AND_RELEASE §18), which was assembled by hand. The test suite that found two P0
data-loss defects had no aggregate measurement behind it.

**Decisions:**

1. **The floor is deliberately LOW, and its value is measured, not aspirational.** The measured
   aggregate is 59.51% (Domain 88.63%, Application 71.20%, Infrastructure 25.58% — the
   infrastructure layer's low rate is honest and is not a target this change sets). The floor is
   **55%**: comfortably under the measurement so tooling or instrumentation drift cannot fail the
   gate, and far above "the suites stopped running", which is the failure this catches. A quality
   target belongs in a reviewed change, not in a CI constant.
2. **Every gate is a `run:` step in `ci.yml`.** That is the D-049 property that makes
   `scripts/verify-ci-steps.sh` able to execute the whole gate locally; a gate in a script CI
   never calls is not a gate. Both new scripts are therefore reached only from workflow steps.
3. **The dependency gate enforces the runtime-surface CLAIM, not just the advisories.**
   `scripts/verify-dependency-health.sh` fails when `dotnet list package --vulnerable` reports an
   advisory AND when the shipped projects resolve anything other than exactly one third-party
   package — because PRIVACY_REVIEW §2 documents exactly that, and a documented property nothing
   checks is a property that drifts.
4. **No full SBOM, and the reason is recorded.** A CycloneDX document needs a new third-party
   tool, and this change's non-goals forbid adding dependencies for tooling's sake. The gate emits
   the resolved inventory as machine-readable JSON instead, which is the smallest honest
   substitute: it is a real artifact, produced on every run.
5. **No new analyzer, declined with a reason.** The compiler plus the xUnit analyzers are
   enforced at warnings-as-errors, the runtime surface is one first-party package whose input
   path is a typed DTO validated on load, and adopting a full CA rule set would produce a large
   churn of findings without new information about this codebase. Revisit if the save path grows
   an untrusted surface.
6. **The release lane is thin and never re-judges green.** `release.yml` is tag-triggered,
   requires a successful `ci` run for the tagged SHA, publishes the CLI, and assembles the
   evidence package by script. It does not re-run the CI body, so the two cannot disagree about
   what "green" means. Dependabot gives the four test-side packages an update path.
7. **`docs/RELEASE_CHECKLIST.md` is a committed, checkable document.** §18's package contents are
   now rows with evidence, and the claims that cannot be established locally are rows labelled
   UNVERIFIED with the reason — a package that quietly drops its open blockers is worse than one
   that lists them.

**Consequences:** `verify-ci-steps.sh` is now wired into ONBOARDING's baseline verification, so
the gate runs by default rather than by recall (the KI-008 short-lived-shell hazard applies: run
it detached for long sessions).

**Still unverified:** hosted CI (KI-001) and every runtime lane (KI-002). The new workflows have
never executed on GitHub; they are validated by executing the same step bodies locally through
the existing reproducer.

## D-065 — The glance is a read-time sentence projection, not a second attention policy

**Status:** Accepted (2026-10-09) — implements OpenSpec change `define-headless-glance-language`.

**The gap.** The headless core could already answer UX_DESIGN §3's three glance questions as
facts, and nothing turned those facts into the sentences a player would read. The simulation
CLI printed `attention=QueueEmptyWithBankedVitality(True)` and project ids;
`ReturnSummaryComposer`'s `LandmarkStageReached` arm stored a landmark advance as
`"Old Trailhead has reached Stabilized."`. Unity screens remain externally blocked, so that
gap could not be closed by a pixel shell.

**Decisions:**

1. **Project in Application, from existing reads.** `GlanceReadModel` + `GlanceProjector` sit
   beside the other read models. `GameSession.GetGlance()` gathers `GetHome()`,
   `GetPendingReturnSummary()`, `GetOnboarding()`, the content graph and an activity snapshot,
   then calls the pure projector. It is parameterless and total: a session composed without a
   connection port gets the source-unavailable sentence instead of a throw, so the CLI can read
   a glance on any save. Domain stays free of player copy.
2. **Attention is consumed, never re-derived.** The projector switches on
   `HomeAttentionReason` and copies `RequiresAttention`. The five sentences and the one-action
   rule are pinned per reason; a completed region names the region and offers no queue action;
   an empty queue with banked progress before completion still demands a choice.
3. **The phrase table is closed and shared.** `GlancePhrases.FormatLandmark` is the ONE form
   used by both the glance and newly composed summary lines, so the two can never disagree
   about the same milestone. Alternative considered and rejected: parsing stored text loosely
   at read time — it would drift silently as composers change.
4. **Historical summary text is NOT migrated.** A stored `"... has reached {Stage}."` line is
   re-rendered exactly at read time and never written back; a line that would echo a stage
   identifier and is not that exact form falls back to an opaque lead instead. This is what
   keeps the `Old Trailhead has reached Stabilized.` round-trip fixture true without a
   migration, and it means a shell may still show the old form for a rotated historical item —
   an accepted, documented cost of not rewriting a player's data.
5. **Newly composed lines change, and the evidence follows.** Only the `LandmarkStageReached`
   arm changed form. Pacing evidence and content version are untouched; the reports contain no
   summary text, which is why they still reproduce byte-for-byte.
6. **The inspection surface is a verb that reads, and it writes exactly what every other verb
   already wrote.** `glance` calls the shared boot flow (`Boot` → `Continue` → `FinishBoot`),
   which rolls the offline systems forward and persists before presenting — the behaviour
   `ack`, `dump` and `validate` already had. **The projection itself writes nothing**: it is a
   pure function of its inputs, it never saves, acknowledges, ingests or credits, and a second
   read is identical. This is pinned by test, not by this paragraph.

   Stated plainly, because documentation that describes behaviour that is not true is the
   thing this repository forbids: **running the `glance` verb against a save does persist that
   save** (an advanced `lastAdvanced`, plus the boot-composed summary), so the save bytes after
   a wall-clock run are not the bytes from before it. `--at` pins the clock so an evidence run
   is reproducible, but it does not make the boot path non-persisting. A verb with a
   genuinely read-only boot would need a new application entry point, which this change's
   non-goals exclude; that is a follow-up, not a silent shortfall.

**Consequences:** `UseCaseCoverageGuardTests` and
`EveryReadModelSurface_IsSweptByReflection_AndOnlySupportSurfacesMayCarryAdapterDetail` now
include `GetGlance`, so the surface is aligned the moment it is added rather than by recall.
The M5-H1 evidence labels stay **IMPLEMENTED / AUTOMATED VERIFIED**.

**Known residual, deliberately out of scope (same class, one layer down):** `GetHome()` itself
still reports `RequiresAttention = true` with reason `PendingReturnSummary` on a save whose
region is complete and whose summary still holds a current item. The GLANCE now outranks that
(state: completion sentence, no action, attention not required), but a shell that binds
`HomeReadModel` directly instead of `GlanceReadModel` still sees the alarm. This decision
corrects the glance layer only, because the glance is what this change specifies; changing
`HomeReadModel`'s attention precedence would alter the attention contract that
`M5H1ShellAcceptanceTests` and the attention-reason suite already pin, and that is a separate
change that must be scoped deliberately rather than smuggled into a copy change.

**Still unverified:** every runtime aspect — rendering these sentences in Unity, reduced-motion
behavior, accessibility semantics, and any device evidence remains UNVERIFIED. This change does
not complete M5 as a roadmap milestone.

---

## D-066 — Bounded ledger retirement: watermark-relative, identity-preserving, amortized

**Status:** Accepted (2026-10-16). Implements OpenSpec change `compact-activity-and-reward-ledgers` (F-07).

**Context.** The processed-record ledger and the reward ledger grew one row per ingested record forever and were re-serialized on every commit (day 730: 395,716 B, linear, ~540 B/day).

**Decision.**
- **Retention rule:** a processed-record row is retired when its record end (`RecordEndUtc`, stored from this change on; legacy rows fall back to `ProcessedAtUtc`) is strictly before the acceptance window's lower bound under post-batch trusted progress, i.e. `TrustedWindowFloor` (45 days) behind trusted progress. Anything retired is already outside what the pipeline would accept.
- **Batch size:** at most `ActivityValidationPolicy.RetirementBatchSize` = 256 rows per structure per ingestion commit, run inside the same atomic commit. Selection is deterministic: ordered by `ProcessedAtUtc`, then ordinal identity key.
- **Identity kept forever:** retired identities go into `ProcessedRecords.RetiredIdentities` (O(1) lookup through an in-memory set). A retired identity is permanently non-creditable. Re-delivery, correction or deletion of a retired record credits nothing, reverses nothing, and is counted as a duplicate plus the sub-classification `LastIngestionOutcome.RetiredRecordsIgnored`. It is counted as a duplicate as well so that the pinned exactly-once acceptance evidence (`DuplicatesIgnored`) is unchanged.
- **Maintained aggregates:** both ledgers keep a durable `MaintainedVitalityCredited` (O(1) `TotalVitalityCredited`) plus `RetiredVitalityCredited`/`RetiredCount`. The validator requires each maintained total to equal retained rows + retired total, and requires retired and retained identities to be disjoint. A null maintained total (pre-D-066 save) is hydrated from recomputation on first mutation, so no migration is needed.
- **Ledger coupling (review point):** the reward ledger retires activity-derived rows (reason prefix `ingest`) from its front whose commit instant is before the **same** cutoff. Non-activity rows are never retired and block further retirement, so history is never reordered. This is a new coupling between two histories that were previously independent. `ProcessedRecordCredited <= LedgerTotal` holds trivially because neither maintained total changes on retirement.

**Measured.** Day 365: 58,540 B; day 730: 80,458 B. Second-year growth is 47% of first-year growth; the remainder is the identity set, about 60 B/day. Retained detail plateaus at about 46 rows. `scripts/verify-save-growth.sh` gates the ratio at ≤ 60%.

**Index cost.** The reward-ledger index is built once per load over the retained window only (bounded, independent of lifetime history; `LastIndexBuildSize` test). It stays warm across commits and retirement within a session (`IndexBuildCount` test). The retired-identity set still grows; a compact probabilistic structure stays a Region-2 decision.

---

## D-067 — At-rest confidentiality relies on OS device encryption (KI-006 option b)

**Status:** Accepted (2026-10-16, product owner decision). Closes KI-006 / PRIVACY_REVIEW P-1.

**Context.** The save, including its backup generation, holds health-adjacent data: timestamped walking counts and provider record identifiers (detail kept only for the ~45-day window since D-066; older records are reduced to identity strings). It is plaintext at the application layer (base64 payload; SHA-256 provides integrity only). Three options were open: (a) app-layer encryption, (b) rely on OS encryption as a documented system requirement, (c) accept the risk.

**Decision.** Option (b). No app-layer encryption. At-rest confidentiality is delegated to the operating system, and this is a **system requirement**:
- **Android:** file-based encryption (FBE). FBE is mandatory for devices launching with Android 10+ (and full-disk encryption or FBE for Android 6–9 launches). The save lives in app-private internal storage (credential-encrypted storage by default), never on external/shared storage.
- **iOS:** Data Protection. The save uses the default protection class (`NSFileProtectionCompleteUntilFirstUserAuthentication`) or stronger, and the app never lowers it to `NSFileProtectionNone`.
- **Backup generation:** treated identically. The atomic store keeps it in the same host-injected app-private directory as the primary, so it inherits exactly the same OS protection. It must never be written to a different, shared or less-protected location (no `/tmp`, external storage, or exported files).

**Rationale.** Platform encryption is hardware-backed, mandatory on the supported device classes, and protects every app file uniformly. App-layer encryption would add key management (Keystore/Keychain), new failure modes (an unreadable save after key loss or restore, which is directly against the product's "never lose the world" invariant), and testing burden, but no meaningful protection against an attacker who already holds an unlocked device. The data is minimized (no names, location, or account) and bounded (D-066).

**Consequences.**
- The runtime/M7 lane must verify the actual save path (Unity `persistentDataPath` resolves to app-private internal storage) and the iOS protection class on device. Until then this is a **requirement**, not verified behavior.
- OS cloud/device backup (Android Auto Backup, iCloud/iTunes backup) copies the save under the platform's own backup encryption. Whether to exclude the save from cloud backup is an M7 decision; this decision does not change it.
- Rooted/jailbroken devices and unlocked-device access are out of scope (accepted, documented).
- Revisit if a backend/sync, exports, or sharing of save files is ever introduced, or if a target platform without mandatory encryption is added.

## D-068 — Android Health Connect adapter: framework API (Android 14+), string bridge, fail-closed

**Status:** Accepted (2026-10-16, executor campaign M7-A; product owner may revisit). Partially resolves the open item "Exact Android Health Connect SDK/API integration strategy" (M7).

**Decision.**
- **API:** the Android 14+ framework `android.health.connect` API (`HealthConnectManager.readRecords`, `StepsRecord`), called from a small Java bridge in `Assets/Plugins/Android/WalkGameHealth.androidlib`. No Jetpack `connect-client`, no Kotlin, no Maven dependencies. Devices below API 34 report `unsupported`. Supporting Android 9–13 through the Jetpack client and the Health Connect app is deferred to a later decision.
- **Seam:** `IHealthConnectBridge` (Infrastructure) is a plain-string protocol. `HealthConnectActivityAdapter` implements both `IActivityRecordSource` (D-034) and `IActivityConnectionPort` (D-043), so every mapping rule is headless-testable. The Unity `AndroidHealthConnectBridge` is a thin JNI forwarder, constructed only by `ShellBootstrap` on an Android player.
- **Records:** provider namespace `android.health-connect`. `SourceRecordId` is the Health Connect record id. `Revision` is lastModified seconds since 2020-01-01, so an edited record supersedes its earlier version. Only records lying entirely inside the window are returned, sorted deterministically. Quantity and timestamp validity stay with the trust pipeline.
- **Fail-closed:** a fetch is whole or nothing. Missing permission throws `UnauthorizedAccessException`; provider missing or malformed bridge output throws `InvalidOperationException`; timeouts and I/O throw `IOException`, all recorded durably by `IngestFromSource` (D-063). Unknown permission strings never become a grant. Paging stops at 50 pages and refuses a partial window.
- **Permission states:** `granted`, `not_requested`, and `denied` (requested, never granted). `revoked` means once granted, now withdrawn, tracked in app-private SharedPreferences. Requested with `Activity.requestPermissions(READ_STEPS)`. The manifest declares `READ_STEPS` and the required `VIEW_PERMISSION_USAGE` / `HEALTH_PERMISSIONS` rationale activity.
- **Sync:** on boot and on resume, the shell re-reads the last 30 days when permission is granted. Replays are exactly-once no-ops.
- **Projector precedence change:** `Unsupported` availability with `NotRequested` permission now projects `SourceUnavailable` (no action), not `PermissionNeeded` (Connect). Asking for a connection that cannot exist was a dead end. An explicit denial or revocation is still reported as such.

**Not covered (open):** deletions through the Health Connect changes API (`IsDeletion` records); background reads; Android 9–13; device behaviour. All device behaviour is **UNVERIFIED** (KI-002).

## D-069 — Health Connect deletions use the existing deletion path, re-emitted idempotently

**Status:** Accepted (2026-10-16, executor campaign M7-B). Closes KI-016(a) at the contract level.

**Context.** Health Connect's change log reports a deleted record only as an id and a deletion time. The existing deletion path (`ProcessDeletion`) needs the record's identity, an interval that passes validation, and a revision above the credited one.

**Decision.**
- The Java bridge keeps an app-private index (`noBackupFilesDir/wg_hc_index.tsv`) of id → start, end, lastModified for every record it returned, pruned after 45 days. On each read it consumes the `StepsRecord` change log. A deleted id that is in the index moves to a "deleted" set. The index is written **before** the next change token is stored, so a crash can only replay a deletion, never lose one.
- Every read re-emits each deleted entry as `del \t id \t start \t end \t lastModified \t deletedAt`. The adapter maps it to a marker with `IsDeletion = true`, quantity 0, the ORIGINAL interval and identity, and `Revision = max(rev(deletedAt), rev(lastModified) + 1)`.
- **Exactly-once comes from the pipeline, not the bridge.** The first marker reverses the credit (clamped at the balance, as for every deletion). Each re-delivery has the same revision and is a stale-revision no-op, including after a restart or a failed save. A deletion of a record never credited is ignored.

**Consequences / open:** if a change token expires (Health Connect keeps change logs for a limited time), tracking restarts and deletions inside the gap are missed (`changesGap` flag stored; KI-016). Device behaviour is UNVERIFIED (KI-002).

## D-070 — Balance cadence: "visible change" gaps are measured per profile (targets adopted 2026-10-16, see D-071)

**Status:** Accepted as a measurement (2026-10-16, executor campaign); the targets are **provisional, not ratified**. They await the product owner.

**Context.** The D-062 pacing contract proves each profile can *finish* Region 1. It says nothing about the felt experience: how long a player walks before they *see* anything change. For a low-attention game that is the main quitting risk.

**Decision.**
- A **visible change** is a landmark stage advance, a restoration finishing, or a discovery unlocking: things the Region screen and the return summary show. Vitality totals alone do not count. `VisibleChangeTracker` / `CadenceReport` (Application/Pacing) compute it from read models only. The CLI prints it with `profile --cadence`; the flag is opt-in, so the committed pacing evidence still reproduces byte-for-byte.
- **Measured baseline** (`docs/evidence/balance/cadence-2026-10-16.txt`), longest gap without change:
  - high 20 days
  - irregular 29
  - moderate 50
  - weekend-heavy 42
  - low 78
  - inactivity 86 (includes its 45-day absence)
- **Regression guards** (`VisibleChangeCadenceTests`): high ≤ 21, irregular ≤ 30, low ≤ 78. They stop the cadence getting worse; they are not targets.
- **Proposed targets for ratification:** longest gap ≤ 30 days for every active profile; first change within 10 days, even for low. Moderate, weekend-heavy and low currently miss the 30-day target (KI-017).

**Not done here:** changing costs or the conversion rate. Those are ratified numbers (D-062) and need an owner decision. Candidate levers, which keep the D-062 closure numbers intact: split the large late-chain projects into smaller visible stages, or add intermediate landmark stages or discoveries to the woodland/wetland chains.



## D-071 — Visible build stages on the long seed projects; D-070 cadence targets adopted (2026-10-16)

**Authority:** Michael delegated gameplay/design decisions on 2026-10-16. A check of D-062 and AGENTS.md found no "requires owner ratification" marker on cadence numbers; "ratified" there covers only the per-profile closure-day bounds. So the D-070 targets are adopted: **no gap over 30 days without a visible change, and a first change within 10 days**, for moderate, weekend-heavy and low (low is best effort, D-039).

**Lever:** the gaps came from three single big projects: BuildWorkshop 1500, WetlandDrainage 2200 and CanopyWalkway 4000. All three are M1/M3 seed definitions that D-062 keeps VERBATIM (the M3 acceptance proof asserts their costs). So instead of splitting them into new ids, which would need a save migration and break the seed contract, each one authors `VisibleBuildStages` (2, 3 and 5 respectively, so each stage is at most 800 Vitality). Stage k of N is reached when k/N of the cost is invested. The Region picture draws a build site whose posts rise per stage, and the alt text names the stage. `VisibleChangeTracker` counts a stage advance as a visible change.

**Compatibility:** content version 3 → 4. This is purely additive presentation data derived from `VitalityInvested`: ids, costs, prerequisites and state are unchanged, so no content upgrade is needed. A v3 save classifies as CompatibleOlder (D-059) and immediately shows its in-progress stage. `verify-content-compatibility.sh` and `verify-pacing-evidence.sh` stay green, and closure days are unchanged for every profile.

**Measured (profile --cadence, longest gap / first change):** moderate 50→10 / day 4; weekend-heavy 42→10 / day 3; low 78→27 / day 10; high 20→4; irregular 29→7. Closure days: high 77, irregular 109, moderate 192, weekend-heavy 158, inactivity 376, pathological 22, all within D-062. Inactivity's 60-day gap is its authored 45-day silence and is out of scope. Gate: `VisibleChangeCadenceTests` (≤30 / ≤10 for moderate, weekend-heavy and low).


## D-072 — M8 Lifecycle is proven headlessly at process boundaries (2026-10-16)

The device-independent half of the M8 Lifecycle campaign is modelled as process boundaries, each a fresh `GameSession` over one save directory. It covers repeated resume with overlapping re-fetch, process death with torn temp files, interrupted boots, and permission revoked and re-granted outside the app. The guarded invariant is canonical progress (lifetime credit, completed projects, exactly-once ingestion), not save bytes, because boots legitimately commit bookkeeping. Real OS kill, reboot and on-device revocation stay UNVERIFIED (KI-002). Evidence: `docs/evidence/m8-h1/LIFECYCLE_HEADLESS.md`.


## D-073 — Accessibility uses the OS: font scale and Unity's screen-reader bridge (2026-10-16)

Large text follows the OS font size (`AccessibilitySettings.fontScale`) rather than an in-game setting, clamped to 1–2× so text never drops below the base sizes. Layout must wrap rather than clip. Screen-reader support is an engine-free `AccessibleOutline` (reading order: title, problem/notice, picture description, text, then actions) published through Unity's `AssistiveSupport.activeHierarchy` only while a screen reader is on. Button nodes invoke the same action ids as taps. Empty states are the screen's own explanatory line at body size; no generic filler. The headless half is Unity-editor-tested; TalkBack and hardware font scaling are UNVERIFIED (KI-002). Evidence: `docs/evidence/m8-h1/UX_ACCESSIBILITY_HEADLESS.md`.


## D-074 — M8 performance measured in the editor; the shell reuses its ScrollView (2026-10-16)

Measured in the Unity editor, not on a device: a mature 365-day restored save, every screen, and 3,000 repeated screen enters/exits in a live UIDocument panel. Wall-times are logged, never gated; structural budgets (≤ 250 elements per screen, ≤ 86 picture shapes, no element stacking, < 1 MB live-panel growth from 750 to 3,000 renders) are asserted. The campaign found that per-render ScrollView creation retained about 233 B each inside UI Toolkit. `ShellView` now reuses one ScrollView per root, which cut growth from about 470 to about 116 B/render and halved render time. The residual is KI-018. Evidence: `docs/evidence/m8-h1/PERFORMANCE_EDITOR.md`.


## D-075 — Release qualification starts from a clean clone; the evidence package verifies, never copies (2026-10-16)

`scripts/validate-clean-clone.sh` clones one exact commit from the remote into a new temp directory (`--no-local`), with no shared build output, Unity Library or hooks. It then runs the identity guard, every CI `run:` step verbatim (`verify-ci-steps.sh`, D-049), and, with `--with-unity`, EditMode, PlayMode and the arm64 APK build from a cold Library. It writes `CLEAN_CLONE.md`, one PASS/FAIL row per step, with counts parsed from logs. `assemble-release-evidence.sh --clean-clone` accepts that report only if it says PASS and names the same commit. It also reads hosted CI for the commit with `gh run list` rather than asserting it. The stale KI-001/KI-002 wording is replaced: editor evidence is labelled as such, and device items live only in the Final Device and Release Qualification Checklist (H–Q), which cannot be ticked from headless or editor evidence.


## D-076 — A restoration storyline: milestone-gated, ordered, derived chapters (2026-10-16)

Michael's campaign brief asks for narrative progression tied to walking. **Decision:** Region 1 gets authored chapters (nine in the first draft, ten after the A2 amendment below): a prologue, restoration chapters ending at the closure survey, and an epilogue after the first evergreen tending cycle. Each unlocks only from restoration milestones (completed projects or care cycles), never from raw steps, streaks or time. Chapters are cumulative, so the story always reads in order, and they never lock again. Unlock state is **derived** from completion state that saves already carry (`StoryProgress.Evaluate`), so there is no save schema change or migration. Content goes 4 → 5 as an additive change (CompatibleOlder). New chapters become "New chapter: …" Transformation items in the return summary (existing kind and bound) and live in a Story journal screen. Locked chapters show only a title and a gentle "restore X to continue" hint, with no guilt language. Rejected alternatives:
- persisted chapter state: needs a migration and a new tamper surface;
- step-count gates: punish low walkers and contradict "restoration, not numbers";
- unordered fragments: that is already what discoveries are.

Cadence bounds come from measurement and are recorded below. OpenSpec: `openspec/changes/add-restoration-storyline/`.

**Amendment (A2, 2026-10-16), from measurement:** the first nine-chapter draft unlocked "The Marsh Breathes" and "Lights in the Square" on the same day for every profile, because settlement power completes before wetland drainage. The draft also left a 72-day woodland gap (moderate). The authored story is now **ten** chapters: Quiet Valley (free), Way In (trailhead), Water Remembers (river intake), Hands at the Bench (workshop), Lights in the Square (settlement power), The Marsh Breathes (wetland drainage), Under the Canopy (woodland understory), Above the Trees (canopy walkway), Every Mile Mapped (valley survey = closure), and The Long Watch (one care cycle).

*Mechanism.* `OfflineAdvancer.AllocateVitality` is the single path through which every project completion and care cycle happens. It compares the derived unlocked count before and after and emits `StoryChapterUnlocked`, which the composer turns into a "New chapter: <title>." item. The summary moment therefore needs no persisted state either.

*Cadence* is measured through the real ingestion pipeline by `StoryCadenceTests`, which replaces a separate `profile --story` CLI. Unlock days for chapters 0–9:

| Profile | Unlock days (chapters 0–9) |
|---|---|
| high | 0,2,9,22,29,40,49,69,77,82 |
| irregular | 0,2,13,30,41,57,69,98,109,117 |
| moderate | 0,4,23,54,71,99,121,171,192,204 |
| weekend-heavy | 0,6,21,48,62,84,104,146,161,174 |
| inactivity, with a 45-day absence | 0,5,38,92,169,215,254,340,376,397 |
| low, the documented long tail | 0,10,60,142,189,262,322, then unread by day 400 |

*Gates.* The prologue is available at day 0. The first milestone chapter lands by day 10. No two chapters unlock on the same day. The maximum gap between chapters is 25 (high), 35 (irregular), 55 (moderate), 50 (weekend-heavy) and 90 (inactivity and low). Every engaged profile reads the whole story within 400 days.

These days come from the test harness's first-available queue policy; a player who orders projects differently simply reads later chapters together when a cumulative gate clears.


## D-077 — An expired Health Connect change token is recovered by re-reading the indexed span (2026-10-16)

**Context.** KI-016(a) residual (D-069): Health Connect keeps change logs only for a limited time. Once the token expires, deletions made during the gap never appear in any log, so their credit would stand.

**Decision.** On `ERROR_INVALID_ARGUMENT` from `getChangeLogs`, the bridge recovers in this order:
1. It takes a fresh token first, so later deletions land in the new log.
2. It re-reads every `StepsRecord` in the full span its D-069 index covers (minimum start to maximum end).
3. `GapReconciler` (pure Java, no `android.*`) moves every indexed record that lies fully inside that span but was not returned to the deleted set. Each keeps its original interval and lastModified, with `deletedAt = now`.
4. The index is saved, and only then is the new token stored.

Any failure throws before step 4, so the next read retries the whole recovery. A partial re-read (more than `MAX_PAGES` pages) is refused rather than read as "absent means deleted". The resulting markers use the existing deletion path, so exactly-once still comes from the pipeline's stale-revision rule. A record already deleted through the change log keeps its original deletion time.

**Evidence.**
- `tools/android/test-bridge-logic.sh` runs 10 headless checks and is now a CI step.
- The arm64 APK compiles the bridge (Unity-editor build).
- Device behaviour is UNVERIFIED (KI-002).

**Rejected alternatives:**
- Ignoring the gap: it leaves credit for deleted walks.
- Re-reading every window ever ingested: it is unbounded, and the 45-day index retention already bounds what the app can still reverse.

**Security review addendum (same PR).** Hostile bridge output was driven through the real pipeline (`HealthConnectBridgeTamperTests`, 18 cases). The pipeline already:
- clamps absurd counts to the per-record ceiling (2,500 Vitality);
- rejects negative, inverted and zero-length records;
- credits a repeated id once;
- fails closed on unknown headers or a BOM;
- keeps every save reloadable.

One gap was fixed: record ids were persisted unbounded. A 100,000-character id or one with control characters went into the ledger and diagnostics. Ids are now limited to 128 characters with no control or lone-surrogate characters, and anything else is a malformed line (fail closed). The probe also found KI-019 (per-record rounding loses credit for small records).

## D-078 — Save tampering: fail closed on inconsistency, accept consistent self-edits (2026-10-16)

**Context.** The save checksum is an unkeyed SHA-256, so anyone with file access can rewrite a save and re-hash it. A key on the device would not change that, because it can be extracted. The game is offline and single-player, so a "cheat" only affects the person who made it. The real risks are crashes, bricked saves, unbounded data reaching the UI, and degenerate state.

**Decision.** Checksum-valid but tampered payloads are tested through the real boot path (`SaveTamperTests`, 14 cases):
- **One tampered generation** recovers from the backup.
- **Both generations tampered** fail closed with a typed status (`StateInvalid`, `SaveUnreadable` or `ContentIncompatible`), and nothing throws.
- **Two validator rules were added:** RNG state must not be all zero (xoshiro would emit zeros forever), and a return-summary item's text must be at most 400 characters.
- **Consistent self-edits still load by design.** Examples are a huge balance or unknown extra fields. Arithmetic is overflow-guarded (`ResourceWallet.Add` clamps), and story chapters stay derived and bounded.

**Not added:**
- A device-held HMAC key: the key is extractable.
- Cross-checking ledger totals against records: these are diagnostics only and do not affect gameplay.


## D-079 — Sub-unit step remainders carry per provider (2026-10-16, closes KI-019)

**Context.** Conversion (100 steps = 1 Vitality) was applied per record with the remainder discarded. Health Connect stores many small records, so real walking lost up to 99 steps per record (100 × 150-step records credited 100 instead of 150; sub-100 records credited nothing).

**Decision.** `ProcessedRecordLedgerState.StepCarryByProvider` holds each provider's remainder (0..99). A new record credits `floor((carry + steps) / 100)` and keeps the rest. Rule version stays 1 (same rate, same transaction ids).
- **Correction:** the record's old uncredited steps (`steps − 100 × credited`) come back out of the pool, the corrected steps go in, and the row is re-attributed. Clawback limits (ACTIVITY_PIPELINE §11) are unchanged.
- **Deletion:** the row's credit is reversed exactly as before. Its uncredited steps come back out of the pool, which is clamped to 0..99, so a deletion never creates credit. Drift is under one Vitality per deletion.
- **Save compatibility:** the field is additive; older saves decode with no carry. Legacy rows keep their per-record credits. The validator rejects a carry outside 0..99 or with an empty provider key.
- **Pacing:** synthetic profiles use day-sized records, so pacing gains at most one Vitality per day.

**Evidence.** `StepCarryTests` (7 cases: many small records, sub-100 records across reload, per-provider pools, replay, deletion, correction, validator bound).

## D-080 — Region 1 completion is celebrated in the return summary, calmly and once (2026-10-16)

**Context.** Finishing Region 1 produced a single summary line ("…'s restoration is complete.") among ordinary items. UX_DESIGN asks for a restoration celebration, without pressure, blocking sequences or required motion.

**Decision.** When the pending summary carries the region-complete moment, the shell retitles it "The valley is restored" and leads with two static lines. A "See the restored valley" action follows the primary Continue. There is no new saved state: the summary is acknowledged durably, so the celebration shows once. It is text-first, so it works with reduced motion and screen readers (D-073 outline order), and the copy passes the no-guilt check.

**Evidence.** `RegionCelebrationTests` (4 EditMode cases); EditMode 65/65 in `docs/evidence/b-celebration/`. Device UNVERIFIED (KI-002).

## D-081 — The celebration is proven end to end from a real completed region (2026-10-16)

**Context.** D-080 tested the celebration with hand-built summaries only; no test or picture reached it from a genuinely completed Region 1.

**Decision.** `CompletedRegionFixture` (PlayMode tests) completes Region 1 through the real pipeline: a manual clock (`ShellBootstrap.CreateSession` clock seam), 20,000 synthetic steps per day and a low-decision player that queues the first open project when idle, mirroring `M4Region1AcceptanceTests`. No save is edited, so the seeded state is valid by construction. It completes on day 77.
- `CelebrationPlayModeTests` checks Home offers the summary, the celebration title, headline and primary Continue, renders the screen in a live UIDocument over real frames, follows "See the restored valley", and checks that after Continue the celebration does not show again.
- `ValleyCaptureTests` takes `WG_CAPTURE_SCREEN=Celebration` and captures the same seeded state.

**Evidence.** PlayMode 4 passed + 1 capture-only ignored (`docs/evidence/b-celebration-capture/playmode-results.xml`, Unity-editor-tested). PNG `docs/evidence/b-celebration-capture/celebration-editor-rendered.png` (editor-rendered, llvmpipe/Xvfb, NOT device). The capture found KI-020 (highlight ring clipping). Device UNVERIFIED (KI-002).

## D-082 — Discoveries and expeditions get a read-only detail screen (2026-10-16)

**Context.** The Region screen only counted discoveries and expedition routes. Players could not see what they had found or what opens a route.

**Decision.** A new `Discoveries` shell screen, linked from the valley, built only from `GetDiscoveries()`, `GetExpeditions()`, `GetProjects()` and `GetRegion()`:
- **Found discoveries** show title, kind (closed `PlayerCopy.DiscoveryCategory` table; an unknown kind reads "find") and the date found, oldest first.
- **Unfound discoveries are never named**, only counted, so nothing is spoiled. Body and provenance text are content keys without authored copy yet, so they are not shown.
- **Routes** show completed / open ("completes on its own while you walk", matching D-037: no start or claim mechanic) / locked with "opens after:" in player words. Unknown project or landmark ids degrade to generic words, never raw ids.
- **Viewing is read-only.** It does not mark discoveries reviewed, so the save bytes are unchanged (tested).

**Evidence.** `DiscoveriesScreenTests` (6 EditMode cases, including a completed region reached through the real pipeline); EditMode 71/71. Editor-rendered PNG (llvmpipe/Xvfb, NOT device). Device UNVERIFIED (KI-002).

## D-083 — Settings previews the text size; it still follows the phone (2026-10-16)

**Context.** D-073 scales text by the OS font scale (clamped 1–2×), but nothing told the player that, or showed the result.

**Decision.** Settings adds "Text size: N%. It follows your phone's font size setting." and a bordered sample sentence drawn at the effective scale (`ScreenModel.TextPreview`, label `text-preview`). Above 2× it says this is the largest size the game supports. The outline reads the sample after the lines and before the actions.
- **No in-app override.** A second, conflicting text-size control would need a new preference and could fight the OS setting. Following the phone stays the single source.
- **Robust source.** `ShellController.TextScaleSource` is wired to `AccessibilitySettings.fontScale` by the bootstrap. NaN, infinities, out-of-range values and a throwing source all degrade to a clamped value without breaking the screen.

**Evidence.** `TextSizePreviewTests` (9 EditMode cases); editor-rendered PNG at 150% (`WG_CAPTURE_TEXT_SCALE=1.5`; llvmpipe/Xvfb, NOT device). The real OS font scale on a phone is UNVERIFIED (KI-002).

## D-084 — The shell rejects malformed and out-of-order input calmly (2026-10-16)

**Context.** A hardening probe of `ShellController.Invoke` found three defects:
1. `nav:` used `Enum.Parse`. An unknown name threw, and a comma list (`nav:Home,Story`) parsed to the OR of the values, which is another real screen (Discoveries).
2. Before a game was loaded, any game action or game screen called a read model that throws (`RequireState`).
3. `start` called `StartNewGame`, which writes unconditionally. From a loaded game (via `nav:Onboarding`) or the BootError screen, that would replace the player's world or an unreadable save that might still be recoverable.

**Decision.**
- Navigation accepts exact screen names only; anything else is "That screen does not exist." on the current screen.
- Before a game is loaded, only `start`, `retry`, `activity:connect`, `nav:Onboarding` and `nav:BootError` work; the rest is a calm error. A null or empty action id is an error too.
- `start` works only on the onboarding screen of an unbooted shell. Starting after "Delete all local data" still works.
- The Application API is unchanged (KI-021).

**Evidence.** `ShellInputHardeningTests` (21 EditMode cases). The comma-list case failed first (it opened Discoveries), then passed after the fix. Device UNVERIFIED (KI-002).

## D-085 — StartNewGame takes an explicit overwrite parameter; the shell refuses to replace (2026-10-16)

**Context.** KI-021: `GameSession.StartNewGame(seed)` writes unconditionally. D-084 guarded it in the shell only. Changing the one-argument method to refuse would break a long-standing contract used by many headless tests and tools that deliberately start over a save.

**Decision.**
- New overload `StartNewGame(seed, overwriteExisting)`. With `false`, it reads both slots first and returns the new `StartStatus.ExistingSaveRefused` (appended value 7) when the primary or the backup holds anything, or when the store reports an I/O failure (fail closed). Nothing is written and the session is unchanged.
- `StartNewGame(seed)` is kept and documented as the explicit overwrite (`overwriteExisting: true`), so every existing test and tool stays valid without edits.
- The shell's `start` now calls `overwriteExisting: false` on top of the D-084 screen guard, so a save that appeared after boot (another session, a late restore) is refused, not replaced. `BootRouter` maps the new status to BootError ("Try again to load it"), keeping the router total.
- No save-format or content change.

**Evidence.** `StartOverwriteGuardTests` (6 .NET cases: empty store, existing save byte-identical, unreadable primary, backup-only, legacy overload still replaces, after delete-all). `ShellInputHardeningTests.Start_refuses_a_save_that_appeared_after_boot`. `ShellControllerTests.Router_maps_every_start_status` failed first on the new value, then passed. .NET 653/653; EditMode 102/102 (`docs/evidence/c-ki021/`, Unity-editor-tested). Device UNVERIFIED (KI-002).
## D-086 — Discoveries get authored story and origin text (2026-10-16)

**Context.** D-082 showed found discoveries by title, kind and date only; body and provenance were content keys with no copy.

**Decision.**
- `WalkGame.Application.Content.Region1DiscoveryCopy` holds authored text for all 26 Region 1 body/provenance keys. The catalog keeps its keys, so canonical state, the content fingerprint and saves are unchanged (no save-compat impact).
- The copy follows the D-076 Millbrook chapters: the floods and the failed drainage scheme, the heron-over-wheel mark (millstone, workshop ledger, mural, roost boards, survey stone), the cranes, the ribboned oak, the pencilled map on the market board, and the survey that closes the story.
- The Discoveries screen shows, under each found discovery, its body and "Where it came from: …". Unfound discoveries still show nothing (no spoilers). A key without copy is skipped; a raw key never reaches the screen.

**Evidence.** `DiscoveryCopyTests` (6 .NET cases: every discovery has body+origin, no placeholders or keys, length bounds, no orphan copy, unknown keys return null). `DiscoveriesScreenTests` +2 EditMode cases (copy shown under found discoveries in order; unfound copy never shown). EditMode 103/103 (`docs/evidence/b-discovery-copy/`, Unity-editor-tested). Device UNVERIFIED (KI-002).

## D-087 — The moment ring always fits inside the valley picture (2026-10-16)

**Context.** KI-020: in the celebration capture the ring around the right-most changed landmark was cut by the picture border. The ring was drawn at radius 1.9×s around the landmark with no bounds check.

**Decision.** `ValleyElement.RingGeometry(cx, cy, s, w, h)` (engine-free, public for tests) keeps the full stroke inside the picture: first shrink the radius (never below 1.2×s, so the ring still surrounds the landmark), then shift the centre only as much as needed. With room, the ring is unchanged. No layout, content or save change.

**Evidence.** `RegionSceneTests.Moment_ring_always_stays_inside_the_picture_and_around_its_landmark` sweeps 6 widths × every landmark position for 1–8 landmarks × 5 levels (bounds, landmark inside ring, radius ≥ 1.2×s), plus an unchanged-with-room case. EditMode 103/103 pre-merge (`docs/evidence/b-ki020-ring/`, Unity-editor-tested). Re-captured `docs/evidence/b-ki020-ring/celebration-editor-rendered.png` (editor-rendered, llvmpipe/Xvfb, NOT device): the right-most ring is fully inside. Device UNVERIFIED (KI-002).
## D-088 — Performance pass: pooled body-line labels, release-silent render log (2026-10-16)

**Context.** D-074 budgeted structure and wall time but never measured allocation per screen switch, and it left Story and Discoveries out of the screen set.

**Decision.**
- Measure: `ShellPerformanceTests.Screen_switch_allocations_are_measured_and_bounded` logs approximate managed bytes for the model and the view per render on all 8 game screens of the 365-day save. Unity Mono has no per-thread allocation counter, so it uses `GC.GetTotalMemory` growth, the median of 60 samples with collections dropped, at 4 KB resolution. A loose 1 MB ceiling catches only order-of-magnitude regressions; wall-times are not gated.
- Fix the measured hotspot: `ShellView.Render` reuses pooled body-line labels per root instead of creating a Label and its style per line on every render. A test proves that no stale text or `empty-state` name carries across screens.
- The `WG_RENDER` per-render `Debug.Log` runs only in debug builds (release builds paid for string building and a stack trace on every screen switch).
- The model side measured ≤ ~20 KB and mostly ~0, so it was left unchanged.

**Evidence.** `docs/evidence/c-perf-pass/PERFORMANCE_PASS.md` has the before/after tables. Story view ~86 KB → ~12 KB, Projects ~78 KB → ~8 KB, 3,000 enter/exits 678 → 392 ms, live-panel growth ~116 → ~78 B/render (about 0 on a rerun, within noise). EditMode 108/108 on the merged branch, PlayMode 4 passed + 1 capture-only ignored (Unity-editor-tested). Device UNVERIFIED (KI-002); KI-018 stays open.

## D-089 — Pooled action buttons across screen switches (KI-018) (2026-10-16)

**Context.** After D-088, buttons (a closure, a Button and its inline style per action) were the largest remaining view allocation per screen switch.

**Decision.** `ShellView` pools the `actions` container and its buttons per root alongside the body-line labels (one `PoolState` in `root.userData`). A pooled `ShellView.ActionButton` registers one click handler that invokes the current render's callback with the current action id; all visible properties and primary/secondary styling are reset per render. Element names stay the action ids, so tests, the accessibility outline and autodrive are unchanged.

**Evidence.** `docs/evidence/d-buttons/PERFORMANCE_BUTTONS.md`: Settings view ~41 KB → ~4 KB, Home ~37 KB → ~0, 3,000 enter/exits 384 → 151 ms, live-panel growth within noise. EditMode 109/109, PlayMode 4 + 1 ignored (Unity-editor-tested). Device UNVERIFIED (KI-002); KI-018 open only for a device measurement.
## D-090 — Home story line: current chapter, next hint, epilogue (2026-10-16)

**Context.** The story (D-076) was only reachable from the journal and return-summary chapter moments; Home gave no sense of where the player is in Millbrook's story.

**Decision.** Home shows one line, `ShellController.HomeStoryLine`: "Story: <latest unlocked chapter heading>. Next: <next chapter's hint>", using the same title and hint the journal already shows, so a locked chapter's title or body never appears. Once every chapter is unlocked it shows the epilogue line ("Millbrook's story is complete. The valley stays restored, and every chapter is here to reread."). Shell-only: no content version, fingerprint or save change. Copy obeys the no-guilt check.

**Evidence.** `StoryJournalTests.Home_shows_the_current_chapter_and_the_next_hint_without_spoilers` (new game prologue + after a 10-day walk) and `Home_story_line_becomes_the_epilogue_when_every_chapter_is_unlocked`. EditMode 111/111 on the merged branch (`docs/evidence/d-home-chapter/`, Unity-editor-tested). Device UNVERIFIED (KI-002).

## D-091 — Adverse byte-level save damage: recovery must persist; both damaged fail closed (2026-10-16)

**Context.** D-078 covered checksum-valid semantic tampering, and Infrastructure tests covered empty/junk generations at the store level. No session-level test proved that byte-level damage to the primary (truncation, one flipped byte, oversized garbage, UTF-16 re-encoding, NUL fill) recovers and that the recovered game then persists.

**Decision.** No production change: the existing envelope checksum and backup fallback already behave correctly. `SaveRecoveryAdverseTests` locks the contract in: (a) each damaged primary yields `RecoveredFromBackup`, a later ingest commits, and the next boot is `Loaded` with progress not below the recovered value; (b) with both generations damaged the session fails closed (`SaveUnreadable`/`StateInvalid`, no loaded state) and `StartNewGame(seed, overwriteExisting: false)` returns `ExistingSaveRefused` with both files byte-identical (D-085).

**Evidence.** 11 cases, `docs/evidence/d-save-adverse/save-recovery-adverse.trx` (automated verified, headless .NET). Full .NET: Domain 148, Infrastructure 109, Application 413 (670). Device storage faults (real power loss, full disk) remain UNVERIFIED (KI-002).

## D-092 — Onboarding and permission edge cases; a fresh-device start resets stale onboarding (2026-10-16)

**Context.** Onboarding is durable in the local preferences file, separate from the save. `DeleteAllLocalData` erases saves first and preferences second, so an app killed between the two leaves a stale onboarding stage (possibly `Complete`) next to no save. The next boot routes to the onboarding screen correctly, but "Begin restoring" then inherited the stale stage, and a brand-new world could skip onboarding entirely. Deny-then-grant mid-onboarding, revoke after onboarding and a kill mid-onboarding had no shell-level tests.

**Decision.** `GameSession.StartNewGame(seed, overwriteExisting: false)` (the fresh-device path the shell uses, D-085) resets only the onboarding stage to `NotStarted` after the new save is durable. Other preferences (reduced motion, sound, text) are player choices and stay. Best-effort: a preferences write failure never fails the start. The legacy overwrite overload is unchanged. No save, content-version or fingerprint change.

**Evidence.** `StaleOnboardingResetTests` (5 .NET cases; the two reset cases fail without the fix). `OnboardingPermissionEdgeTests` (5 EditMode cases): denied keeps the step with no re-prompt and no read, a later grant is picked up on resume and syncs; revocation never reopens the guide or reads steps; a kill right after start or at the first-project step resumes there with the canonical gate intact; a kill mid delete-all starts the new world's onboarding. Unity-editor-tested, `docs/evidence/e-onboarding-edges/`. The real Health Connect permission dialog and process death on a device remain UNVERIFIED (KI-002).

## D-093 — An outdated Health Connect provider asks for an update, not a retry (2026-10-16)

**Context.** The bridge reports `update_required` when Health Connect is installed but too old. The adapter mapped it to `TemporarilyUnavailable` with permission `NotRequested`, so the projector showed "Walking data is not connected yet" with a Connect action (a dead end: the permission flow cannot run on an outdated provider) and, after a failed read, "we will try again" (waiting never fixes it).

**Decision.**
- New `ActivitySourceAvailability.ProviderUpdateRequired`, `ActivityPlayerStatus.ProviderUpdateRequired` and `ActivityRecommendedAction.UpdateProvider`, appended (existing numeric values unchanged). The adapter maps `update_required` to it.
- Projector precedence step 0: `ProviderUpdateRequired` outranks every permission state.
- Copy: "Health Connect needs an update before new walks can count. Your progress is safe." / "Next: update Health Connect from your app store, then come back." The Glance sentence says the same in its own voice.
- These are ephemeral read-model values (D-043), never persisted: no save or content change (no save-compat impact).

**Evidence.** `HealthConnectFailureStateTests` (9 .NET cases through the real adapter, session and projector with a fake bridge): outdated provider (no read, no credit loss, UpdateProvider), recovery after update (dedupe holds), provider uninstalled mid-session (no Connect dead end), permission revoked between status check and read (whole fetch fails, nothing credited, then OpenSettings), clock moved backwards, timezone change ×3, 40-day gap with a failure before success (backlog once). Projector precedence table +3 rows; the adapter SDK mapping row updated. .NET 684 total; EditMode 116/116 incl. every-status-has-copy (`docs/evidence/d093-hc-failure-ux/`, Unity-editor-tested). Device UNVERIFIED (KI-002): the real `update_required` signal and Play Store routing are untested; the shell shows the action text only, it does not deep-link.

## D-094 — Onboarding shows the update-required state for an outdated Health Connect (2026-10-16)

**Context.** D-093 fixed the Activity screen, but onboarding derived its step state from permission only. An outdated provider reports permission `NotRequested`, so the guide's walking step and the Glance onboarding sentence still said "Connect" (a dead end).

**Decision.**
- New `OnboardingActivityStepState.ProviderUpdateRequired = 5` (appended). Availability `ProviderUpdateRequired` outranks every permission state, as in D-093's projector step 0.
- Guide step title becomes "Update Health Connect", shows the D-093 status and "Next: update…" lines, never offers Connect, and keeps Continue (nothing is locked). Glance onboarding sentence: "Your movement app needs an update before new walks can count. You can keep going; nothing is locked."
- The onboarding stage never moves because of this; after an update the normal Connect step returns. Ephemeral read-model value only: no save or content change.

**Evidence.** `OnboardingProviderUpdateTests` (5 .NET cases); EditMode `Outdated_health_connect_shows_update_required_not_connect`. .NET 689 total; EditMode 117/117 (`docs/evidence/d094-onboarding-update/`, Unity-editor-tested). Device UNVERIFIED (KI-002).

## D-095 — Accessibility audit across every screen; primary-button contrast raised to 8:1 (2026-10-16)

**Context.** D-073 covered six screens. Onboarding, Guide, Story, Discoveries, DeleteConfirm and BootError had no focus/label/touch checks, and the theme's contrast claims were comments, not tests.

**Decision.** `AccessibilityAuditTests` (8 EditMode cases) renders one model per `ShellScreen` value (a coverage test fails if a new screen is added without being audited) and checks: button tree order = action order = screen-reader order, title first, every button focusable; non-empty, unique, non-id-like labels; touch targets ≥ 48 dp (Android minimum) at text scale 1, 1.3 and 2, with spacing; WCAG contrast of every theme pair (text ≥ 7:1, non-text ≥ 3:1) and that rendered text uses only audited colours; no element carries a transition with reduced motion on or off.

**Finding fixed.** The primary-button text (white on `Accent` #2F6B4F) measured 6.29:1, below the documented 7:1 body target. `Accent` is now #245A41 (8.03:1 with white; 7.1:1 against the page). The Muted comment is corrected to the measured 7.49:1. The valley picture's own palette (RegionScene) is unchanged. Visual-only; no save or content change.

**Evidence.** EditMode 125/125 (`docs/evidence/d095-a11y-audit/`, Unity-editor-tested). TalkBack, switch access and real touch on hardware UNVERIFIED (KI-002).

## D-096 — Copy polish pass with a source-level no-guilt check (2026-10-16)

**Context.** A review of every shell string found pressure or code leakage outside the summaries that D-073 checked: "Keep walking: …" on the empty Projects screen; "Delete failed (<category>)" and "Could not start a new game (<status>): <detail>"; "Unknown action: <id>"; domain error text shown verbatim (e.g. "Unknown project 'proj.nope'."); "failed its integrity check"; "Please update." with no reassurance.

**Decision.** Rewritten calmly, with no codes or ids: "No projects are open right now. Finishing a restoration opens new ones."; "We couldn't start a new world. Nothing on this device was changed."; "Your data could not be deleted, so nothing was changed. You can try again."; "Unknown action."; any failed domain result shows "That could not be done just now. Your progress is safe."; "Your save did not pass its safety check. …"; "This save was made by a newer version of the game. Update the game to open it; nothing has been changed."; "Next: nothing to do. It will try again on its own." Copy only; no save, content or behaviour change.

**Evidence.** `CopyPolishTests` (6 EditMode cases): a source scan of the player strings in ShellController, PlayerCopy and BootRouter against a guilt/pressure list (including "!"); every activity status/action and boot outcome; bad action ids never echoed. EditMode 131/131 (`docs/evidence/d096-copy-polish/`, Unity-editor-tested). Gap: Application-layer Glance copy is covered by the existing .NET GlanceLanguageTests, not by this scan.

## D-097 — Copy scan covers Home summary text; settings status lines reworded (2026-10-16)

**Context.** D-096 left a gap: Home shows Application-layer Glance sentences, the story line and the balance line, and none of them went through the shell copy scan. The Settings status lines were bare labels ("Reduced motion: on", "Diagnostics: hidden"), which are terse for a screen reader and say nothing about what the setting does.

**Decision.** `CopyPolishTests` renders Home after the guide, after five days of walking and after a 30-day absence, and checks every line against the guilt/pressure list and the raw-code pattern. The settings lines are now full sentences from `ShellController.ReducedMotionLine/SoundLine/DiagnosticsLine`, for example "Reduced motion is on. Your choice is saved on this device." and "Support details are hidden. You only need them to look into a step-count question." The wording claims nothing the shell does not do; the shell has no animation today (UX_DESIGN). This is a copy change only, with no save, content or behaviour change.

**Evidence.** 2 new EditMode cases; EditMode 133/133 (`docs/evidence/d097-copy-scan/`, Unity-editor-tested; device UNVERIFIED, KI-002).
## D-098 — Long-run save growth gate and save-load fuzzing; two fail-closed fixes (2026-10-16)

**Context.** Region 1 hardening asked for long-run save-size and migration fuzz tests. A deterministic fuzz of mature saves (1–3 random mutations per case: remove a field or element, or set it to null, -1, `long.MaxValue`, a string or an empty object; the checksum is re-sealed so the damage reaches decoding) found two ways that `Continue()` **threw** instead of failing closed: (1) `"records": null` made System.Text.Json raise `InvalidOperationException`, which the codec did not catch (it caught `JsonException` only); (2) a null processed-record entry made `GameStateValidator` throw `NullReferenceException` inside `RecomputeLifetimeVitality`. A malformed `payloadSha256Base64` would also have thrown `FormatException`.

**Decision.** `SaveCodec.Decode` maps `InvalidOperationException` to `DeserializationFailed` and an unreadable checksum field to `ChecksumMismatch`. `GameSession.DecodeAndValidate` treats a validator exception (NullReference, InvalidOperation, Argument, Overflow, KeyNotFound) as a validation failure (`StateInvalid`, file untouched). There is no change to the save format, migrations or any valid save's load.

**Evidence.** `LongRunSaveAndMigrationFuzzTests` (3 .NET cases, automated verified). (a) 400 days of daily play: 36,013 B at day 60, 46,500 B at day 200, 58,513 B at day 400; late growth is 60 B/day (the D-066 identity-only retired set), gated at ≤ 100 B/day and no faster than early growth; the final state validates and lifetime Vitality survives retirement. (b) Fuzz: 400 cases over a genuine schema-v1 mature save (27 loaded valid, 318 SaveUnreadable, 55 StateInvalid) and 400 over a current-schema save (43 / 307 / 50). Every loaded case validates at the current schema, and every refused case leaves the primary byte-identical. Before the fix, cases 68 and 144 of the v1 run threw. .NET 692/692.

## D-099 — Region 1 "Your progress" screen (2026-10-16)

**Context.** The roadmap's MVP polish list and the request for this campaign both name a progress/stats view. Players could see balances and the story, but nowhere showed what their walking had added up to. The constraint is no streak pressure: nothing that counts days in a row, sets goals or compares days.

**Decision.** Home → "Your progress" is a read-only screen. It shows: lifetime Vitality gathered (the durable ledger total, which survives row retirement per D-066); the approximate steps (Vitality × 100, the V1 conversion rule, labelled "about" because sub-unit carry is not included); restorations finished, as N of total; discoveries found; story chapters unlocked; and the closing line "Nothing here expires. Every walk counts, whenever it happens." Before any credit, it shows the empty state "Your first walk will show up here. There is no rush." The screen is composed in the shell from existing read models, with no Application, save or content change. **Not included:** "days walked". Per-day history older than the 45-day trusted window is retired (D-066), so an honest lifetime count would need a new persisted aggregate and a save migration. That is deferred and needs its own D-entry.

**Evidence.** `ProgressScreenTests` (6 EditMode cases): navigation and read-only actions, real credited walking, rendering leaves the save byte-identical, and calm copy in every state (guilt list plus "goal" and "record"). The accessibility audit now reaches 13 screens. EditMode 137/137 on the branch and 139/139 after merging main (with D-097), and PlayMode 4 passed + 1 capture-only ignored (`docs/evidence/d099-progress/`, Unity-editor-tested). Editor-rendered PNG: `docs/evidence/d099-progress/progress-editor-rendered.png` (llvmpipe/Xvfb, NOT device; 6 synthetic days at 120,000 steps = 7,200 Vitality, about 720,000 steps). Device rendering is UNVERIFIED (KI-002).

## D-100 — Project-completion moments name the landmark and the story (2026-10-16)

**Context.** A finished project showed in the return summary as a bare "{Project} is complete." The handoff asked for richer, calm completion moments tied to the valley and the storyline.

**Decision.** `ProjectCompletionMoments.Describe` (Application, pure) derives one follow-up line from authored content: the landmark stage the project unlocks ("Old Trailhead is now stabilized.") and the storyline chapter that requires it ("It is part of the chapter …" once unlocked, otherwise "The story moves toward …"; the journal already shows chapter titles, so nothing is spoiled). The shell adds it directly under each *current* single-project completion item. Stored summary text, the composer, the per-kind bounds, the save and the content fingerprint are unchanged; aggregate, region and chapter lines get no follow-up.

**Evidence.** `ProjectCompletionMomentsTests` (.NET, 8 cases including a calm-copy sweep over every project) and `ProjectCompletionMomentTests` (EditMode, real walking through the shell). .NET 700/700; EditMode 140/140 in `docs/evidence/d100-completion/` (Unity-editor-tested). Device UNVERIFIED (KI-002).

## D-101 — Valley restoration ambience: footpaths, flowers and birds, inside the shape budget (2026-10-16)

**Context.** The valley picture changed per landmark, but the land between landmarks stayed the same until the hills turned green. The campaign asked for a performance-safe restoration visual improvement with a before/after picture.

**Decision.** `RegionScene.Ambient` derives three static details from data the scene already has: a footpath between each pair of neighbouring landmarks that are both past Ruined (≤ 7), meadow flowers that grow with the share of restorations finished (1 as soon as one is finished, up to 5), and birds once the valley is half green (1) and fully restored (2). `ValleyElement` draws them with Painter2D at fixed positions, with no textures and no per-frame work. `MaxShapes` rises by `MaxAmbientShapes` (14) from 86 to 100, which is exactly the existing phone-safe cap that `RegionSceneTests` enforces. The alt text mentions footpaths and birds, so the picture is never the only channel. No save, content or Application change.

**Evidence.** `ValleyAmbienceTests` (4 EditMode cases: fresh valley empty, paths only between recovered neighbours, monotonic growth, worst-case budget). EditMode 143/143, PlayMode 4 passed + 1 capture-only ignored (`docs/evidence/d101-valley/`, Unity-editor-tested). Editor-rendered before/after (llvmpipe/Xvfb, NOT device): `docs/evidence/d101-valley/celebration-before-after-editor-rendered.png` (completed valley: paths, five flowers, two birds) and `valley-before-after-editor-rendered.png` (mid-game, 1 of 19: one flower, which is honest for that state); full frames are under `before/` and `after/`. Device rendering is UNVERIFIED (KI-002).

## D-102 — Lifetime "days walked" is deferred: an honest count cannot be built from today's save (2026-10-16)

**Context.** D-099 left out "days walked" and asked for its own decision. This campaign was asked to add it as an honest lifetime counter through a proper versioned save migration, only if that fits the repository's migration policy.

**Finding.** The policy itself permits it: a persisted-shape change bumps `SchemaVersions.Current` (2 → 3) and registers a sequential `ISaveMigration`, like `m1-to-v2`. The honesty requirement is what fails, for two reasons found in the code:
1. **The past cannot be recovered.** D-066 retires processed-record rows older than the trusted window, and a retired row keeps only its identity (`RetiredIdentities`). The identity does not encode a date, and `RecordEndUtc` is dropped. So for any save older than about 45 days, a v2 → v3 migration could only count days in the retained rows. That number is an undercount presented as a lifetime figure.
2. **A "day" is undefined in the save.** Records carry only UTC instants (`StartUtc`/`EndUtc`). No zone offset or local date is persisted, so a morning walk in UTC+8 falls on the previous UTC day. A UTC-date counter would disagree with the player's own calendar, and the device's current zone is not the zone of past walks.

**Decision.** No schema bump and no counter in this campaign. Shipping a migration that produces a lower bound labelled "lifetime", or a UTC-day count, would be a silent honesty break. The Progress screen stays as D-099 defined it.

**What a correct version needs (future OpenSpec change).** (a) Persist each credited record's local date (the provider's zone offset; Health Connect supplies one) on ingest. (b) Keep a maintained distinct-day aggregate that is updated on credit and on deletion or correction, and folded into a retired-days total at retirement (bounded growth under the D-098 60 B/day gate). (c) Add a v2 → v3 migration that seeds the count from retained rows and marks legacy saves as a lower bound, shown as "at least N days", with migration tests on representative and mature fixtures. Each part changes the persisted shape, so it goes through OpenSpec first.

**Also in this change (hardening).** `TitleMatchingInvariantTests` (3 .NET cases). Summary items are text-only and the shell links them to content by title (moment rings by substring, D-100 lines by exact project title). These cases assert that project titles are unique, that landmark titles are unique and none contains another, and that a project title naming a landmark only does so when it raises that landmark.

## D-103 — "Days walked" ships: local date per record, maintained count, "at least N" for legacy saves (2026-10-16)

**Approval.** The OpenSpec change `openspec/changes/add-days-walked-count/` was approved by the executing agent. Repository policy allows it: the change is Region 1 scope, and the only gates on agent approval are D-011 and the ROADMAP post-MVP gate, which cover Region 2. It follows the save-migration policy in `openspec/config.yaml` (a registered `ISaveMigration` plus a mature-save fixture test).

**Decision.** It implements the design that D-102 named:
- `NormalizedActivityRecord.EndZoneOffsetSeconds` is optional and outside identity. The Health Connect bridge sends it as an optional sixth field.
- `ProcessedRecordEntry.LocalDay` stores the record's local date.
- `ProcessedRecordLedgerState` maintains `WalkedLocalDays` (sorted, distinct). It is updated on credit, on correction and on deletion (a day is removed only when no other retained row credits it and the day is after every retired row's day). Retirement never prunes it.
- Schema v2 → v3 (`MigrationV2ToV3`) seeds a proven lower bound: the minimum number of local dates that can explain the retained credited end instants (greedy 50 h cover; offsets −12…+14 h). Retired rows add nothing, so the result is a lower bound and is flagged.
- Post-migration days on or before the last legacy UTC day + 1 are not added, so the count never overlaps the legacy seed.
- The Progress screen says "You have walked on N days." or, for a lower bound, "at least N days". A credit without an offset (fixture and dev sources) also makes the count a lower bound.

**Rejected:** a UTC-date count, and seeding from the device's current zone. D-102 explains both.

**Tests:** `DaysWalkedTests` (Domain, 9), `DaysWalkedSessionTests` (5), `MigrationV2ToV3Tests` (3), adapter sixth-field tests (3), EditMode `ProgressScreenTests` +2. Existing schema-literal assertions now use `SchemaVersions.Current` and the extended chain; no test was deleted.

**Gaps:** The Health Connect `endZoneOffset` read on a device is UNVERIFIED (KI-002). The Java change is compiled only when the APK is built.

## D-104 — Calm, optional sound cues on return moments (2026-10-16)

**Context.** Settings had a sound toggle but the game played no sound. UX_DESIGN asks for calm feedback that never nags.

**Decision.** There are three short cues, and they play only on a return summary that shows real change: Restoration (two rising notes), Chapter (three) and Celebration (four, Region 1 complete).
- A cue plays only when the player's sound setting is on.
- It plays once, on arrival at the screen. A redraw, a text-size change or a screen-reader refresh never repeats it.
- Nothing else makes a sound: no taps, no reminders, no idle loops.
- The samples are synthesised in code (`SoundCues.Samples`: soft sine tones with a raised-cosine envelope, peak 0.2 or about −14 dBFS, under 1 s each). There are no audio assets, so nothing needs licensing.
- `com.unity.modules.audio` is added to the Unity manifest. It is a built-in module and costs nothing.

**Tests:** EditMode `SoundCueTests` (5): sound off means no cue, cue priority, arrival-only playback, sample bounds (quiet, short, silent at both ends, no clicks, deterministic), and a real return summary following the setting.

**Gaps:** Audible output and volume on a device are UNVERIFIED (KI-002). The default of the sound setting is unchanged.

## D-105 — Hardening: days-walked plausible range, tamper and fuzz (2026-10-16)

**Context.** D-103 added persisted days-walked fields. A tampered or corrupted save could show the player an impossible count, for example 5,000 days on a three-week-old profile.

**Decision.** `GameStateValidator` now checks a plausible range. Every walked day, and the legacy boundary, must lie between the UTC day of `CreatedAtUtc` − 47 (records can be up to 45 days older than a new profile, and a local date is at most a day off its UTC date) and the UTC day of the later of `LastAdvancedUtc` and `IngestionCheckpointUtc`, + 2. Neither the count nor the legacy lower bound may exceed that span. A violation fails closed and leaves the save untouched, as with every other validator rule.

**Tests:** `DaysWalkedHardeningTests` (5 cases): inflated legacy count rejected and file untouched; far-future and ancient days rejected; an honest +14 h edge stays valid; a 300-case fuzz of the `processedRecords` subtree on both v2 (migration path) and v3 saves, where each case loads valid or fails closed untouched.

## D-106 — Home layout polish: no repeated lines (2026-10-16)

**Context.** The editor-rendered Home showed the same sentence twice (the glance "what changed" and "attention" lines were both "Clear the old trailhead is complete."). An empty queue also printed both "Nothing is being restored right now." and "Nothing is being restored yet. Choose a project to begin."

**Decision.** Home drops the glance "restoring now" sentence when the queue is empty (the empty-state line keeps the call to action), and shows each line once, with ordinal de-duplication and the first occurrence kept. Read models and copy are unchanged; this is shell-only, with no save or content change.

**Tests:** EditMode `HomeLayoutPolishTests` (3 cases). Evidence: `docs/evidence/d106-home/` holds before/after PNGs (editor-rendered, llvmpipe) and EditMode 154/154 (Unity-editor-tested). Device rendering is UNVERIFIED (KI-002).

## D-107 — Battery / background-sync budget review (2026-10-16)

**Context.** PERFORMANCE_BUDGETS §12–§14 ask for no polling loops, no wake-heavy timers and bounded activity queries, and the release checklist (L) asks about battery drain from background reads. Nothing checked the shell's actual query frequency.

**Decision (review result).** The current design meets the budget and is now pinned by tests. Walking data is read only on boot and on return to the foreground (`OnApplicationPause(false)` → `Resume`), once each, over a window of `SyncWindowDays` = 30 days. There is no background sync (KI-016c), no timer, and no per-frame `Update`. Navigation and rendering only make cheap status queries (SDK and permission state), never data reads. The only `WaitForSeconds` is the development-only emulator autodrive. No code change was needed.

**Tests:** EditMode `BackgroundSyncBudgetTests` (5): 100 navigations make zero data reads; each resume reads exactly once; every read window is ≤ 30 days; status calls per render stay within a cap of 8 (measured 0.89 in the editor); a source scan bans `Update`/`InvokeRepeating`/timers/`while(true)` in the shell.

Evidence: `docs/evidence/d107-sync-budget/editmode-results.xml`, EditMode 159/159 (Unity-editor-tested).

**Gaps:** Device battery drain, binder cost of status calls, and thermal behaviour are UNVERIFIED (KI-002). Background sync remains deliberately absent (KI-016c).

## D-108 — iOS HealthKit parity check: honest unsupported state, gap recorded (2026-10-16)

**Context.** ROADMAP M7 lists HealthKit integration, and several docs say "Health Connect/HealthKit". A code check found no HealthKit adapter, bridge, entitlement or iOS build. The non-Android release port still carried the stale detail "health connect adapter not yet implemented (M5-A)".

**Decision.** Record the gap instead of implying parity. `docs/PLATFORM_PARITY.md` holds the capability matrix and the contract a future HealthKit adapter must meet. KI-022 tracks the missing adapter. `ShellConnectionPort` now carries a traceable diagnostic (`UnsupportedDetail`, citing KI-022); it is never shown to players. No behaviour, save or content change.

**Tests:** EditMode `PlatformParityTests` (4): unsupported snapshot, onboarding completes with no Connect dead end, Activity screen is honest and leaks no diagnostics, no development walking in release.

Evidence: `docs/evidence/d108-platform-parity/editmode-results.xml`, EditMode 158/158 (Unity-editor-tested).

**Gaps:** No iOS adapter or build (KI-022). Needs macOS/Xcode and a device; iOS behaviour is UNVERIFIED.

## D-109 — Screen-tour copy fixes (2026-10-16)

**Context.** The editor-rendered screen tour (`docs/evidence/screen-tour/`) showed two copy defects: BootError printed a raw identifier ("Status: SaveUnreadable", against UX_DESIGN §5), and queue buttons doubled the verb because project titles are already verb phrases ("Restore Restore the river intake").

**Decision.** BootError drops the raw status line; the plain-language problem notice and "Try again" stay. Queue buttons read "Start: <title>" (`ShellController.QueueLabel`) on Projects and in the guide. Shell copy only: no save, content or read-model change.

**Tests:** EditMode `ScreenTourCopyTests` (2). Evidence in `docs/evidence/d109-tour-copy/`: re-captured BootError and Projects PNGs (editor-rendered, llvmpipe) and EditMode 161/161 on this branch merged with main (Unity-editor-tested).

**Gaps:** Device rendering UNVERIFIED (KI-002). A booted shell can still reach BootError via `nav:BootError` with no notice (no player button leads there).


## D-110 — BootError is refused while a game is loaded (2026-10-16)

**Context.** D-109 left a gap: a booted shell could reach BootError via `nav:BootError` and show "We couldn't open your world" over a healthy, open game. No player button leads there, but the action id is accepted by `Invoke`.

**Decision.** With a game loaded, `nav:BootError` is refused as a calm error on the current screen: "Your world is open and fine. There is no problem to show." The screen does not change. Before a game is loaded, BootError stays reachable (the boot router and `retry` still use it). Shell input only: no save, content or read-model change.

**Tests:** EditMode `ShellInputHardeningTests` +2 (`BootError_is_refused_while_a_game_is_loaded`, `BootError_stays_reachable_before_a_game_is_loaded`). Evidence: `docs/evidence/d110-booterror/`, EditMode 167/167 (Unity-editor-tested).

**Gaps:** Device rendering UNVERIFIED (KI-002).
## D-111 — Local-only Privacy screen with save export through the OS share sheet (2026-10-16)

**Approval.** The OpenSpec change `openspec/changes/add-privacy-data-export/` was approved by the executing agent. As with D-103, repository policy allows it: it is Region 1 / shell scope, it changes no persisted shape, and the only gates on agent approval (D-011, the ROADMAP post-MVP gate) cover Region 2.

**Decision.**
- New `ShellScreen.Privacy` (appended last), reached from Settings ("Privacy and your data"). Six plain lines say what is stored (valley progress; walks: step counts, start/end, local day, the walking app's record number; settings and guide progress), where (app-private storage under the phone's encryption, D-067), and what is never collected (no name, email, location, accounts, ads, analytics; nothing sent anywhere).
- `export` ("Share a copy of your save"): `GameSession.ExportSaveEnvelope()` (the exact envelope the codec writes, read-only) → `SaveExport.ToReadableJson` (Infrastructure; verifies the payload SHA-256, returns `Exported`/`Malformed`/`ChecksumMismatch`, never throws) → `ShellController.ShareText`. Document: `format` "simple-walk-game-export", `formatVersion` 1, `schemaVersion`, `savedAtUtc`, `payloadSha256`, `state`.
- `AndroidShareSheet` sends `ACTION_SEND` `text/plain` through `createChooser` (C# JNI only; no Java, manifest or FileProvider change). Other platforms have no share sheet and say so.
- Fail closed with one calm sentence: no share sheet, a failed or throwing share, a document over 512 KB, or a bad envelope. Before a game is loaded the screen and action are refused (D-084 rules).

**Rejected:** writing an export file to shared storage (PRIVACY_REVIEW §3 forbids less-protected copies); exporting the raw base64 envelope (unreadable to players); import/restore (needs save-trust rules; a later change).

**Tests:** .NET `SaveExportTests` (11: read-only export, decode round trip, document shape, tamper → `ChecksumMismatch`, hostile bytes → `Malformed`), .NET 739/739. EditMode `PrivacyScreenTests` (7) and the D-095 accessibility audit now covers Privacy; EditMode 172/172 on this branch and 174/174 merged with main after D-110 (Unity-editor-tested). Evidence: `docs/evidence/d111-privacy/`.

**Gaps:** The Android share sheet on a device is UNVERIFIED (KI-002); the JNI path is compiled only when the APK is built. The screen tour does not yet include a Privacy PNG.

## D-112 — KI-022 started: HealthKit adapter (headless-tested), Swift bridge (UNVERIFIED), not composed (2026-10-16)

**Context.** D-108 found no iOS provider. The adapter contract is in `docs/PLATFORM_PARITY.md`. There is no macOS host, so nothing native can be compiled or run here.

**Decision.**
- `HealthKitActivityAdapter` (Infrastructure) implements `IActivityRecordSource` and `IActivityConnectionPort` over the string seam `IHealthKitBridge`, mirroring D-068. Namespace `ios.healthkit`. Samples are immutable in HealthKit, so a credit is revision 1 and a deletion revision 2 (re-delivery is a stale-revision no-op). Deletion lines carry the interval the bridge remembered, because `HKDeletedObject` has only a UUID. The local day comes from `HKMetadataKeyTimeZone` when present (D-103 rules). Malformed or duplicate lines fail the whole fetch (D-063). Ids reuse the Health Connect id bounds.
- Read access is not observable in HealthKit. An answered dialog (`unnecessary`) maps to Granted so reads can run, and `ReadAccessConfirmed` becomes true only after a read returns a sample.
- `IosHealthKitBridge` (Shell, `DllImport("__Internal")`, editor stub) and `Assets/Plugins/iOS/WalkGameHealthKitBridge.swift` (`@_cdecl`, read-only stepCount, sample query plus anchored query for deletions, 10 s timeouts, deletions re-emitted while in the window as in D-069).
- **Not composed.** `ShellBootstrap` is unchanged, so iOS players keep the D-108 unsupported state until a macOS build and device check exist. Entitlement (`com.apple.developer.healthkit`) and `NSHealthShareUsageDescription` are documented in `docs/HEALTHKIT_SETUP.md`, not applied.

**Tests:** .NET `HealthKitActivityAdapterTests` (35 cases with theories: mapping, offsets, malformed/duplicate/oversized lines, window and order, request-status mapping, unavailable/not-asked never read, bridge errors, read-access honesty, one read per fetch, credit once and deletion reversal once across a restart, per-provider identity). .NET 774/774 and EditMode 174/174 merged with main after D-111 (Unity-editor-tested; the editor compiles only the stub branch of `IosHealthKitBridge`). Evidence: `docs/evidence/d112-healthkit/`.

**Labels.** Adapter: automated-verified (headless). Swift and the iOS P/Invoke branch: UNVERIFIED (never compiled). No iOS build claim.

**Gaps:** macOS build; composition and "connected" copy that respects unobservable read access; iPhone+Watch double counting; anchor and interval maps are lost on reinstall; the interval map grows with every sample (pruning to the sync window is not done).

## D-113 — HealthKit prep: seen-samples pruning, iPhone/Watch dedup design, reinstall behaviour (2026-10-16)

**Context.** D-112 left three HealthKit gaps that can be worked on without a Mac: the Swift bridge's interval map grows with every sample, iPhone and Apple Watch samples double count, and the reinstall behaviour of the `UserDefaults` state was only a one-line note.

**Decision.**
- `HealthKitSampleLedger` (Infrastructure) is the tested model of the bridge's `intervals` / `deleted` maps. `Prune(now)` drops entries that ended before now − 37 days (the 30-day shell read window, D-107, plus a 7-day clock margin) and caps each map at 50,000 entries, oldest end first. Pruning never changes what a read window emits. The Swift bridge mirrors the rule (UNVERIFIED, never compiled).
- `HealthKitSourceDedup` is the dedup design: fixed priority Watch > iPhone > other (only `com.apple.*` collectors count as Watch/iPhone); a sample keeps the share of its steps outside time covered by strictly higher-priority samples, prorated and floored; same-priority samples are not merged; the result is order-independent and never above the input. **Not wired**: the bridge protocol has no source field yet; wiring needs a sixth field and its own D-entry.
- Reinstall behaviour is documented in `docs/HEALTHKIT_SETUP.md`: every failure mode keeps a credit at worst, never double credits.
- The adapter stays unwired (D-112); no change to save format, content or the Android path.

**Tests:** .NET `HealthKitPrepTests` (15 cases: retention constants, prune boundary, prune preserves emitted deletions including a clock moved back by the margin, cap eviction order, unknown/pruned/resurrected UUIDs, classification theory ×5, full and partial overlap, untouched non-overlaps, three levels with point samples, 200-round order-independence and bounds). .NET 789/789; EditMode 175/175 (Unity-editor-tested; evidence `docs/evidence/d113-healthkit-prep/`).

**Labels.** C# rules: automated-verified (headless). Swift pruning mirror: UNVERIFIED. Reinstall table: documented reasoning, UNVERIFIED on a device.


## D-114 — HealthKit bridge source field; iPhone/Watch dedup wired in the adapter (2026-10-16)

**Context.** D-113 designed `HealthKitSourceDedup` (Watch > iPhone > other) but left it unwired because the bridge protocol had no source field.

**Decision.**
- Credit lines gain an optional sixth tab field `source`: `watch` | `phone` | `other`. Absent or empty means `other`; any other token fails the whole fetch as a protocol error (D-063). Five-field lines behave exactly as before, so an older bridge is not broken.
- The Swift bridge classifies natively with the D-113 rule (only `com.apple.*` bundle identifiers count, by `HKDevice.model` prefix `Watch` / `iPhone`) and always emits the field.
- `HealthKitActivityAdapter.FetchRecords` runs `HealthKitSourceDedup.Resolve` over the in-window credit lines of one response. Each credit's quantity becomes its deduplicated share (never above the bridge count). A share of 0 is omitted rather than sent as a zero-quantity record (which the trust pipeline would reject); deletions and non-positive counts pass through unchanged.
- The adapter stays NOT composed (D-112). No save, content or Android change.

**Known residuals (logged under KI-022).** Dedup is per response, and the pipeline is exactly-once by sample id and revision. A share credited before a higher-priority overlapping sample arrives is not reduced later (worst case: the pre-D-114 double count for that window, never more). If a covering Watch sample is deleted, an omitted phone sample is credited in full on the next read, but a partially credited one keeps its earlier share (under-credit). Both need device data to size.

**Tests:** .NET `HealthKitSourceFieldTests` (token theory ×4, unknown tokens ×4 fail closed, five-field compatibility, full overlap omits the phone sample, partial overlap prorates, deletions pass through, non-positive counts untouched, 200-round randomised order-independence and never-above-input).

**Labels.** C# parsing and wiring: automated-verified (headless). Swift classification: compiled on hosted macOS once CI runs on this PR (`ios-bridge-compile`); runtime and device UNVERIFIED.

## D-115 — Unity iOS Xcode export verified by an unsigned hosted-macOS xcodebuild (2026-10-16)

**Context.** KI-022 needed proof that the Unity iOS app (not just the Swift file) compiles and links.

**Decision.**
- The Linux box exports the Xcode project with the free iOS Build Support module (`tools/unity/export-ios-xcode.sh`, `ShellBuild.BuildIosXcode`: IL2CPP, device SDK, no signing, no team id). The script restores `ProjectSettings.asset` (KI-015).
- Transport: `tools/unity/push-ios-export.sh <tag>` pushes the export as <45 MB xz chunks on a throwaway `ios-export/<tag>` branch (never main). `.github/workflows/ios-xcode-export-verify.yml` runs only on those branches (or manual dispatch) on `macos-latest`: reassemble, SHA-256 check, unsigned `xcodebuild` for a generic iOS device, then checks the four `wg_hk_*` exports and the HealthKit link. Free (public repo, standard runner); no Unity, licence or secrets on CI; no artifacts.
- No wiring: the adapter stays NOT composed (D-112); no save, content or Android change.

**Result.** BUILD SUCCEEDED first time (run 38920379246, Xcode 26.6); no fix needed.

**Labels.** Xcode-built unsigned on hosted macOS, not device-tested. Signing, entitlement, usage string, runtime: UNVERIFIED.

**Superseded transport (D-116).** The public `ios-export/<tag>` transport published Unity's proprietary engine library (`libiPhone-lib.a`) in this public repository. The owner deleted `ios-export/grok2251-a`; `push-ios-export.sh` is retired and the workflow is manual-only. The run 38920379246 result above stays valid evidence.

## D-116 — Unity engine binaries must never be pushed to the public repository (2026-10-16)

**Context.** D-115 moved a Unity iOS Xcode export to hosted macOS by pushing it, as xz chunks, to the throwaway branch `ios-export/grok2251-a` of this public repository. That export contains Unity's proprietary iOS engine library (`Libraries/libiPhone-lib.a`) and other Unity binaries. Publishing them is a licence and redistribution risk. The owner deleted the branch. An audit for this decision also found that commit `3ca344e` had accidentally tracked the repo-root Unity editor cache (`Library/`, 544 files including Unity-compiled `Unity.Multiplayer.Center.*.dll`/`.pdb`, `Library/Artifacts`, `Library/Bee`) and `Logs/`.

**Decision.**
- Unity engine binaries (player libraries such as `libiPhone-lib.a`, `UnityFramework`, `libil2cpp*`), exported player or Xcode trees, chunked export payloads and editor caches (`Library/`, `Temp/`, `Logs/`) must never be committed or pushed to this repository, on any branch.
- `tools/unity/push-ios-export.sh` is retired: it prints the reason and exits 3.
- `.github/workflows/ios-xcode-export-verify.yml` is manual-only (`workflow_dispatch`; the `ios-export/**` push trigger is removed). It first runs the guard, then fails closed in "Obtain export" until a PRIVATE transport (a private repository or private storage, at no paid cost) is designed in its own D-entry.
- Guard: `scripts/verify-no-engine-binaries.sh` checks tracked file names (engine payload paths, editor caches, native extensions `.a/.so/.dylib/.dll/.pdb/.framework/...`) and content magic (ar static library, Mach-O, ELF, xz). `--history` checks every path ever committed on the current history for engine payloads. `--self-test` proves it can fail (7 cases). CI runs all three; the pre-commit hook runs `--staged`.
- The root `Library/` and `Logs/` are untracked and git-ignored. `export-ios-xcode.sh` stays (it writes only to the git-ignored `src/WalkGame.Unity/Build/`).

**History check.** `origin/main` history (c616b22) never contained `libiPhone-lib.a`, `ios-export/` or any chunk (`--history` PASS); the payload commit was only on the deleted branch. The accidental `Library/` files do remain in main history from `3ca344e`. Removing them would need a history rewrite and a force-push of main, which this project forbids for agents. That is an owner decision (morning queue). They are Unity package editor assemblies and caches, not player engine libraries.

**Labels.** Guard automated-verified (self-test plus a clean run on this tree). Whether GitHub still serves the deleted payload commit by SHA, or keeps it in cached Actions data, is UNVERIFIED and outside the repository (owner/GitHub support).

## D-117 — ProjectSettings.asset is only committed under a named decision (2026-10-16)

**Context.** KI-015: Unity tooling rewrites the tracked `src/WalkGame.Unity/ProjectSettings/ProjectSettings.asset` (platform switch, `WG_ANDROID_ARCH`, iOS export). The rule "never commit it by accident" was documentation only, so nothing stopped an accidental commit.

**Decision.**
- `scripts/verify-project-settings.sh --staged` runs in the pre-commit hook. It refuses a staged change to the file unless `SWG_PROJECT_SETTINGS_DECISION=D-NNN` names a heading that exists in `docs/DECISIONS.md`. This is the sanctioned, deliberate path, not a hook bypass.
- CI runs `--range <merge-base with origin/main>..HEAD`. Every commit in the range that changes the file must carry the trailer `Project-Settings-Decision: D-NNN` naming an existing decision.
- `--self-test` (6 cases) proves that both modes refuse and allow correctly. CI runs it.
- Past changes (90f0207, 9b40267, 9be1302) predate this rule and are on main; the CI range starts at the merge base, so they are not re-judged.

**Labels.** Automated-verified (self-test; CI step reproduced locally with `verify-ci-steps.sh`). No game, save or content change.


## D-118 — Optional landmark letters (Region 1 storyline depth) (2026-10-16)

**Context.** Storyline depth was requested. Region 2 is not approved, so it has to come from Region 1.

**Decision.** The OpenSpec change `add-landmark-letters` is agent-approved as Region 1 / shell scope (as D-103). Six letters from Nell Hartley are each tied to one project (trailhead, river intake, workshop, east wetland, canopy walkway, valley survey). They are derived from project completion only (`GameSession.GetLetters()`), with no save or content-fingerprint change. They are shown in the Story journal, and counted in Discoveries, only after the first find. The copy is calm (no streak, guilt or pressure wording, test-pinned).

**Labels.** Automated-verified (.NET 806/806); Unity-editor-tested (EditMode 181/181). Device UNVERIFIED (KI-002).

## D-119 — Optional gentle daily reminder (2026-10-16)

**Context.** The reminder preferences existed (D-042 era, default off) but nothing delivered them.

**Decision.** The OpenSpec change `add-gentle-reminder` is agent-approved as Region 1 / shell scope (as D-103). The reminder is wanted only when both `NotificationsOptIn` and `DailyReminderEnabled` are on, and it is scheduled only when the OS permission is granted. It is re-synced on boot, resume and toggle, and the game never re-asks on its own. Android delivery uses an inexact repeating RTC alarm (never wakes the device) and a low-importance channel. There is no boot receiver and no exact-alarm permission. The copy is fixed and calm.

**Labels.** Unity-editor-tested with a fake (EditMode 186/186); compiled into the arm64 APK (`docs/evidence/apk-d119/`). Device delivery UNVERIFIED (KI-002). Residuals: a device reboot clears the alarm until the game is next opened; there is no time picker yet (it uses the stored 08:00 default); no iOS delivery.

## D-120 — Localization readiness: one shell string table plus a ratchet (2026-10-16)

**Context.** Player strings were literals spread across the shell, so a translation would have to edit code.

**Decision.** `WalkGame.Shell.PlayerStrings` is the single table for shell player copy (`T(key)`, and `F(key, args)` with invariant `{n}` templates; a missing key yields an empty string, never the raw key). Migrated so far: BootRouter, `ReminderCopy`, the reminder lines (D-119), the letters lines (D-118) and the Settings status lines. `LocalizationReadinessTests` (EditMode, 5) scans `Assets/Shell` source. Migrated files must have zero multi-word literals; other files are capped at their current count and may only go down; `// l10n-ignore: reason` marks non-player literals. Every key used in code must exist in the table. `CopyPolishTests` now scans `PlayerStrings.cs` for guilt wording instead of BootRouter. A full migration is tracked as KI-023.

**Labels.** Unity-editor-tested (EditMode 191/191). No save, content or behaviour change; every rendered string is byte-identical.

## D-121 — Reminder time picker and re-arm after a phone restart (2026-10-17)

**Context.** D-119 residuals: the reminder always fired at the stored 08:00 default, and a phone restart cleared the alarm until the game was next opened.

**Decision.** OpenSpec `add-reminder-time-and-reboot` (agent-approved, Region 1/shell scope, as D-103/D-119). While the reminder is on, Settings offers "Remind me earlier/later, around HH:MM": 30-minute steps inside 06:00–21:00, never moving the wrong way, hidden at the window edge. A move saves the existing `DailyReminderMinutesOfDay`, re-schedules once, shows a calm note, and never re-asks permission. Android: a non-exported `ReminderBootReceiver` (`BOOT_COMPLETED` only, normal install-time `RECEIVE_BOOT_COMPLETED`) re-arms the same inexact RTC alarm only if `ReminderBridge` had armed it and notifications are still allowed; `cancel` clears the flag. Battery: one AlarmManager call at boot; no service, wake lock, network or exact alarm.

**Save compatibility.** No save or content change (the time field already existed, D-119). Installs from before D-121 have no armed flag, so the receiver does nothing until the game next opens and re-syncs.

**Labels.** Unity-editor-tested (EditMode 205/205); Java compiled into an arm64 debug APK (`docs/evidence/apk-d121/`). Device boot and delivery UNVERIFIED (KI-002). No iOS delivery.

## D-122 — Localizing application/content copy without touching content hashes or saves (2026-10-17)

**Context.** KI-023: the shell table (D-120) is complete (every shell file at zero, #109). Application/content copy is not: project, landmark and region titles, chapter titles/bodies/hints, letters, discovery copy and glance phrases. Several content "key" fields (`TitleKey`, chapter `BodyKey`/`HintKey`) actually hold the authored English, and some of that text is copied into stored return summaries. Renaming those fields or replacing their values with real keys would change content definitions, the content-compatibility gate and potentially saved summary text.

**Decision.** The authored English stays where it is, unchanged, as the canonical source. Localization is a presentation-only overlay, `WalkGame.Application.Content.ContentText`: a flat table keyed by a stable content id plus a field (`letter.trailhead#body`, later `proj.clear-trailhead#title`, `chapter.<id>#hint`, `disc.<key>#text`). `Resolve(id, field, authoredEnglish)` returns the overlay entry or, if missing or blank, the authored English, so a partial translation never shows a raw key or an empty line. `ContentText.English` has no overlay and returns the authored string instance itself. `GameSession.ContentLocale` defaults to English; it is never persisted, validated or read by canonical state. Stored summary text is out of scope: it stays as recorded (a later slice may re-derive summary lines from ids at render time).

**First slice.** Letters (place and body) resolve through `ContentLocale` in `GetLetters`. `ContentTextTests` (4): English is identity, missing/blank entries fall back, the overlay is copied at construction, and a pseudo-locale changes letter text while save bytes, `ContentVersion` and catalog titles are unchanged.

**Next slices.** Project/landmark/region titles (resolve by id in read models, not in `PlayerCopy.Title`, which only sees text), chapters, discovery copy, glance phrases; then a language preference (preferences file, not the save) and a locale loader. No translation ships yet.

**Save compatibility.** None: no save, schema or content change.

**Slice 2 (2026-10-17, grok0157).** Project, landmark and region titles (`GetHome`, `GetProjects`, `GetRegion`, by stable id + `title`), chapter title/body/hint (`GetStory`, by chapter id), discovery titles (`GetDiscoveries`) and D-086 discovery body/origin copy (`GameSession.DiscoveryCopy(id, field, key)`, used by the Discoveries screen) resolve through `ContentLocale`. Read-model field names and id/key fields are unchanged. `ContentTitleOverlayTests` (2): English equals the catalog text; a pseudo-locale changes every routed string but not ids, save bytes, `ContentVersion` or the catalog. Remaining: glance phrases, expedition/producer/care-program titles, stored return-summary lines, language preference.

**Slice 3 (2026-10-17, grok0215).** Producer titles (`GetRegion`), the care-program title (`GetRegion().PostCompletionCare`) and expedition titles (`GetExpeditions`) resolve by stable id. Glance phrases resolve through `glance.<name>#text` entries. Patterns such as `underway` = "{0} is underway ({1} of {2})." and `landmark-sentence` = "{0} {1}." allow word order to change, and a broken localized pattern falls back to the English pattern. Stage phrases use `glance.stage.<stage>`, activity sentences `glance.activity.<status>` and onboarding sentences `glance.onboarding.<step>`. `GlanceProjector.Project` takes an optional `ContentText` (default null gives identical English). Stored summary lines still use the English `FormatLandmark` and are never translated. Tests: `ContentGlanceOverlayTests` (5, .NET) and the EditMode `DiscoveriesLocaleRenderTests`. Evidence: `docs/evidence/ki023-discoveries-locale/`. Remaining: stored return-summary lines, expedition/care descriptions, single-word labels and a `PlayerStrings` locale, and the language preference.

## D-123 — Reminder time maths and restart path: property tests and one edge-case fix (2026-10-17)

**Context.** D-119/D-121 computed the next reminder trigger inline in `ReminderBridge` (Android-only, untestable headless) and the restart re-arm decision was untested. An audit also found that a damaged or hand-edited preferences file with an out-of-range reminder time was applied as-is; "turn on the reminder" then re-submits that stored time, which fails validation every time (a dead end).

**Decision.** (1) `ReminderTime.java` (pure, no Android imports) holds `nextTriggerMillis(now, minutesOfDay, zone)` and `shouldRearm(...)`; `ReminderBridge` uses both. The trigger is the first instant strictly after now with that wall-clock time, re-derived per calendar day, so a daylight-saving gap moves it forward the same day instead of skipping it. `shouldRearm` also refuses blank title/body. (2) `ReminderTimeTest` (headless, run by `tools/android/test-bridge-logic.sh`, which CI already runs): 200,000 seeded cases across every JDK time zone, 2000–2040 (strictly future, within a day plus DST shift, on the minute, wall time exact unless in a DST gap, idempotent, next one about a day later), fixed Europe/London spring/fall cases, bounds, and a 20,000-case restart decision table; 1,020,030 checks. (3) `LocalPreferencesStore` ignores an out-of-range stored reminder time (keeps the 08:00 default). `ReminderPropertyTests` (2): 400 seeded `SetDailyReminder` calls with restarts every 20 (accepted iff 00:00–23:59; a rejected call changes nothing; a restart reads back the last accepted value), and 200 damaged stored times (the second test fails without the fix).

**Save compatibility.** No save or content change. The preferences file format is unchanged; only values that `SetDailyReminder` could never write are now ignored on load.

**Labels.** Java logic headless-tested; the APK compile is part of the RC run. Device delivery UNVERIFIED (KI-002).

## D-124 — System.Text.Json 10.0.13 (LTS) replaces 8.0.5 in the core and Unity plugins (2026-10-17)

**Context.** KI-024: STJ 8.0 support ends in November 2026. STJ is the only shipped runtime package, and it is published into the Unity plugin folder (D-007, D-064). Dependabot #5 was closed because nothing in Unity had been checked.

**Decision.** Pin `System.Text.Json` 10.0.13 (the .NET 10 LTS line, supported to Nov 2028) in `Directory.Packages.props`. `tools/unity/sync-plugins.sh` also copies `System.IO.Pipelines`, which STJ 10 references on netstandard2.x. `link.xml` is unchanged: STJ and Encodings.Web stay preserve="all", and the linker keeps the Pipelines members STJ uses. Dependabot still ignores STJ majors; minor and patch updates on 10.x stay manual until the Unity lane runs in CI.

**Evidence.** `docs/evidence/stj10-grok0215/RESULTS.md`: .NET 815/815, EditMode 205/205, PlayMode 4 passed + 1 ignored (capture), arm64 IL2CPP APK built, iOS Xcode export built locally (not pushed, D-116), stripped assemblies inspected, dependency-health PASS.

**Save compatibility.** No save, schema or content change. The JSON format is produced by the same codec options, and the full .NET save-compat and fixture suites pass unchanged.

**Residual.** IL2CPP runtime save/load on a device and a macOS xcodebuild of the export are UNVERIFIED (KI-002, D-116).


## D-125 — Titles are typed as text, not sniffed with a dot heuristic (2026-10-17)

**Context.** `PlayerCopy.Title` and `GlanceProjector.DisplayTitle` treated any title containing '.' as a stable id and replaced it with "A restoration project". With the D-122 overlay a translated title can legitimately contain a dot ("St. Ives"), so it vanished.

**Decision.** Explicit typing: every read-model title field (`*.TitleKey` in read models) carries display text only (authored English or its overlay), and an unknown title is blank, never an id. The one id fallback (`HomeReadModel.QueuedRow` for an unknown project) now yields an empty string. `PlayerCopy.Title` and `DisplayTitle` only check for blank; the dot test is gone. Stored return-summary text (`ReturnSummaryComposer.Title`, id fallback for a missing definition) is unchanged: it is recorded text, never passed through `PlayerCopy.Title`, and changing it would alter stored copy.

**Tests.** `TitleTypingTests` (.NET, 2: a dotted translated title reaches the glance, which fails without the fix; read-model titles never equal a stable id) and `TitleTypingShellTests` (EditMode, 2). .NET 822/822, EditMode 209/209 (Unity-editor-tested).

**Save compatibility.** None: no save, schema or content change.

## D-126 — Shell strings take a locale; no language setting yet (2026-10-17)

**Context.** KI-023: content copy resolves through the D-122 `ContentText` overlay, but `PlayerStrings` (D-120) was English only, so a locale could translate titles but not the shell around them.

**Decision.** `PlayerStrings.Locale` (a `ContentText`, default `ContentText.English`) uses the same rules as D-122: entries keyed `ui.<key>#text`; a missing or blank entry falls back to the English table; a broken localized template in `F` falls back to the English template; a key absent from the English table stays empty (an overlay cannot add keys). English returns the table's own string instances (byte-identical). `ShellController.SetLocale(locale)` sets both the session content locale and the shell locale; `null` restores English. Nothing is persisted.

**Language setting: not added.** No real translation exists. A player-facing setting would offer one choice, and a hidden setting would mean a preferences field and a loader with nothing to load. The setting arrives with the first real translation (a preferences-file field, not the save, plus a locale loader); until then `SetLocale` is the only entry point and it is used by tests and capture only.

**Tests.** `PlayerStringsLocaleTests` (EditMode, 5): English identity, replacement and fallback, template reorder and broken-template fallback, no added keys, `SetLocale` drives shell + content and leaves save bytes unchanged. EditMode 214/214 (Unity-editor-tested).

**Save compatibility.** None.

## D-127 — Projects lists what you can act on first (2026-10-17)

**Context.** Region 1 mid/late-game review with editor-rendered PNGs (`docs/evidence/region1-midlate-polish/`). At day 30 the Projects screen showed nine restored projects before the active project and the ready-to-start options.

**Decision.** Projects rows are ordered being restored → waiting (by queue position) → ready to start → restored, and a restored row drops the "(N of N vitality)" suffix. Presentation only. The capture gains `WG_CAPTURE_STATE=mid|late` (with `WG_CAPTURE_DAY`) via `CompletedRegionFixture.BuildUntil`.

**Tests.** `MidGamePolishTests` (EditMode). EditMode 215/215 (Unity-editor-tested). No save or content change.

## D-128 — Shell sync pipeline: one run at a time, and a calm exception boundary (2026-10-17)

**Context.** Hardening audit of the Unity-to-domain boundary. Unity is single-threaded, but a platform callback (permission dialog, OS pause/resume, a bridge that pumps messages) can re-enter `ShellController` while `Boot()`/`Resume()` or an action is still running. Findings: (1) a resume delivered during the boot sync ran a nested `Continue()` + provider read inside the outer one; (2) a tap delivered mid-pipeline ran a second action against half-updated state (a stray `delete:confirm` would erase the world mid-sync); (3) `GameSession.PersistOrThrow` throws `IOException` when a save cannot be committed, and nothing caught it, so a full disk or revoked storage threw out of the UI Toolkit callback and left a half-drawn screen; (4) `SyncLiveSource` caught only IO/access/invalid-operation, so any other bridge exception (a JNI `AndroidJavaException`, `FormatException`) aborted the boot; (5) a double-tapped "Start" on an already queued or active project showed an error.

**Decision.** `ShellController` runs one pipeline at a time (`IsBusy`). A `Resume`/`Boot` arriving mid-pipeline is coalesced into ONE re-run afterwards (never nested, never looped); an action arriving mid-pipeline is dropped. `Boot` and `Invoke` go through one boundary (`Guarded`) that turns IO, access, invalid-operation and argument exceptions into the existing calm sentence ("That could not be done just now. Your progress is safe."), or BootError when no world is open; the session never reports a failed commit as success, so the committed save is untouched. The live-source read swallows any bridge exception (the next resume retries; D-063 still records IO-class failures durably). Re-queueing a queued/active project is a no-op. App pause mid-save: saves are synchronous inside one call, so a pause cannot interleave in-process; a process kill mid-write remains covered by the atomic store (D-040/D-052).

**Tests.** `SyncReentrancyTests` (EditMode, 6): resume storm during boot sync → exactly two fetches; stray tap mid-sync dropped; double-tapped Start calm; non-IO bridge exception never aborts boot; unwritable save directory → calm error, save bytes unchanged, shell not left busy; 50× resume/nav storm reloads cleanly. EditMode 220/220 + 1 ignored (Unity-editor-tested); device UNVERIFIED (KI-002).

**Residual.** After a failed commit the in-memory state can be ahead of disk until the next successful write (pre-existing session contract; closed by D-131). Null scan: shell entry points already reject null/empty action ids (D-084); no new unhandled null path found in `ShellController`/`ShellBootstrap`.

**Save compatibility.** None.

## D-129 — Region 1 epilogue: closing letters, a final journal entry, valley notes (2026-10-17)

**Context.** After Millbrook's closure milestone the Story journal ended on one static line. OpenSpec `add-region1-epilogue` is agent-approved as Region 1 scope on the same basis as D-103/D-118. D-011 and the ROADMAP post-MVP gate apply to Region 2 only; this change adds no Region 2 content or hook, and `add-region2-storyline` stays PROPOSED.

**Decision.** `Region1Epilogue` (Application content) has a final journal entry, two closing letters from Nell (day 0 and day 7 after completion) and eight valley notes (one per 30 days, then the "In the valley now" line cycles forever). `GameSession.GetEpilogue()` is a pure projection of `RegionCompletedAtUtc` and the trusted clock. It returns `None` before completion. A backward clock shows the day-0 epilogue, and nothing that was shown disappears while the clock moves forward. The Story journal adds an Epilogue block. Notes describe the valley without naming calendar seasons, so they read true in either hemisphere. There are no counts, "N of M", notifications, rewards, streaks or timers.

**Tests.** `EpilogueTests` (.NET) and `EpilogueJournalTests` (EditMode). The read-model sweep in `M5H1ContractHardeningTests` now counts 15. Counts are in the PR. Device UNVERIFIED (KI-002).

**Save compatibility.** None. There is no save, schema or content-fingerprint change, and existing completed saves show the epilogue at once.
## D-130 — Null-input scan of GameSession entry points (2026-10-17)

**Context.** This follows the D-128 boundary audit and covers the Application side. `EnqueueProject(null)` and `ActivateQueuedProject(null)` threw `ArgumentNullException` from a dictionary lookup. An `IActivityRecordSource` that returned `null` instead of an empty list threw out of `IngestFromSource`. The shell's D-128 boundary does not catch `ArgumentNullException` from the live-source read, but its catch-all swallowed it, so the result was silent but unrecorded.

**Decision.** A null or empty project id is now a calm `UnknownProject` (enqueue) or `NotQueued` (activate) result. A null batch from a provider is read as "nothing new" and ingested as an empty batch. `DequeueProject`, `MarkDiscoveryReviewed` and `DiscoveryCopy` already handled null and are now pinned by tests.

**Tests.** `NullBoundaryTests` (6). Device UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-131 — Failed-save gap: mark unsaved, retry on resume, calm notice (2026-10-17)

**Context.** D-128 residual: after a failed commit the in-memory state is ahead of disk until the next successful write. A resume re-ran `Continue()`, which reloaded disk and silently dropped that progress, and a failed commit inside the live sync was swallowed with no notice.

**Decision.** `GameSession` tracks `HasUnsavedChanges` (set when `PersistOrThrow` fails with IO or access denied; cleared by any successful commit, a reload, a new game or erase) and offers `RetryPendingSave()` (writes the whole in-memory state; false, never throwing, while storage still fails). `ShellController.BootCore` retries first on resume. If the retry fails it keeps the in-memory world and does not reload. Any failure that leaves state unsaved shows `shell.notSavedYet`: "Your latest progress could not be saved just now. It is kept here, and saving will be tried again when you come back." No credit is lost or doubled. The ledger entry and processed-record ids travel with the state, so a retry or later commit lands them once. A reload drops them, and the next provider read credits them once.

**Tests.** `FailedSaveGapTests` (.NET, 6). EditMode: `D131_unsaved_progress_is_retried_on_resume_and_lands_exactly_once`. The D-128 unwritable-save test now expects the D-131 notice (intended behaviour change, not a removed test). Device UNVERIFIED (KI-002).

**Save compatibility.** None (no schema or content change).

## D-132 — Kill mid-save at each write step (2026-10-17)

**Context.** D-128 argued that an app pause cannot interleave inside the synchronous commit and that a kill is covered by the atomic store (D-040/D-052), but no test cut the write at every step of `AtomicFileSaveStore.WriteAtomic`.

**Decision.** `KillMidSaveTests` reproduces what is left on disk after each completed step, plus a torn temp write: none, a half-written `save.tmp`, `save.tmp` written, backup temp copied, old backup deleted, backup promoted, primary deleted, and primary promoted. For each cut, a fresh session must boot `Loaded` or `RecoveredFromBackup` with either the old or the new credit total (never a partial or fabricated world). Replaying the same activity must then land the credit exactly once, and a second replay must credit nothing. A torn backup temp with an absent primary still recovers the previous generation. No production change was needed.

**Tests.** `KillMidSaveTests` (.NET, 9). Device UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-133 — Epilogue journal PNG and the post-restoration progress line (2026-10-17)

**Context.** The first editor-rendered capture of the D-129 epilogue journal (a completed region through `CompletedRegionFixture`) showed "9 of 10 chapters unlocked. Restoring the valley reveals the next one." That line is wrong once the valley is restored, because the last chapter needs a season of tending.

**Decision.** `ShellController.StoryProgressLine(unlocked, total, regionCompleted)` uses `shell.story.progressTending` ("… Tending the restored valley reveals the last one.") after restoration. The capture gains `WG_CAPTURE_SCREEN=EpilogueJournal`. Evidence: `docs/evidence/d133-epilogue-journal/` (editor-rendered, before and after).

**Tests.** EditMode `D133_progress_line_after_restoration_points_to_tending_not_restoring`. Device UNVERIFIED (KI-002).

**Save compatibility.** None (presentation only).

## D-134 — Resume during failing saves still runs offline catch-up (2026-10-17)

**Context.** D-131 gap: when `RetryPendingSave()` failed on resume, `ShellController.BootCore` returned early. It kept the in-memory world but skipped the offline catch-up a boot runs, so time away produced nothing and no return summary was shown until storage recovered.

**Decision.** `GameSession.CatchUpWithoutReload()` runs the same `OfflineAdvancer.Advance` + pending-summary composition as `FinishBoot`, on the in-memory state, then tries one whole-state commit (never throws for I/O; returns true when durable). Exactly once comes from the existing monotonic checkpoints (`LastAdvancedUtc`, per-producer `LastTickUtc`): repeating it at the same clock credits nothing, stepwise catch-up equals one catch-up over the span, and a backward clock never regresses. The shell calls it on the failed-retry path and routes to Guide or ReturnSummary as a boot would; the calm `shell.notSavedYet` notice stays while storage fails.

**Tests.** `ResumeCatchUpGapTests` (.NET, 6: matches a healthy reboot, repeat is a no-op, stepwise = single, recovery commits once and replay credits 0, backward clock, no state fails closed). EditMode `D134_resumes_during_failing_saves_catch_up_in_memory_and_land_once`. Device UNVERIFIED (KI-002).

**Save compatibility.** None (no schema or content change).

## D-135 — Long random play property tests for the core invariants (2026-10-17)

**Context.** Exactly-once credit, durable accomplishments and monotonic progression were each tested in isolation, but never across long mixed sequences.

**Decision.** `LongPlayInvariantPropertyTests` runs 12 seeded sequences of 300 random steps: new batches, replays of delivered records, forward and backward clock jumps with D-134 in-memory catch-up, project queueing, summary acknowledgements, reloads, and ~10% injected commit failures that are then retried. After every step: a replay credits 0, lifetime credited Vitality never decreases, completed projects and unlocked chapters only grow, and nothing stays unsaved once storage is healthy. At the end a reload reproduces the same credit and completions, and replaying the whole history credits 0. Each seed must credit activity and complete at least one project, so the properties are not vacuous. No production change; no finding.

**Tests.** `LongPlayInvariantPropertyTests` (.NET, 12). Device UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-136 — Region 1 polish: Guide and Settings at 200% text (2026-10-17)

**Context.** A 2x text-size review of the Guide and Settings screens with editor-rendered PNGs (`docs/evidence/d136-guide-settings-2x/`). At 200% the title scaled to 56 px and wrapped one word per line, pushing the Guide's only action below the fold.

**Decision.** `ShellView.TitleSizeFor(k)` grows titles at half the body rate (28 px at 100%, 42 px at 200%) and keeps them at least 10% larger than body text. Body text and touch targets still scale fully (D-073), so the 200% resize requirement holds. Settings scrolls at 2x, and every action stays reachable. No other findings.

**Tests.** EditMode `D136_title_size_grows_at_half_rate_and_stays_above_body`. The large-text test now expects `TitleSizeFor(2f)` and still asserts the title is larger than body (intended behaviour change, not a removed test). EditMode 227/227 + 1 ignored (Unity-editor-tested). Device UNVERIFIED (KI-002).

**Save compatibility.** None (presentation only).

## D-137 — No Inconclusive EditMode tests; RC check on 86f767a (2026-10-17)

**Context.** The release-candidate clean-clone check on `86f767a` (`scripts/validate-clean-clone.sh --with-unity`) passed every lane except Unity EditMode. That lane reported 227 passed and 1 inconclusive, and Unity exits 2 on an inconclusive test, so the lane failed. The inconclusive test was `SyncReentrancyTests.Double_tapped_start_on_a_project_is_a_no_op_not_an_error`. Since the Guide queues the only open project, Projects offers no `queue:` action after `Started()`, so the test's `Assume` gave up.

**Decision.** The test now walks the Guide until the Guide offers its own Start, double-taps that, and uses `Assert` (not `Assume`). The assertion is not weakened. Tests must not depend on `Assume` for reachable states. No production change.

**Tests.** EditMode 228/228, 0 inconclusive (Unity-editor-tested). .NET 871/871 (headless-tested). The other clean-clone lanes on `86f767a` passed: CI-step reproduction 19 ok, PlayMode 4 + 1 ignored, arm64 APK built. Device UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-138 — KI-023 remainder: stored summary lines and single-word labels (2026-10-17)

**Context.** D-122 left two English-only surfaces. Return-summary lines are stored in the save as composed English sentences, and the shell had single-word labels and short label templates such as "Home", "Continue", "Earlier: …" and "Step 1 of 5".

**Decision.** Display-only, with no save, schema or content-hash change.
- `SummaryLineText` matches a stored line against the closed set of composer sentence forms and the D-065 landmark glance form. On a match it re-renders the line from the overlay pattern `summary.<form>#text` (positional `{0}`), mapping a content title back to its stable id and localizing it. An unrecognised line, a missing pattern or an ambiguous title keeps the stored English. A broken pattern uses the English form. English is byte-identical.
- `ReturnSummaryReadModel.ItemRow.Text` stays the stored English. The shell's moment, celebration and chapter parsers keep reading it. The new `DisplayText` (and `PrimaryNextActionDisplay`) is what gets rendered. `SummaryLines` is display text.
- 19 `label.*` keys in `PlayerStrings` cover nav and screen titles, Continue, Earlier, Problem/Note, Start, Move up, diagnostics, resources, discoveries count, step, prologue/chapter headings and the story line.
- Residual: "expedition complete (+N type)" lines and expedition/care descriptions stay English.

**Tests.** .NET `SummaryLineOverlayTests` (9): English identical, every recognised line translated, the save bytes unchanged, fallbacks. EditMode `SummaryAndLabelLocaleTests` (2): a real 7-day return summary renders translated lines and labels, the line count is unchanged, saves are unchanged, and English is restored. .NET 880/880 (headless-tested), EditMode 230/230 (Unity-editor-tested). Device UNVERIFIED (KI-002).

**Save compatibility.** None. The content-compatibility gate is unchanged.
## D-139 — Hardening: sync under persistent save failure; performance re-measured (2026-10-17)

**Context.** D-131/D-134 cover a resume while saves fail. They did not cover a long run of ordinary syncs (redelivered and overlapping batches plus catch-up) while every write fails, followed by recovery. The last performance measurement (D-074) predates the localization overlays.

**Decision.** No production change was needed. `PersistentSaveFailureSyncTests` (3 seeds x 40 sync cycles) fails every write, redelivers random earlier records, and occasionally retries and catches up. The in-memory world must equal a healthy twin's reboot, credit must never go backwards, and the first successful `RetryPendingSave` must boot to the same world. A full redelivery after recovery must credit nothing. No finding. Performance was re-measured in the editor and headless (`docs/evidence/d139-perf-remeasure/`) with no regression; numbers are in PERFORMANCE_BUDGETS.

**Tests.** .NET +3 (headless-tested). Performance is editor-measured (Unity-editor-tested). Device UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-140 — Region 1 narrative polish: ecology and settlement progress as story (2026-10-17)

**Context.** Region progression summary lines read "The valley's ecology has reached stage 2." That is a mechanical number. The glance had to hide it behind the opaque "The valley changed." because it echoes a stage identifier.

**Decision.** `ReturnSummaryComposer.ProgressionLine` uses authored Region 1 sentences for the four ecology stages (timber cleared, reservoir, sedges, nesting islets) and the four settlement stages (workshop, water lines, market hall, power). These are the arcs' actual milestone projects. Example: "The settlement is lit: the square glows after dark." A stage outside the table keeps the numbered form. Only newly composed lines change. Already-stored lines are not migrated (D-065 precedent) and still localize through `summary.ecology-stage` and `summary.settlement-stage`. The new sentences take whole-sentence overlay entries (`summary.ecology.N`, `summary.settlement.N`). Region 1 scope only; nothing from `add-region2-storyline`.

**Tests.** `ProgressionNarrativeTests` (3): one sentence per authored stage, no digits or "stage", glance-safe lead, fallback, overlay. .NET 886/886 (headless-tested). EditMode 230/230 (Unity-editor-tested). Pacing evidence 7/7 reproduced, content compatibility passed. Device UNVERIFIED (KI-002).

**Save compatibility.** None (the stored text field is unchanged in shape, with no schema, content-hash or migration change).

## D-141 — KI-023 closed: reward expedition lines and expedition/care descriptions localize at display time (2026-10-17)
**Decision.** The last English-only application copy now goes through the D-122 overlay, display-only. `SummaryLineText` recognises the reward-suffixed form "{title} expedition complete (+{units} {Resource})." (`summary.expedition-complete-reward#text`, positional {0} title, {1} units verbatim, {2} resource; the resource name resolves from `resource.<ResourceType>#title`). `GameSession.GetExpeditions()` and the region read model's post-completion care row resolve descriptions from `<id>#description` (new `ContentText.Fields.Description`). Every miss falls back to the authored English: an untranslated line returns the stored bytes, a broken pattern falls back to the English form.

**Tests.** `ExpeditionTextOverlayTests` (3, headless-tested). .NET 889/889 (headless-tested). EditMode 230/230 and PlayMode 4 passed + 1 opt-in capture ignored on the new code (Unity-editor-tested, batch mode). Device UNVERIFIED (KI-002).

**Save compatibility.** None. Saves, schema, content versions and content hash are unchanged; stored lines stay English.

## D-142 — Save-migration hardening: atomic runner result and adverse old-save tests (2026-10-17)
**Decision.** `MigrationRunner.TryMigrate` now migrates into a local and replaces the caller's payload only when the whole chain succeeds. Before, a failure at step 2+ left the ref payload holding the step-1 output, contradicting the runner's "a failure preserves the original recoverable data" contract (the codec discarded it, so no live data loss, but the contract was false). A step that returns null is now a migration failure instead of a "successful" null payload.

**Tests.** `SaveMigrationAdverseTests` (13): hostile v1/old payloads (array or string root, text/object carry, junk producer rows, schema 0, negative, newer than the build, empty object) all decode without throwing and either fail closed with no state or pass as a full current-schema state; newer schema is never downgraded; checksum mismatch on an old save is rejected before any migration; mid-chain failure and null-returning step leave the caller payload untouched (both fail on the old runner). .NET 902/902 (headless-tested). EditMode 230/230, PlayMode 4 passed + 1 opt-in capture ignored (Unity-editor-tested, batch mode). Device UNVERIFIED (KI-002).

**Save compatibility.** None. No schema, codec format or migration output change; valid saves migrate exactly as before.


## D-143 — Adverse lifecycle in one live process: time-zone travel, clock jumps, revocation between syncs (2026-10-17)
**Decision.** Pin the in-process half of the adverse lifecycle matrix. Earlier suites cover process boundaries (D-072 `M8LifecycleHeadlessTests`) and a backward clock across reloads (`TemporalAnomalyTests`). `TimezoneTravelLifecycleTests` (5) keep one `GameSession` alive while: the player flies west (each walk counts on its own local day; vitality is identical to a no-travel control), crosses the date line east (no day invented or lost, survives reload), a replay re-labels a record's offset after travel (exactly-once no-op), the device clock jumps back two days and forward again mid-session (no double credit, checkpoint never regresses), and permission is revoked between two syncs then re-granted (progress kept, status PermissionDenied, only the gap credited). No finding; no production change.

**Verification.** .NET 907/907 (headless-tested). Device behaviour UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-144 — Health Connect failure-state UX: exhaustive status matrix and progress safety (2026-10-17)
**Decision.** Pin the activity-connection failure UX over every permission x availability x last-outcome x processed combination (120), not one sampled row per status. `HealthConnectFailureMatrixTests` assert: projection is deterministic; `ProviderUpdateRequired` dominates; the player is never told the walk is flowing (`ConnectedCurrent`) without a grant, an available source, processed data and a successful last fetch; an ungranted state never reads as waiting/retrying; every fixable state offers exactly its action (Connect / OpenSettings / RetryLater / UpdateProvider) and the rest none; D-068 holds. A session test walks every permission x availability state live and across a reboot: lifetime vitality and project investment never move, and recovery credits only the new walk. No finding; no production change.

**Observation (not changed).** A source that is `Unsupported` with a prior `Denied`/`Revoked` permission still projects `PermissionDenied` + OpenSettings (permission outranks unsupported unless never requested, D-068). On Android 14+ Health Connect is a system module, so this mostly arises on older devices after uninstalling the provider; deciding whether that should read as `SourceUnavailable` is a UX call for later, not a bug.

**Verification.** .NET 1028/1028 (headless-tested; +121 theory rows). Real Health Connect UI UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-145 — First-session payoff: display-time percent and distance to the next visible change (2026-10-17)
**Decision.** Very short sessions must pay off. The first project costs 300 Vitality (30,000 steps), so a 300–2,000-step first walk funded 1–7% and the summary only said "Vitality credited: N." Home and the return summary now show "{title}: {n}% restored. About {steps} more steps to finish it." (or "... to its next visible stage." for D-071 build-stage projects), derived at display time by `ProjectPayoff` from invested Vitality, authored cost and authored build stages. Percent is at least 1 once anything is invested and at most 99 before completion. OpenSpec: `openspec/changes/add-first-session-payoff/`.

**Rejected.** An onboarding-only cheaper first project: it changes authored content (content version/hash, pacing evidence, cadence gate) to fix a perception problem that display-time progress fixes without touching canonical state. Revisit only if device playtests say the copy is not enough.

**Tests.** `FirstSessionPayoffTests` (14, headless-tested), `FirstSessionPayoffShellTests` (1, Unity-editor-tested, batch mode). Pacing evidence 7 reproduced / 0 drifted; content compatibility passed. Device UNVERIFIED (KI-002).

**Save compatibility.** None. No save, schema, content version or content hash change.
## D-146 — Unsupported provider outranks a prior denial: "source unavailable", not "Open Settings" (2026-10-17)
**Decision.** Resolves the D-144 observation. When the activity source is `Unsupported` (Health Connect not installed/available), the status is `SourceUnavailable` whatever the permission, including a prior `Denied`/`Revoked`. Before, a prior denial projected `PermissionDenied` + OpenSettings, a dead end because Settings cannot grant access to a provider that is not there. Precedence is now: ProviderUpdateRequired, then Unsupported, then permission (D-068 order otherwise unchanged). An installed provider after a denial still offers OpenSettings.

**Tests.** `ActivityStatusProjectionTests.Unsupported_provider_never_asks_the_player_to_connect` rows for Denied/Revoked updated to the new contract (supersedes the D-068 denial row); `HealthConnectFailureMatrixTests` asserts every Unsupported row is SourceUnavailable with no action, plus 4 explicit D-146 cases. .NET 1032/1032 on main alone, 1046/1046 combined with D-145 (headless-tested). EditMode 231/231 on the combined tree (Unity-editor-tested, batch mode). Real Health Connect UI UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-147 — Region 1 accessibility invariants enforced on every shell screen (2026-10-17)
**Decision.** The D-095 audit sampled some checks on a subset of screens (2x text on 6 screens, reduced motion on 9). Five EditMode invariants now run over EVERY `ShellScreen` value (reached the way a player reaches it): (1) 2x text survives — every label and button scales to 2x body, wraps, and has no fixed height/width; (2) touch targets are at least 48 dp in BOTH dimensions at 1x/1.3x/2x and grow with the text; (3) every control has a semantic name (its action id), a visible label equal to its accessible name, and a matching screen-reader stop in the same order, the reader starts at the title header, nothing decorative is focusable, and the valley picture has alt text that the reader announces; (4) every rendered colour pair meets contrast against the theme (body text 7:1 on the page, indicator bars 3:1, button text 7:1 on its fill, button boundary 3:1); (5) no element on any screen at any scale carries a transition, so reduced motion holds by construction.

**Finding fixed.** Buttons had a minimum height but no minimum width, so a very short label could shrink a target below 48 dp wide. `ShellView` now sets `minWidth = ButtonHeight * k`.

**Tests.** `AccessibilityAuditTests.D147_*` (5). EditMode 236/236 (Unity-editor-tested, batch mode). Device screen readers UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-148 — Batch-mode PNG captures of the Region 1 text screens (2026-10-17)
**Decision.** The editor capture (`ValleyCaptureTests` via `capture-valley.sh`) gains `WG_CAPTURE_STATE=payoff` (first project queued + one 2,000-step walk, the D-145 first-session state), and `tools/unity/capture-text-screens.sh` renders Home (payoff, 1x and 2x), the return summary (payoff) and Settings to PNG in one detached run. Captures committed under `docs/evidence/d148-text-screens/` (small PNGs, ~30–70 KB each; run logs not committed). A guard in `ScreenTourCoverageTests` keeps the script and the payoff seeding in place.

**Observation (not changed here).** Home in the payoff state shows the project three ways ("Vitality credited: 20.", "... is underway (20 of 300).", and the D-145 percent line). Candidate polish: fold the "underway (x of y)" line into the payoff line when both are present.

**Verification.** Captures editor-rendered (llvmpipe). EditMode 237/237 (Unity-editor-tested, batch mode). Device UNVERIFIED (KI-002).

**Save compatibility.** None (test/tooling only).

## D-149 — Home no longer repeats a funded project three ways (2026-10-17)
**Decision.** Resolves the D-148 observation. Once the D-145 payoff line shows ("{title}: n% restored. About N more steps ..."), Home omits the restoring-now line "{title} is underway (x of y)." for the same project; before anything is invested the restoring-now line still shows. The return summary is unchanged. OpenSpec `add-first-session-payoff` spec gains a requirement + scenario and task 2.2. Home payoff captures in `docs/evidence/d148-text-screens/` re-rendered after the change.

**Tests.** `FirstSessionPayoffShellTests` extended (unfunded keeps restoring-now; funded drops it). EditMode 237/237 (Unity-editor-tested, batch mode). Captures editor-rendered. Device UNVERIFIED (KI-002).

**Save compatibility.** None (display only; no save, schema, content version or hash change).

## D-150 — new-agent-worktree.sh always runs origin/main's copy and self-checks its base (2026-10-17)
**Decision.** Root cause of a worktree cut from the shared checkout's stale HEAD e44b282 after #61: the session invoked `/workspace/simple-walk-game/scripts/new-agent-worktree.sh` from that stale checkout, and the shell executed *that checkout's* copy of the script, which predates the fetch-and-base fix (it ran `git worktree add -b <branch> <path>` with no start point, i.e. local HEAD). The fixed script on main was never executed. Fix: after fetching `<remote>/main`, the script compares its own blob to `<remote>/main:scripts/new-agent-worktree.sh` and, if different, re-execs the fetched copy (guarded by `SWG_WORKTREE_REEXEC`); after `worktree add` it verifies the new HEAD equals the fetched base or fails. Copies older than D-150 cannot self-update, so AGENTS.md documents running `git show origin/main:scripts/new-agent-worktree.sh | sh -s -- <campaign> <session>` from a possibly stale checkout.

**Tests.** Guard suite scenario 15 (stale invoking copy re-execs the fetched copy; worktree based on fetched main). Guard suite 51/51 (shell-tested on box).

**Save compatibility.** None (tooling only).

## D-151 — Home says where walked Vitality went and labels the balance unspent (2026-10-17)
**Decision.** Right after a walk Home showed "Vitality 0 · Materials 0 · Knowledge 0", which read as "nothing earned" because Vitality auto-invests into the active project. Home now shows "Your Vitality goes straight into {project}: {invested} invested so far." whenever the active project has any investment (not after region completion), and the balance line reads "Unspent: Vitality n · Materials n · Knowledge n" (`label.vitalityInvested`, `label.resourcesUnspent`). "Earned today" was rejected because the read model has no per-day earnings and adding one would go beyond display-only. Display only: no save, schema, content version or hash change.

**Tests.** `FirstSessionPayoffShellTests` extended (no invested line before funding; after a 2,000-step walk the unspent label and the invested line show and the bare "Vitality 0 ·" line is gone). EditMode 237/237 (Unity-editor-tested, batch mode). Captures in `docs/evidence/d151-text-screens/` (editor-rendered). Device UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-152 — Home groups the active project's facts together (2026-10-17)
**Decision.** Region 1 polish after D-151: the "Your Vitality goes straight into {project}: N invested so far." line now sits directly after the D-145 payoff line (percent + steps to go) instead of after the activity and story lines, so everything about the project being restored reads as one group; the unspent balance stays last. Display order only.

**Tests.** `FirstSessionPayoffShellTests` asserts the invested line immediately follows the payoff line. EditMode 237/237 (Unity-editor-tested, batch mode). Device UNVERIFIED (KI-002). The d151 captures predate this reorder (same lines, different order).

**Save compatibility.** None.

## D-153 — Text-screen evidence re-captured after D-152 (2026-10-17)
**Decision.** Re-ran `tools/unity/capture-text-screens.sh` on main `ab6cb8b` so the committed Home evidence matches the shipped D-152 line order. New set: `docs/evidence/d153-text-screens/` (Home 1x/2x, return summary, Settings). The d151 README points to it. Evidence only, no code change.

**Labels.** Editor-rendered (Unity 6000.3.26f1, llvmpipe). Device UNVERIFIED (KI-002).

**Save compatibility.** None.

## D-154 — Projects screen says what finishing each project opens (2026-10-17)
**Decision.** Region 1 choice framing: each open (active, queued or available) project row on Projects is followed by "Finishing it opens: A, B." naming the Locked projects for which it is a prerequisite and whose other prerequisites are already restored. `ShellController.OpensNext` mirrors `OfflineAdvancer.UpdateAvailability` (prerequisites only), so the line is exactly what completion would unlock. No line when nothing opens; never on restored or locked rows. Locked projects were already counted on screen; naming the immediate next ones is a choice aid, not a spoiler of story content. Display only, new `PlayerStrings` key `shell.projects.opens`.

**Tests.** EditMode `ProjectChoiceFramingTests` (3). EditMode 240/240, PlayMode 4/4 + 1 ignored capture (Unity-editor-tested, batch mode); .NET 1046/1046 unchanged (headless-tested). Capture `docs/evidence/d154-project-choices/` (editor-rendered). Device UNVERIFIED (KI-002).

**Save compatibility.** None. Content fingerprint unchanged.

## D-155 — Story and Discoveries say what open restorations lead to (2026-10-17)
**Decision.** Region 1 choice framing, same shape as D-154, display only.
- Story: under the "(next)" chapter, one "Getting there:" line per restoration the chapter still needs. An open project shows its status and progress (captured: "Getting there: Drain and replant the east wetland — being restored now (350 of 2200 vitality)."). A locked one names its open, unrestored prerequisites ("… opens once X is restored."), or says it "opens as the valley recovers" when a prerequisite is itself still locked, so a locked project is never named as the next step. Care-gated chapters (the epilogue) add nothing; the authored hint covers them.
- Discoveries: "Something new turns up when you finish: A, B." names the open (active, queued or available) projects whose completion turns up a still-unfound discovery. It names projects only, never the unfound find, so D-082's no-spoiler rule holds.
- Both lines come from the canonical rules. `StoryReadModel.ChapterRow` now carries `RequiredProjectIds` and `MinCareCycles` straight from `StoryChapterDefinition` (what `StoryProgress.Evaluate` reads), and `DiscoveryRow` carries `UnlockedByProjectId` (what `OfflineAdvancer.EvaluateDiscoveries` reads). These are ids, never copy, and a locked chapter still has no body. Helpers: `ShellController.ChapterPathLines` and `ShellController.FindsAhead`. New `PlayerStrings` keys are `shell.story.path.*` and `shell.discoveries.ahead`.
- `ValleyCaptureTests` gains `WG_CAPTURE_SCROLL_TO` (scrolls a line into view), and Discoveries with `WG_CAPTURE_STATE=mid|late` uses the seeded mid/late save. This is capture tooling only.

**Tests.** .NET `LeadsToReadModelTests` (3): read-model ids equal the catalog, and completing a project turns up exactly the discoveries that name it (headless-tested). EditMode `StoryDiscoveryLeadsToTests` (5). `StoryJournalTests` line-count assertion extended by the path lines; no test deleted. .NET 1049/1049, EditMode 245/245, PlayMode 4 passed + 1 opt-in capture ignored (Unity-editor-tested, batch mode). Captures in `docs/evidence/d155-leads-to/` (editor-rendered). Device UNVERIFIED (KI-002).

**Save compatibility.** None. Schema v3 unchanged; content fingerprint unchanged; pacing evidence 7/7 reproduced.

## D-156 — Home says what today's walks earned; derived, no save-schema bump (2026-10-17)
**Approval.** OpenSpec change `openspec/changes/add-today-vitality-earned/` was approved by the executing agent under the design authority Michael granted for this campaign. Region 1 scope.

**Decision.** Home shows "Today: N Vitality earned from your walks." directly under the Unspent line. The line appears only when N > 0; there is no zero-day line, streak or comparison.
- The value is derived. `ProcessedRecordLedgerState.VitalityOnLocalDay(day)` sums the net applied `VitalityCredited` of the retained processed-record rows whose `LocalDay` (D-103) is that day.
  - Undated rows (v1/v2 legacy, fixture or dev sources) never count.
  - A deleted walk (zero eligible steps) counts 0, even when its clawback was clamped.
  - A downward correction reduces its day by what was actually reversed.
  - A day at or before `RetiredThroughLocalDay` is unknown, and no line is shown.
- `GameSession.GetTodayEarnings(utcOffsetSeconds)` takes today as the trusted clock's instant under the device offset. `ShellController.LocalOffsetSeconds` defaults to the device's current offset and is injectable in tests.

**Schema bump: none, recorded explicitly.** The save schema is v3 before and v3 after. No migration was added, and `MigrationRunner`'s chain is unchanged.
- The brief proposed a persisted per-day ledger behind a v3 → v4 migration. The design review found the save already holds that ledger: each credited record's row carries its net applied Vitality and local day, and is committed atomically with the reward.
- A second copy would add a validator invariant and save bytes. Its migration could only seed it empty (wrong mid-day) or from the same rows.
- Deriving meets every requirement of the brief:
  - conservative on old saves (never invents credit);
  - bounded at 0 added bytes, so the D-098/D-139 save-growth gate is unchanged;
  - exactly-once, by the existing identity dedup;
  - corrections and reversals reduce the day;
  - the same local-day rule as days walked.
- If per-day history beyond the 45-day retention window is ever needed, a later change can add a pruned persisted ledger through `MigrationRunner`, seeded from this derivation. That would be its own schema bump and its own decision.

**Tests.**
- .NET, headless-tested:
  - Domain `DailyEarningsTests` (5).
  - Application `DailyEarningsSessionTests` (13): local-day boundary (UTC+8 vs UTC−5), replays, correction and deletion, clamped deletion, undated walks, offset out of range, round trip with no new field and reading never writing, v1 and v2 old saves load with today 0 and lifetime totals intact, a hostile v2 save with injected local days fails closed or shows 0, and a tampered negative row is rejected.
- EditMode `HomeTodayEarnedTests` (5).
- Totals: .NET 1067/1067; EditMode 250/250; PlayMode 4 passed + 1 opt-in capture ignored (Unity-editor-tested, batch mode).
- Gates: pacing evidence 7/7 reproduced, content compatibility passed, save growth verified, architecture 15/15.
- Capture: `docs/evidence/d156-today-earned/` (editor-rendered). Device UNVERIFIED (KI-002). On a device, the Health Connect `endZoneOffset` read is still unverified (KI-002), and without it walks are undated and the line does not appear.

**Save compatibility.** None: schema v3 is unchanged, nothing new is persisted, and the content fingerprint is unchanged.

**Owner acceptance (2026-10-17).** Michael, the owner, accepted the derived no-migration design: per-day earnings stay derived from the processed-record rows, and schema stays v3. The 'confirm D-156' item is removed from the KNOWN_ISSUES morning queue. A persisted pruned ledger stays a possible later change with its own OpenSpec, migration and decision.

**Addendum (D-157).** A walk whose source supplied no zone offset is now dated at ingestion with the device zone's offset at the record's end instant (adapter level, schema unchanged). The Today line therefore also shows for offsetless Health Connect records and, once composed, untagged HealthKit samples.

## D-157 — An offsetless walk gets a local day from the device zone at the record's time (2026-10-17)

**Context.** D-156's "Today: N Vitality earned" and D-103's days walked only count walks that carry a local day. A record gets one only from its source's zone offset. A walk without an offset was undated, so the Today line could stay hidden on real data. The question was what the adapters actually receive:
- **Health Connect.** The bridge reads `android.health.connect.datatypes.StepsRecord` through the framework API (API 34+). There `IntervalRecord.getEndZoneOffset()` and `getStartZoneOffset()` are documented as *never null*. A record written without an offset (Jetpack `startZoneOffset`/`endZoneOffset` are nullable) is given the writer's system default by the framework builder, so the platform still returns one. The bridge's null check and the adapter's empty sixth field are therefore defensive. They are reachable from a future or alternative bridge, never observed. The real-world risk is low but it was not handled.
- **HealthKit** (unwired, KI-022). A sample's zone comes only from optional `HKMetadataKeyTimeZone` metadata, which the Swift bridge reads when present. Apple sets it mainly for some sample kinds (e.g. sleep). iPhone step samples are commonly untagged, so on iOS an undated walk would be the *normal* case.

**Decision.** At ingestion, in the adapter (Infrastructure), a **credit** record with no source offset gets the **device zone's offset in force at the record's end instant** (`DeviceZoneOffsets.Local` = `TimeZoneInfo.Local.GetUtcOffset(endUtc)`).
- A source offset always wins, and the fallback is not consulted.
- Deletions never take it. A correction without an offset already keeps the row's previous day (D-103).
- The offset is the one at the record's time, not at sync time. A walk before a DST change gets the pre-change offset, and a `DateTimeOffset` instant is unambiguous in the repeated fall-back hour.
- It fails soft. A throwing zone lookup, a null result, a non-whole-second offset or one outside ±18 h leaves the walk undated, exactly as before. The fetch never fails because of it.
- It uses the same zone the shell reads "today" in (`ShellController.LocalOffsetSeconds` = `DateTimeOffset.Now.Offset`), so a walk and "today" agree for a player who did not travel.
- The Domain stays zone-free: `LocalDayCalendar` still takes an integer offset. The zone lookup is an injected `Func<DateTimeOffset, int?>` on the adapter constructor (optional; null keeps the pre-D-157 behaviour). `ShellBootstrap` passes `DeviceZoneOffsets.Local` for Health Connect. `HealthKitActivityAdapter` has the same seam, mirrored, and stays unwired (KI-022).

**What does not change.**
- Crediting. The offset is outside record identity (`ActivityIdentity`), and the Vitality credited is identical with and without the fallback (tested).
- Exactly-once. A replay is a dedup no-op. A replay under a different device zone (the player flew) re-credits nothing and does not re-label the stored day (tested).
- The save schema. It stays v3. `ProcessedRecordEntry.LocalDay` already exists, so no migration and no new field are needed.

**Rejected.**
- Inferring in the Domain or Application. It would put a zone database into deterministic code (the `openspec/config.yaml` invariant: all time from an injected clock, integer math).
- The device's *current* offset, as D-102 rejected for seeding. It would mis-date walks across a DST change.
- Keeping the walk undated. Then Today could never show on iOS.
- Flagging inferred days so days walked stays "at least N". A best-estimate day can be off only by a midnight shift, so "at least" would not be true either.

**Residual (logged in KI-025).** If the player changes zone between an offsetless walk and its first sync, the walk is dated in the new zone: its day may shift by one, and "Today" or days walked can be off by one. Crediting is never affected. Mono's `TimeZoneInfo.Local` may also be cached until restart after a zone change. Both "today" and records use it, so they stay consistent. Device behaviour for both platforms is UNVERIFIED (KI-002, KI-022).

**Tests.** `DeviceZoneOffsetFallbackTests` (Infrastructure, 10 cases):
- the offset at the instant across both DST transitions (custom zone rules, independent of the host tz database), non-hour (+5:45) and date-line (+14 / −11) offsets;
- a missing or empty sixth field takes the fallback, and a present offset wins without consulting it;
- the record's time, not the sync time, decides the day across a DST change;
- a throwing, null or out-of-range fallback leaves the walk undated and still fetches;
- deletions never take it; HealthKit untagged versus tagged samples;
- end to end through `GameSession`: Today shows 60 only with the fallback, credit is identical, a replay under another zone is a duplicate with the day unchanged, schema v3, and the day survives a reload.

A mutation check (removing the fallback call) fails 4 of them. All are headless-tested.
- Totals: .NET 1077/1077 (Domain 162, Infrastructure 208, Application 707; headless-tested); EditMode 250/250 and PlayMode 4 passed + 1 opt-in capture ignored (Unity-editor-tested, batch mode, so the `ShellBootstrap` wiring compiles in Unity). Gates: architecture 15/15, content compatibility passed, save growth verified, pacing evidence 7/7 reproduced.

**Save compatibility.** None: schema v3 is unchanged and the content fingerprint is unchanged.

## D-158 — Days walked and "Today" must agree: seeded consistency property (2026-10-17)

**Context.** One local day per record now feeds two separately computed views. Days walked (D-103) is a *maintained* sorted set, `WalkedLocalDays`, updated on credit, correction, deletion and retirement. Per-day earnings and "Today" (D-156) are *derived* from the retained rows at read time. D-157 makes more records dated. Nothing tested that the two views agree when a correction moves a walk to another local day, when walks are deleted, or when rows retire past the 45-day horizon.

**Decision.** Add `LocalDayViewsConsistencyPropertyTests` (Application). There are 10 seeds × 400 random steps. The steps are new walks with mixed source offsets (none, −5/−4 h, 0, +5:30, +5:45, +8, +14, −11 h; some sub-unit walks), corrections that change quantity *and* offset, deletions, replays, clock advances (about 100 days per run, so retirement happens) and reloads every 50 steps. After every step, on the decoded save:
- every retained credited row's day is a walked day (unless it is at or before the retired horizon);
- every walked day is backed by a retained credited row, or is at or before the horizon;
- a day with Vitality earned is a walked day, and per-day earnings are never negative;
- dated per-day earnings equal the dated retained credited row Vitality, and all retained row Vitality is at most lifetime credited;
- "Today" equals the derivation for the clock's local day under five device offsets (−11 h to +14 h), including unknown at or before the horizon;
- a full replay credits 0 and moves no view;
- a reload reproduces every view.

The run asserts it actually reached retirement and a correction that moved a walk's day. Otherwise the property would prove little.

**Result.** No finding; production code is unchanged. A mutation check passes it only with the fix in place: dropping `MaybeForgetDay` on a day-moving correction fails 4 of 10 seeds with "walked day … has no retained credited row". All of it is headless-tested. .NET 1087/1087 (Domain 162, Infrastructure 208, Application 717).

**Save compatibility.** None (test only).

## D-159 — Whole Region 1 playthrough simulation; the survey no longer closes the region with a chain unbuilt (2026-10-17)

**Context.** The pacing CLI (D-039/D-062) proves closure timing for one fixed queue policy: catalog order. Nothing played Region 1 as a player meets it: from the first onboarding screen, reading every return summary, through closure to the care chapter and the last epilogue note, under different project choices.

**Decision.** Add `Region1PlaythroughTests` (Application, headless). It covers low (3,000/day), irregular (the D-039 weekly pattern) and high (20,000/day) walkers, each under three choice policies: catalog order, cheapest open project first, and deepest (last-listed) first. Each run:
- drives onboarding through `AdvanceOnboarding` to Complete, making the first choice through `EnqueueProject`;
- loads a fresh session from disk every day, ingests through the real pipeline, reads and acknowledges the return summary, and chooses only when idle;
- keeps walking after closure until the care chapter unlocks and the last epilogue note (day +210) is revealed.

It asserts all of the following:
- closure lands within the `PacingContract` target (high ≤ 120, irregular ≤ 250). Low has no target (D-039). A 730-day reachability tripwire stands in for it; it is not a balance target.
- all 19 projects were offered, chosen and completed.
- all 10 chapters are unlocked, and each "New chapter" line was shown. The care chapter is shown only after closure.
- all 13 discoveries are recorded and announced.
- all 3 expeditions are announced as ready and as complete.
- all 6 letters are found, and the full epilogue (journal, 2 closing letters, 8 notes) is shown.
- both arcs reach 4/4, the state is validator-clean, and the three policies take distinct routes.

**Finding (fixed).** `MeetsClosureCriterion` returned true on the survey milestone alone. That assumed the survey's prerequisite closure restores the whole roster, and it does not: the canopy walkway is no prerequisite of the survey. Under cheapest-first, all three walkers funded the survey before the canopy. The region closed with 18/19 projects and 5/6 chains. The player got the celebration, the epilogue and Evergreen Tending, which absorbs all later Vitality, while chapters 7–9 and the canopy letter stayed locked behind a project the queue never prompted for again.

The fix: the chain minimum is enforced on every route, so Region 1 closes on the survey plus all six chains. `GameStateValidator` now uses `ExplainsRecordedCompletion` (the current rule OR the pre-D-159 milestone-only rule). A save that already closed the old way therefore still loads and stays complete, and it can still fund the canopy and unlock every chapter (`PreD159ClosedSave_…`). OpenSpec `enforce-closure-chain-minimum`.

**Measured** (seed 42; closure day / care chapter day):

| Walker | Catalog | Cheapest | Deepest-first |
|---|---|---|---|
| high | 77 / 82 | 77 / 82 | 77 / 82 |
| irregular | 109 / 117 | 110 / 117 | 109 / 117 |
| low | 510 / 544 | 512 / 544 | 510 / 544 |

**Observation (accepted, KI-026).** On a day with more than 5 Transformation facts, the D-055 per-kind rotation can retire the project's own "X is complete." line in favour of the same day's later chapter, arc and landmark lines. This happened for the canopy walkway on the closure day (cheapest-first) and for settlement power (deepest-first). The test allows it only on a saturated day, and for at most 2 projects per run.

**Mutation check.** Reverting the `RegionModel` change fails the three cheapest-first playthroughs, the distinct-routes check and the survey-before-canopy regression.

**Verification.** Headless-tested: .NET 1099/1099 (Domain 162, Infrastructure 208, Application 729). Unity-editor-tested: EditMode 250/250. Pacing evidence: 7 reproduced, 0 drifted; content compatibility and architecture gates green.

**Save and content compatibility.** No save-schema change (v3), no content change (fingerprint and content version unchanged). Saves completed under the old rule stay valid by design.

## D-160 — The celebration recaps what the player restored (2026-10-17)

**Context.** The Region 1 celebration (D-080/D-081) shows a headline, a reassurance line and that day's summary items. It never says what the whole restoration added up to. The day's items are also bounded (D-055), and D-159 found the final project's own line can rotate out on the closure day (KI-026).

**Decision.** When the celebration applies, the shell inserts a "What you restored:" recap right after the two celebration lines, before the day's items. It is built by `ShellController.RestoredRecap` from read models at render time:
- the number of restoration projects finished (`RegionReadModel.CompletedProjects`);
- "Places brought back:", listing every landmark at Restored or above (localized titles);
- discoveries recorded, expeditions walked and Nell's letters found (`GetDiscoveries`, `GetExpeditions`, `GetLetters`);
- the existing D-103 days-walked line, when days are known.

Counts of zero are omitted. Each count has a one/many key in `PlayerStrings` (`shell.recap.*`). The copy states facts only. It never says "N of M", and it uses no remaining/collect/streak wording. Continue stays the primary action. The recap is shown once, because it lives on the celebration summary, which is acknowledged durably.

**Scope.** Display only. No stored summary text, save, content or schema change. It is not shown on any other screen; Progress already carries running totals.

**Evidence.** EditMode `RegionCelebrationTests`: 2 new cases. One completes Region 1 through the real pipeline and checks every recap line against the read models, the calm-copy sweep, that Continue stays primary and that the recap is shown once. The other checks the omissions. EditMode 252/252 (Unity-editor-tested). Captures are editor-rendered (llvmpipe/Xvfb), NOT device: `docs/evidence/d160-restored-recap/celebration-top-editor-rendered.png`, `celebration-recap-editor-rendered.png` and `celebration-recap-2x-editor-rendered.png`. Synthetic walks carry no local day, so the fixture's recap has no days-walked line; with dated walks it shows "You have walked on N days." Device UNVERIFIED (KI-002).

## D-161 — Release-candidate check on f046934 (2026-10-17)

**Context.** D-159 changed a gameplay rule (when Region 1 closes), and D-160 changed the celebration screen. The last RC evidence (`521920e`) predates both, and dozens of slices since.

**Decision.** Re-run the RC procedure (D-075) on main `f046934`. First, `scripts/validate-clean-clone.sh --with-unity`: a fresh clone, every CI `run:` step, .NET, EditMode, PlayMode and an arm64 debug APK from a cold Library. Then `scripts/assemble-release-evidence.sh --clean-clone`. Evidence: `docs/evidence/rc-f046934/`.

**Result.** All 11 mechanical claims PASS. CI steps: 19 ok, 2 hosted-only skips. .NET: 1099/1099. EditMode: 252/252. PlayMode: 4 passed + 1 capture-only ignored. The APK built. Hosted CI is green. Labels: automated verified / Unity-editor-tested / build-verified only. Device is UNVERIFIED (KI-002). Not a release.

## D-162 — Copy-quality sweep: generated lines read well with every title (2026-10-17)

**Context.** Every project title is imperative ("Complete the valley survey"), so composer and shell templates that put a title in subject position read badly. The D-160 capture showed "Complete the valley survey is complete." Others: "Source-to-Sound Route route is ready.", "Clear the old trailhead is ready to queue.", glance "Rebuild the trail bridges is underway (…)" and "Next up is Clear the old trailhead.", Story "Getting there: Wire settlement power opens once Restore the market hall is restored.", the button "Move Clear the old trailhead up", and D-100's "A is now x, and B is now y, and C is now z.".

**Decision.** Fix it at display time; stored lines are unchanged.
- `SummaryLineText.Display(stored)` is the player-facing form of a stored summary line in every locale, English included. `GameSession` now always fills `ItemRow.DisplayText` with it. `ItemRow.Text` stays the stored line, and shells keep parsing that.
  - Project complete becomes a per-project completion sentence (`Region1SummaryCopy`, keyed by stable id, e.g. "The valley survey is complete."). A locale can override it with `<project id>#completed` (`ContentText.Fields.Completed`). A locale's existing `summary.project-complete` pattern still wins over the English sentence, so stored lines keep translating.
  - Project ready: "Ready to queue: {0}." Expedition ready: "A new expedition is open: {0}." Expedition complete: "Expedition walked: {0}." / "Expedition walked: {0} (+N Resource)." A locale's own `summary.*` pattern for these forms still wins.
  - A line is only re-rendered when its title maps to a known project or expedition. Unknown titles and notices stay as stored.
- The glance lead uses the item's display form when it would otherwise echo the stored line. Glance English: "Now restoring: {0} ({1} of {2})." and "Next up: {0}.".
- Shell: "Move up: {0}", "Getting there: {0} (opens after: {1}).", "Getting there: {0} (opens as the valley recovers).". D-100 landmark lists join as "A, B, and C".
- `FormatSummaryLines` (`SummaryLines` on results) and `SummaryLineText.Localize` are unchanged: they still give the stored English line in English.

**Scope.** Display only. No save, schema, content fingerprint or stored-text change. Older saves render with the new forms automatically.

**Evidence.** `SummaryCopyQualityTests` (11 headless tests) render every composer template for every title it can carry through the real composer and `Display`: 19 projects ×2, 3 expeditions ×3, 13 discoveries, every chapter, landmark × stage, producer × 3 and the region. They reject doubled words, an imperative title as a sentence subject (`{title} is/are/opens/continues/was`), doubled punctuation or spacing, raw ids and unfilled placeholders. They also cover the glance phrases and D-100 lines for every project. `Region1PlaythroughTests` now runs the same checker over every summary line the player sees in all 9 playthroughs. EditMode `CopyQualitySweepTests` (3) render the shell templates for every project title (and every pair), and every line and button of Home, Projects, Story, Region, Discoveries, Progress and the summary, mid-game and on the celebration. .NET 1110/1110, EditMode 255/255. Captures (editor-rendered): `docs/evidence/d162-copy-sweep/`. Labels: headless-tested, Unity-editor-tested, editor-rendered; device UNVERIFIED (KI-002).

## D-163 — A crowded summary keeps every project's completion line (KI-026) (2026-10-17)

**Context.** A return summary keeps at most 5 Transformation lines (D-055) and retired the oldest first. On a crowded day (closure day: two completions, two chapters, landmark stages, arcs, expeditions, the region), the project's own "is complete." line was the oldest and was dropped (D-159, KI-026).

**Decision.**
- **Composer retention (`ReturnSummaryComposer.PickRetired` / `RetentionRank`).** When a kind's bucket is full, the item retired is the one with the oldest first-commit time, as before, so older days still rotate out first (D-055). Within that oldest batch, the lowest-value line goes first. The incoming line competes too. Transformation ranks, kept longest first: region complete, a project's completion line, new chapter (it carries the "read the new chapter" action), expedition (also counted on Discoveries and in the D-160 recap), the "N restoration projects are complete" count, ecology/settlement arc lines, and last the landmark stage line. Other kinds share one rank, so their rotation is unchanged.
- **Display (`SummaryDisplayFilter`, shell only).** The summary screen does not render (1) a landmark stage line when a CURRENT completion line for the project that brought it is shown, because the D-100 detail under it already says "X is now restored.", or (2) a "… is ready to queue." line for a project already restored. Stored items are unchanged.

**Why not display-only for everything.** A line the composer retired is not stored, so no display rule can bring it back. The retention order is the smallest change that keeps it. It is deterministic, adds no save field and no schema change, and existing saves load unchanged. Only newly composed summaries differ.

**Evidence.** `SummaryPriorityTests` (16 headless tests): the crowded D-159 closure day keeps both completions, both chapters and the region; older batches still rotate first; other kinds rotate as before; rank table; display pruning. `Region1PlaythroughTests` now requires 0 rotated-out completion lines in all 9 playthroughs (was ≤2). An expedition line may yield to chapter and project lines only on a saturated day; Discoveries and the recap still count it. EditMode `RegionCelebrationTests.Celebration_keeps_the_final_completion_line_and_skips_repeats`. Pacing evidence 7/7 reproduced, content compatibility and save growth pass. .NET 1126/1126, EditMode 256/256. Labels: headless-tested, Unity-editor-tested; device UNVERIFIED (KI-002).
