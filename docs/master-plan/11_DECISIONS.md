# 11 — Initial Architecture and Product Decisions

These are master-plan decisions, not immutable law. Change them through an ADR when implementation evidence justifies it.

## D-001 — Real-world activity is the game

**Decision:** Required progression comes from real activity, not active in-app grinding.

**Consequence:** Optional deep gameplay cannot become a required economy gate.

## D-002 — Zero-required-screen-time progression

**Decision:** A user can progress without opening the app daily.

**Consequence:** No claim buttons, daily-open streaks, or exact-background-timer assumptions.

## D-003 — Local-first SQLite

**Decision:** Core state is persisted in SQLite and works offline.

**Consequence:** Cloud sync is additive and outbox-based.

## D-004 — Event-backed progression

**Decision:** Activity grants, allocations and world changes have durable event/basis records.

**Consequence:** State can be explained, replayed and deduplicated.

## D-005 — Native health stores are canonical

**Decision:** HealthKit on iOS and Health Connect on Android are canonical activity sources. Device pedometer may provide UI preview only.

**Consequence:** Native development builds/modules are required.

## D-006 — Minimal health permissions

**Decision:** Start with steps, walk/run distance and workout/exercise duration; add optional movement metrics only for implemented features.

**Consequence:** No default heart-rate/sleep/weight collection.

## D-007 — Background work is opportunistic

**Decision:** Background callbacks trigger reconciliation but never constitute the authoritative progression clock.

**Consequence:** Foreground catch-up must always restore correctness.

## D-008 — Vitality is automatic

**Decision:** Activity creates Vitality grants that auto-route through allocation policy.

**Consequence:** No manual collection loop.

## D-009 — Consistency uses Momentum, not brittle streaks

**Decision:** Use rolling activity consistency.

**Consequence:** A rest day or missed app open does not destroy a streak.

## D-010 — 2.5D world before 3D

**Decision:** Deliver a polished regional map/diorama before optional 3D Visit World.

**Consequence:** Product validation is not blocked by game-engine complexity.

## D-011 — Expo SDK 57 baseline

**Decision:** Start implementation on current stable Expo SDK 57 / RN 0.86, unless bootstrap-day compatibility evidence requires a newer stable version.

**Consequence:** Node/toolchain and native modules follow that SDK's supported matrix.

## D-012 — Supabase is deferred

**Decision:** No backend is required for first core loop.

**Consequence:** Account/cloud work cannot block M0–M7.

## D-013 — Single-player trust is lightweight

**Decision:** Avoid invasive anti-cheat until social/competitive/external-value systems exist.

**Consequence:** Provenance/dedupe exist now; stronger server validation is deferred.

## D-014 — No monetization optimization in MVP

**Decision:** Validate behavior and retention first.

**Consequence:** Economy is not warped around purchases.

## Deferred decisions requiring spikes/ADRs

- DB query layer: raw Expo SQLite vs Drizzle or another typed layer.
- Exact HealthKit incremental/aggregate hybrid strategy.
- Exact Health Connect change-token vs aggregate-window strategy by metric.
- Historical data import window on first authorization.
- Detailed correction/reversal semantics for edited health data.
- Final Vitality formula constants.
- Skia vs layered RN implementation for world map.
- Cloud-sync scope for activity data.
- 3D runtime/library.
- Optional social/shared-world features.
