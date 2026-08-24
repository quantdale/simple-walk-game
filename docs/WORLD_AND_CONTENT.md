# World and Content Design

## 1. World-design objective

The world is the long-term visual memory of the player's real-world activity.

It must visibly communicate that sustained movement repaired something meaningful. Restoration should be readable even if the player ignores most numerical UI.

The first region must be deep enough to prove the product thesis before additional regions are built.

---

## 2. Region structure

A region should contain several interconnected restoration domains rather than disconnected levels.

Recommended first-region composition:

- central settlement/hub;
- damaged water system;
- ecological zone such as wetland/grove;
- infrastructure/industrial ruin;
- agricultural or greenhouse zone;
- research/observation site;
- transit route/gate;
- residential/community area;
- one visually dominant end-state landmark.

The exact fiction may change, but the content architecture should support this scale.

---

## 3. Environmental storytelling

The world should communicate degradation and recovery through:

- structural condition;
- debris;
- water quality/flow;
- vegetation density and diversity;
- wildlife presence;
- inhabitants and activity;
- lighting;
- atmospheric particles;
- soundscape;
- route accessibility;
- signage, artifacts, and records.

Avoid relying on exposition text to explain every transformation.

---

## 4. Restoration language

Every major landmark needs a clear before/after contrast.

A useful restoration sequence may be:

1. **Ruined** — obviously non-functional.
2. **Stabilized** — hazards/debris reduced; structure readable.
3. **Functional** — primary function returns.
4. **Restored** — visually healthy and integrated.
5. **Flourishing** — optional late stage showing abundance, community, or ecological maturity.

Content authors may omit stages, but should not create cosmetic-only stages that do not communicate real change.

---

## 5. First-region content minimum

Before the first region is called content-complete, it should include at least:

- 5–7 major project chains;
- 12–20 meaningful project nodes total;
- 6+ major landmarks;
- 3+ visually distinct environment transformations;
- 2+ producer/infrastructure systems;
- 10+ discoveries with provenance;
- 3+ expedition objectives/routes;
- one region-level ecological progression arc;
- one narrative closure milestone;
- a post-completion evergreen state.

These are minimum planning targets, not reasons to pad weak content.

---

## 6. Project-chain design

A project chain should tell a transformation story.

Example pattern:

`clear access → stabilize structure → restore core function → connect dependent system → ecological/community payoff`

Each step should unlock something visible or systemic.

Avoid chains where five steps are numerically different but visually identical.

---

## 7. Interdependency rules

Interdependence creates strategic choice but must not create opaque deadlocks.

Good dependency:

- repairing water intake enables wetland recovery and greenhouse efficiency.

Bad dependency:

- a hidden research flag blocks a project with no player-visible explanation.

Requirements:

- prerequisites are inspectable;
- circular dependencies are validation errors;
- at least one viable progression path exists at all times after onboarding;
- optional branches cannot accidentally block the critical path.

---

## 8. Ecological progression

Ecology should be represented at an understandable abstraction level.

Possible canonical axes:

- water health;
- vegetation recovery;
- habitat quality;
- wildlife presence.

These may map to discrete stages rather than continuous simulation.

The world should reflect ecological state through environment sets and ambient behavior.

---

## 9. Settlement progression

The settlement/hub provides human-scale emotional payoff.

Potential changes:

- more inhabitants;
- repaired structures;
- workshop activity;
- lights and power;
- market/community spaces;
- transport access;
- new dialogue/lore;
- visible use of restored infrastructure.

The settlement must not become a foreground city-builder requiring constant micromanagement.

---

## 10. Discoveries

Every discovery should have:

- stable ID;
- category;
- title/body content keys;
- unlock trigger;
- provenance text/data;
- optional world location;
- optional associated media/model/icon;
- rarity only if it has meaningful design purpose;
- reviewed state separate from unlocked state.

Discoveries should reinforce the relationship between movement and world recovery.

---

## 11. Narrative model

The MVP narrative should be lightweight and asynchronous.

Recommended structure:

- premise at onboarding;
- short story fragments tied to major restorations;
- discoveries that reveal history and context;
- occasional settlement/world reactions;
- strong region completion beat.

Avoid long unskippable dialogue that violates the attention budget.

---

## 12. Visual state binding

Every significant scene element should have a documented mapping from canonical state.

Example:

```text
landmark.waterworks.stage = RUINED
  → broken intake mesh
  → dry channel
  → debris decals
  → no workers
  → low ambient water audio

landmark.waterworks.stage = RESTORED
  → repaired mesh
  → flowing channel
  → clean bank material
  → workers/ambient activity
  → water ambience
```

Bindings should be data-driven where practical.

---

## 13. Content authoring schema

Every project/content definition should be serializable and validateable outside runtime presentation.

A project definition should include:

- ID;
- region;
- prerequisite IDs;
- resource/progress requirements;
- stage effects;
- unlock effects;
- discovery effects;
- producer effects;
- presentation keys;
- localization keys;
- content version.

The content pipeline must validate references before shipping.

---

## 14. Localization readiness

Even if the MVP ships in one language initially:

- player-visible text should use localization keys or a structure that can migrate cleanly;
- UI should tolerate longer strings;
- world signage containing text should be minimized or localized intentionally;
- numerical formatting should use locale-aware presentation where appropriate.

Do not bake critical copy permanently into textures.

---

## 15. Art direction principles

The art direction should emphasize restoration contrast.

Useful contrasts:

- dry → flowing;
- gray/barren → green/living;
- silent → populated;
- broken → functional;
- hazy/polluted → clear;
- isolated → connected;
- dark → warmly lit.

The game does not need photorealism. It needs a strong readable transformation language that scales to mobile hardware.

---

## 16. 3D world scope discipline

The Visit World experience should prioritize:

- recognizable landmarks;
- visible state transitions;
- efficient traversal;
- readable composition;
- performance.

Avoid spending the MVP budget on:

- enormous empty terrain;
- complex combat arenas;
- dozens of enterable interiors;
- high-cost simulation invisible to ordinary players;
- bespoke interaction mechanics for every landmark.

One polished, stateful region is enough.

---

## 17. World traversal

Optional traversal should be simple and forgiving.

Possible features:

- walking/running avatar;
- fast travel between restored landmarks;
- simple camera controls;
- inspect points;
- route gating based on canonical restoration.

Fast travel is encouraged because the product is not trying to maximize time spent crossing a map.

---

## 18. Content pacing

Each project chain should create a cadence of:

`visible early improvement → functional midpoint → meaningful completion → dependency payoff`

Balance against several activity profiles rather than only an idealized daily target.

Content should not require the player to maintain exact day-by-day activity.

---

## 19. Post-completion region state

After the region reaches its major completion milestone:

- the world remains explorable;
- producers continue within bounded rules;
- collections/discoveries may remain;
- optional flourishing upgrades may exist;
- no artificial reset occurs;
- future Region 2 can be introduced as expansion, not as the only remaining reason to open the game.

---

## 20. Content validation gates

Before a content build is accepted:

- all stable IDs unique;
- no missing prerequisite references;
- no dependency cycles;
- all critical paths reachable;
- all project states have presentation bindings;
- all major visual states load without missing assets;
- no impossible resource requirement;
- localization keys resolve;
- discoveries have valid triggers;
- expedition reward references resolve;
- Region 1 can be completed in deterministic simulation.

---

## 21. Content simulation

Create automated progression simulations using representative behavior profiles.

Expected reports:

- time/activity required per project;
- bottleneck resources;
- idle/capped time;
- queue-empty frequency;
- region completion range;
- discovery pacing;
- number of required foreground decisions.

The number of required decisions is a product metric. If a simulated moderate player must make dozens of maintenance decisions each day, the content design violates the product thesis.

---

## 22. Definition of content-complete

Region 1 is content-complete when:

1. every major chain has final definitions;
2. every canonical stage has a presentation mapping;
3. progression simulation demonstrates healthy pacing across multiple activity profiles;
4. all discoveries/expeditions are integrated;
5. the region has a coherent narrative arc;
6. post-completion behavior exists;
7. no placeholder asset/text is required for the critical path;
8. the region can be completed from a clean state through deterministic simulation;
9. world-state presentation remains performant on target devices;
10. adding Region 2 would be genuine expansion rather than unfinished-work avoidance.
