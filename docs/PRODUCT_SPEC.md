# Product Specification

## 1. Product statement

Simple Walk Game is a mobile ambient-progression game that converts ordinary real-world activity into the restoration of a persistent damaged world.

The product is explicitly designed around **low required attention**. The player should not need to become an active gamer to benefit from the system.

### Primary value proposition

> Your ordinary movement matters. Walk through real life, then return to a world that has changed because you moved.

---

## 2. Target users

### Primary user

A person who:

- wants additional motivation to move;
- may be busy, working, studying, or otherwise unable to dedicate long sessions to a game;
- enjoys progression, collection, world-building, or visible transformation;
- may enjoy games but does not want another daily obligation;
- values progress that survives inconsistent use.

### Secondary user

A more engaged player who wants to:

- optimize project priorities;
- inspect the world in detail;
- customize restored spaces;
- pursue discoveries and collections;
- spend longer optional sessions exploring.

The product must serve the primary user first. Secondary depth may never make the primary experience incomplete.

---

## 3. Core jobs to be done

The player hires the product to:

1. make ordinary movement feel meaningful;
2. provide a satisfying sense of long-term progress;
3. encourage return without guilt or coercion;
4. create visible evidence that activity accumulated over time;
5. give small moments of decision and anticipation;
6. provide optional escapism through a world that improves with the player’s activity.

---

## 4. Product principles

### P1 — Real life comes first
The game should fit around life, not demand that life fit around the game.

### P2 — Progress should be legible
Players should understand what their movement contributed to.

### P3 — The world is the progress visualization
Numbers matter, but restoration should be visible in terrain, water, structures, flora, wildlife, lighting, ambience, and inhabitants.

### P4 — Automation is earned convenience, not neglect
Automation should reduce busywork and preserve intentionality.

### P5 — Every interruption is optional unless state truly requires a decision
Do not manufacture alerts just to create engagement.

### P6 — Returning after absence should feel good
The return experience should summarize progress and suggest one next action.

### P7 — No hidden maintenance debt
The player should never discover that days of progress were wasted because they failed to tap a claim button, empty a chest, or refresh a queue.

---

## 5. Functional requirements

### 5.1 Activity

The system must:

- ingest supported activity data;
- normalize it into a platform-independent representation;
- reject obviously invalid or unsupported records;
- tolerate late and corrected records;
- deduplicate repeated records;
- credit activity exactly once;
- provide understandable diagnostics when activity is unavailable;
- support fixture/manual simulation in development without contaminating production progression logic.

### 5.2 Progression resource

The MVP should expose one clear primary activity-derived resource, tentatively **Vitality**.

Vitality must:

- derive from validated eligible activity;
- use documented conversion rules;
- be bounded against pathological source data;
- be auditable through an internal ledger;
- feed restoration/projects rather than exist as a meaningless score.

### 5.3 Projects

Projects represent substantial world changes.

The system must support:

- project prerequisites;
- costs/progress requirements;
- queueing;
- priorities;
- automatic application of available eligible progress;
- completion events;
- deterministic outcomes;
- visible world-state mapping.

### 5.4 Restoration

Restoration must affect canonical region state and presentation.

A major restoration should change multiple dimensions where appropriate:

- geometry or structures;
- materials/decals;
- vegetation;
- water;
- particles/atmosphere;
- population/ambient life;
- soundscape;
- available interactions;
- producer capacity;
- unlocked discoveries or routes.

### 5.5 Producers and passive systems

Producers may generate secondary resources or capabilities over time.

Requirements:

- deterministic rates;
- storage caps;
- offline advancement;
- no required frequent collection;
- clear cap behavior;
- no exponential runaway economy;
- automation configuration where useful.

### 5.6 Discoveries

Discoveries exist to make movement feel meaningful beyond raw resource accumulation.

Possible categories:

- wildlife;
- artifacts;
- old-world records;
- ecological observations;
- settlement stories;
- infrastructure remnants;
- rare environmental events.

A discovery should have provenance: the player should know roughly what activity/progression caused it to appear.

### 5.7 Expeditions

Expeditions are asynchronous objectives that can consume progress/time and return discoveries or region context.

They must:

- be optional;
- avoid requiring exact return times;
- continue while closed;
- resolve deterministically or from bounded random seeds stored in state;
- never block core progression if ignored.

### 5.8 Return summary

On meaningful return, the product should summarize:

- eligible activity processed;
- project progress/completions;
- new unlocks;
- producer outcomes if material;
- expedition/discovery outcomes;
- the next most useful action.

The summary should prioritize **change**, not dump raw logs.

---

## 6. Information architecture

Baseline top-level areas:

### Home
Fastest view. Shows what changed, current priority, progress, and one primary action.

### Region
A lightweight map/state view of restoration progress and major landmarks.

### Projects
Current queue, prerequisites, priorities, and future restoration choices.

### Discoveries
Journal/collection of discovered content and region story.

### Visit World
Optional real-time world exploration. May be represented as a prominent secondary action rather than permanent navigation depending on final UX testing.

### Settings
Activity permissions, data/privacy, accessibility, notification settings, diagnostics, export/reset where appropriate.

---

## 7. Onboarding requirements

The onboarding objective is comprehension, not feature exposure.

Within approximately two minutes, the player should understand:

1. movement powers restoration;
2. the app does not need to remain open;
3. activity access is requested for a clear reason;
4. the first restoration goal;
5. progress is not lost for taking a day off.

Avoid:

- long forced tutorials;
- teaching every system upfront;
- forced world-tour cinematics before the product is usable;
- asking for unrelated permissions.

---

## 8. Notification requirements

Notifications are opt-in, conservative, and event-based.

Potentially valid notifications:

- major project completed;
- expedition returned;
- meaningful new discovery;
- a player-configured reminder;
- action genuinely required because an explicit queue has no fallback.

Invalid default notifications:

- repeated “come back” nags;
- shame messaging about inactivity;
- artificial scarcity warnings;
- generic engagement prompts without player value.

---

## 9. Accessibility requirements

The MVP must include:

- scalable text where platform/engine constraints permit;
- screen-reader semantics for core UI;
- non-color-only state communication;
- reduced motion mode;
- haptics disable option;
- sound-independent feedback;
- adequate touch targets;
- readable contrast;
- ability to complete core progression without precise real-time motor input.

Optional 3D exploration should not be required for accessibility-critical progression.

---

## 10. Economy requirements

The economy exists to pace and communicate restoration, not to create pressure.

Rules:

- one clearly dominant activity-derived currency/resource in the MVP;
- secondary resources must have distinct purposes;
- hard caps require explicit rationale;
- no maintenance currency that destroys assets when depleted;
- no irreversible choice without warning;
- no premium-currency architecture in the MVP core;
- no reward design that incentivizes unsafe excessive exercise.

Activity conversion must use diminishing/bounded behavior when necessary rather than encourage unhealthy extremes.

---

## 11. Safety and responsible motivation

The product should encourage consistency, not extremity.

It must avoid:

- language implying medical outcomes;
- pressure to exercise through pain or illness;
- unlimited reward scaling that encourages unsafe overexertion;
- punitive streak loss;
- competitive systems that push excessive activity during MVP validation.

The game may celebrate movement without presenting itself as a clinical fitness application.

---

## 12. Offline requirements

While offline, the player must be able to:

- load the last known world state;
- inspect projects and region state;
- make local decisions;
- progress time-based local systems where valid;
- queue changes for persistence;
- safely reconcile platform activity when data becomes available.

No core state should become inaccessible because a backend is unavailable.

---

## 13. Error-state requirements

The product must explicitly handle:

- no activity permission;
- permission revoked;
- activity source unavailable;
- stale activity source;
- malformed/unsupported source records;
- duplicate activity;
- clock/time-zone change;
- corrupted or partially written save;
- migration failure;
- low storage;
- interrupted app lifecycle;
- unavailable optional 3D assets/resources.

Error handling should preserve player state first and explain recovery paths second.

---

## 14. Non-goals for MVP

The MVP is not trying to prove:

- social virality;
- multiplayer retention;
- competitive fitness;
- real-money economy;
- large-scale live operations;
- dozens of regions;
- combat depth;
- endless procedural content;
- precise route tracking;
- smartwatch-first interaction.

The MVP is trying to prove one thing extremely well: **ambient real-world movement can drive a satisfying persistent restoration game with minimal required screen time.**

---

## 15. Product acceptance criteria

The vertical slice should not be called successful until observed or tested behavior demonstrates:

- a player can make meaningful progress without keeping the app open;
- a normal return session is understandable in under one minute;
- sustained activity produces visible world transformation;
- skipping several days does not create catastrophic loss;
- duplicate/replayed activity does not duplicate rewards;
- a restart does not alter already committed progression;
- the player can understand permission/error states;
- the product remains coherent with Visit World mode never opened;
- optional deeper use feels additive rather than mandatory;
- Region 1 has a satisfying beginning, middle, and end-state transformation.
