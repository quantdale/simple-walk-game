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

**Status:** Proposed

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

**Status:** Proposed

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
