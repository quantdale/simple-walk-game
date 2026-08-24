# Agent execution contract

This repository is intended to be developed in long autonomous campaigns. Agents must treat `docs/master-plan/` as the product and architecture source of truth until superseded by an ADR or an explicitly approved plan update.

## Non-negotiable product rules

1. **Real-world activity is primary gameplay.** Never introduce a required screen-time grind.
2. **Zero-required-screen-time progression must remain valid.** The user may ignore the app for days and return to a correctly reconciled world.
3. **Background work is opportunistic.** Never rely on a task firing at an exact interval.
4. **Health-store reconciliation is idempotent.** Reprocessing the same input must not create duplicate rewards.
5. **The local database is authoritative for offline gameplay.** Cloud services may synchronize but must not be required for the core loop.
6. **Health data minimization is mandatory.** Do not collect sensitive metrics merely because APIs expose them.
7. **No brittle daily-open streak.** Progress and consistency derive from activity, not app launches.
8. **No punitive inactivity loss by default.** Projects can pause; they should not decay simply because the user was busy.
9. **Optional 3D/interactive world modes may not become a progression gate.**
10. **Accessibility is a product feature.** Support non-step movement sources where platform data permits it.

## Engineering rules

- TypeScript strict mode.
- Keep domain simulation pure and platform-independent.
- Native HealthKit/Health Connect code belongs behind a narrow provider interface.
- Every write path that awards progression must be transaction-safe and replay-safe.
- Persist schema migrations; never mutate production schema ad hoc.
- Use deterministic clocks and fixture providers in tests.
- Add semantic test IDs to user-critical flows.
- Prefer explicit state machines/enums over scattered booleans for long-lived lifecycle state.
- Instrument before optimizing.
- Do not add a backend dependency to solve a local-only problem.
- Do not request health permissions before the user sees a clear explanation of the benefit.

## Campaign protocol

Before coding:

1. Read the relevant master-plan documents.
2. Inspect current code, tests, migrations, and recent commits.
3. Record starting SHA and current branch.
4. Identify the milestone/campaign exit criteria being pursued.
5. Prefer one coherent vertical slice over many disconnected partial features.

During coding:

- Keep the app buildable.
- Add/extend tests with the implementation.
- Add migrations with schema changes.
- Keep fake activity providers first-class for automation.
- Do not silently change product semantics; write an ADR for deliberate changes.

Before finishing:

- Typecheck, lint, unit/integration tests, build, and relevant E2E.
- Report exact commands and results.
- Report files/migrations added.
- Report unresolved risks and deferred work.
- Update durable roadmap/waypoint docs if milestone state changed.
- Commit cohesive changes.
