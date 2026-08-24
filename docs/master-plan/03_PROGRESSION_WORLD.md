# 03 — Progression and World Simulation

## 1. Design objective

Every meaningful unit of activity should eventually produce a comprehensible world consequence without requiring the player to manually spend it.

## 2. Pipeline

```text
ActivityLedgerEntry
  -> EligibilityDecision
  -> VitalityGrant
  -> AllocationPolicy
  -> ProjectProgressEvent
  -> ProjectCompletionEvent
  -> WorldEvent
  -> RegionState / JourneyTimeline / AwayReport
```

Every stage is persisted or reconstructible. Never mutate a number in-place without retaining the event/basis that explains why it changed.

## 3. Vitality formulas

Start simple and interpretable.

Recommended v1 inputs:

- steps contribution;
- walking/running distance contribution;
- workout-duration contribution when activity would otherwise be underrepresented;
- optional elevation bonus.

Use overlap-aware formula logic so a run does not trivially earn full independent rewards from steps + distance + workout duration.

Formula should return:

```ts
interface VitalityComputation {
  total: number;
  baseMovement: number;
  durationBonus: number;
  elevationBonus: number;
  longSessionBonus: number;
  formulaVersion: number;
  explanation: string[];
}
```

Exact balance constants should be tuned through simulation and dogfooding, not hard-coded into the master plan.

## 4. Balance principles

- Ordinary active users should complete visible projects several times per week.
- Very active users should progress faster, but content should not be exhausted immediately.
- Low-activity users should still see incremental movement.
- One exceptional day should feel meaningful without making the next week irrelevant.
- No hard daily cap that makes legitimate activity feel wasted; prefer diminishing bonuses/soft caps for extreme outliers if needed.
- Returning after a long absence should produce a satisfying summary, not hundreds of modal dialogs.

## 5. Allocation policies

Built-in presets:

- Balanced
- Restore Nature
- Explore
- Rebuild
- Research

Advanced custom policy can allocate percentages among categories or prioritized project queues.

If the current project completes mid-allocation, remaining Vitality continues to the next eligible project in the same transaction.

If no eligible project exists, Vitality remains banked or routes to a safe default; it must never disappear.

## 6. Project engine

Project states:

`LOCKED -> AVAILABLE -> ACTIVE -> COMPLETED`

Optional states:

`PAUSED`, `BLOCKED_BY_DECISION`, `ARCHIVED`

Project completion is exactly-once. Completion effects are idempotent and keyed by stable event ID.

Declarative effects may include:

- set region restoration stage;
- unlock project(s);
- unlock expedition target;
- spawn world landmark;
- modify environment parameter;
- add wildlife/settlement population band;
- queue decision;
- add Journey entry;
- grant cosmetic/lore artifact.

## 7. World event log

Examples:

```ts
type WorldEventType =
  | 'project_completed'
  | 'region_stage_changed'
  | 'waterway_restored'
  | 'species_returned'
  | 'structure_rebuilt'
  | 'route_reopened'
  | 'expedition_discovery'
  | 'decision_resolved';
```

World events are append-only, sequenced, timestamped, and content-versioned.

Materialized region state can be rebuilt from snapshots + events.

## 8. Region model

A region should expose dimensions that can change independently:

- ecology;
- water;
- infrastructure;
- settlement;
- knowledge/research;
- exploration coverage;
- narrative flags;
- visual stage parameters.

Do not encode every change as a single 0–100 restoration percentage. The user should see a world with tradeoffs and specialization.

## 9. Decisions and branching

A decision definition contains:

- trigger condition;
- prompt;
- choices;
- consequences;
- default/automation eligibility;
- visual preview metadata;
- downstream project graph changes.

Choice outcomes should be deterministic and recorded as events.

Avoid fake choices where both options differ only in copy.

## 10. Expeditions

Expeditions provide long-horizon goals beyond local projects.

State:

`PLANNED -> ACTIVE -> COMPLETE -> REVEALED`

The `REVEALED` distinction allows completion to occur without app open while preserving a satisfying reveal later; however, unrevealed completion must still unlock dependencies if needed. “Reveal” is presentation, not a claim gate.

Inputs may include:

- cumulative distance-derived effort;
- workout-duration-derived effort;
- elevation affinity;
- region prerequisites;
- minimum elapsed real time if fiction requires travel time.

## 11. Momentum

Momentum is a rolling consistency score, not a fragile streak.

Potential v1 algorithm:

- derive personalized daily movement points;
- aggregate 7-day rolling window;
- compare to a configurable baseline;
- smooth changes so one rest day does not collapse it;
- provide small non-essential bonuses or purely expressive world effects.

Never require app opens.

## 12. Narrative

Narrative should be asynchronous and digestible.

Use:

- project completion blurbs;
- Journey timeline;
- region milestones;
- expedition logs;
- occasional decisions;
- environmental storytelling in world view.

Do not interrupt every project with a dialogue. Away-report summarization must collapse multiple changes.

## 13. Away report

The away report is generated from the last acknowledged report checkpoint to current event sequence.

Sections:

1. **Your activity** — concise, source-correct totals.
2. **What it powered** — Vitality and allocation explanation.
3. **World changes** — top 1–3 changes.
4. **Discoveries** — expedition/lore if any.
5. **Needs your choice** — at most the most important pending decision.

If 50 events occurred, summarize and link to Journey rather than forcing 50 acknowledgements.

## 14. Content authoring

Create JSON/TS schema-validated content packs:

- regions;
- projects;
- decisions;
- expedition targets;
- world visual stage mappings;
- narrative copy.

Tests must validate:

- unique IDs;
- no missing references;
- no impossible prerequisite cycles unless explicitly modeled;
- every completion path has effects;
- localization-safe copy keys when localization begins.

## 15. Simulation tooling

Create a headless simulator CLI/script that can run personas:

- sedentary: 2k steps/day;
- light: 5k/day;
- active: 8–12k/day;
- runner: 30–60 km/week;
- gym-focused: several workouts/week but modest steps;
- hiker: high elevation on weekends;
- inconsistent/bursty.

Simulate 1, 4, 12 and 52 weeks and output:

- Vitality earned;
- projects completed;
- region stages;
- decision count;
- content exhaustion point;
- unused/banked Vitality;
- notification-worthy events.

Balance changes should run this simulation before merge.
