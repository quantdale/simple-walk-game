# 00 — Product Thesis

## 1. Problem

Most fitness games assume the player is willing to perform two separate activities:

1. exercise in the real world; and
2. spend additional time actively playing the game.

That assumption excludes an important audience: people who already walk, run, train, hike, commute, or exercise but are not active gamers and do not want another daily screen-time obligation.

The product must therefore treat **physical activity itself as the core gameplay action**.

## 2. North-star statement

> **A persistent world that grows because you move in the real world.** Walk, run, hike and train normally. Your activity restores ecosystems, rebuilds civilization, sends expeditions into unknown territory, and permanently transforms a dying world. Play for seconds or explore for hours—the world progresses because you lived your life, not because you stared at your phone.

## 3. User-time contract

The design must support four valid engagement modes:

| Mode | Screen time | Intended outcome |
| --- | ---: | --- |
| Passive | 0 min/day | World still progresses from activity ingestion. |
| Check-in | 30–90 sec | Understand what happened, claim no mandatory reward, make at most 1–2 choices. |
| Management | 3–5 min | Adjust priorities, queue projects, review expeditions and regions. |
| Visit World | 10–30+ min optional | Explore, decorate, observe, take screenshots, discover lore. |

No mode below another in this table may be required to maintain progress.

## 4. Core loop

### Passive loop

1. User walks/runs/trains.
2. Health store records activity.
3. App reconciles available data when possible.
4. Activity is normalized into an append-only ledger.
5. Eligible activity creates `VitalityGrant` records exactly once.
6. Vitality flows through the user's allocation policy.
7. Projects advance.
8. Completed projects emit immutable world events.
9. Regions, ecosystems, settlements, expeditions, and narrative state update.
10. The next foreground session presents an away report.

### Check-in loop

1. Open app.
2. See a concise “Since you were away” summary.
3. See today/weekly activity and the world effect, not merely fitness statistics.
4. Optionally answer one meaningful decision.
5. Optionally reprioritize projects.
6. Leave.

### Optional deep loop

The user may inspect the map, visit the world, decorate restored spaces, read lore, compare historical world states, tune automation, or plan expeditions. None of these is necessary to convert earned activity into progress.

## 5. Primary fantasy

The initial setting should preserve the **dead-world -> living-world restoration** fantasy because it gives physical movement a visible emotional consequence.

Recommended initial region: **Ashfall Basin** or an equivalent devastated valley with clearly staged restoration:

- Stage 0: ash, ruins, polluted waterways, dead vegetation.
- Stage 1: paths reopen, water begins flowing, pioneer plants return.
- Stage 2: wetlands/forest/farms recover, wildlife returns.
- Stage 3: settlements, workshops, research, transit, public spaces reactivate.
- Stage 4: regional specialization and player choices visibly alter the rebuilt ecosystem/civilization.

The game must surface transformations as before/after states, map changes, animated summaries, milestones, and optional world visits.

## 6. Activity is not just currency

Do not reduce every physical action to “steps = coins.” Use a normalized activity model while preserving interpretability.

Recommended reward dimensions:

- **General movement** — broad restoration energy.
- **Distance** — exploration and logistics affinity.
- **Workout duration** — effort credit for gym/cycling/activities with few steps.
- **Elevation** — mountain/engineering/exploration affinity.
- **Consistency** — rolling Momentum, not brittle app-open streaks.
- **Long sessions** — expedition endurance bonuses.

Avoid making heart rate, body weight, calories, or other sensitive/noisy metrics required for the core economy.

## 7. Vitality economy

`Vitality` is the common, understandable progression resource. It is generated from validated activity data and automatically consumed according to project priorities.

Properties:

- no manual “claim” requirement;
- no unbounded wall-clock idle generation;
- no loss because the user failed to open the app;
- grants are immutable/idempotent;
- balancing formulas are versioned;
- historical grants retain the formula version used;
- conversion caps/soft caps may exist to prevent pathological data imports, but ordinary high activity must not be punished.

## 8. Automation as a first-class feature

The player can set allocation policy, for example:

- 50% Restoration
- 25% Exploration
- 15% Settlement
- 10% Research

Policy may be represented as categories or explicit queued projects. The system should always have a sensible default, including a `Balanced` preset.

Automation must be transparent: the away report should explain where Vitality went and why.

## 9. Projects

A project is the main unit of world progress. Examples:

- Clean the North River.
- Rebuild the old footbridge.
- Restore wetland habitat.
- Reopen the greenhouse.
- Survey the ridge.
- Reconnect the transit gate.
- Establish a wildlife corridor.

Projects have:

- stable ID and version;
- region;
- category;
- prerequisite expression;
- Vitality cost and optional affinity requirements;
- progress;
- completion effects;
- world event payload;
- summary copy;
- visual stage effects;
- optional decision hooks.

## 10. Decisions

Decisions are sparse and meaningful. They should not become taps-for-the-sake-of-taps.

Good example:

> The restored river has reached a fork. Restore the wetlands or redirect water toward abandoned farms.

Rules:

- most decisions wait until the user returns;
- missing a decision should not destroy earned activity;
- auto-resolve only when the user explicitly enables a policy;
- show consequences clearly enough to feel intentional;
- choices should alter future project graph, visuals, bonuses, narrative, or specialization.

## 11. Expeditions

Expeditions turn cumulative activity into discovery.

Recommended model:

- player assigns an expedition target;
- expedition has an effort requirement expressed in activity-derived units, optionally combined with minimum real time;
- progress accrues without app opens;
- completion creates discoveries, region unlocks, lore, cosmetic artifacts, project blueprints, or world events;
- no “come back in 4 hours to collect” requirement.

## 12. Consistency without anxiety

Replace conventional daily app streaks with **Momentum**:

- rolling 7- or 14-day activity consistency;
- based on personalized activity bands rather than absolute elite thresholds;
- no requirement to open app;
- no catastrophic reset from one missed day;
- optional recovery/rest-day recognition;
- display as a trend or stability bonus, not a threat.

## 13. Notifications

Notifications exist to celebrate meaningful changes, not manufacture engagement.

Examples:

- “The North River is clear again.”
- “Your expedition reached the ridge.”
- “Three projects advanced while you were away.”

Default policy should be conservative, with digesting, quiet hours, user-selectable categories, and no guilt language.

## 14. Monetization constraints

Do not optimize monetization before product-market fit. If monetization is later added:

- never sell health-derived progression advantage;
- never sell a way to bypass real activity in competitive contexts;
- prefer cosmetics, optional world themes, additional narrative regions, or one-time premium unlocks;
- never use HealthKit/Health Connect information for ad targeting.

## 15. Product metrics

Do not optimize for raw session duration. Long screen time can mean the design failed.

Primary metrics:

- percentage of ingested activity correctly converted to progression;
- weekly active users who receive at least one meaningful world change;
- return rate after 3/7/14 days without requiring daily opens;
- median check-in duration target: 30–90 seconds;
- project completion and choice engagement;
- percentage of users using automation successfully;
- permission success and health-data reconciliation success;
- world-state catch-up latency on foreground;
- retention correlated with activity, not notification spam.

## 16. Explicit anti-goals

Do not build:

- mandatory tap-to-collect income;
- energy bars that refill by waiting in-app;
- daily-open streaks;
- repetitive manual placement required for progress;
- constant red-dot notification debt;
- mandatory combat grinding;
- a generic idle game reskinned with step coins;
- precise background-timer dependencies;
- invasive health-data collection;
- a 3D engine before the passive loop is proven.
