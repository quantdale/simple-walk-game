# Active Execution Campaign — M0R Product Intent & Repository Identity Repair

**Status:** ACTIVE  
**Supersedes:** `M1 Deterministic Trust Kernel + Minimum M2 Ingestion Slice` planned at `7c7687e8e9d908db623935fd2d9f3c46f675ff4d`  
**Target branch:** `repair/product-intent-and-repo-identity`  
**Campaign class:** DOCUMENTATION / PRODUCT-CONTRACT REPAIR  
**Priority:** Critical — prevent implementation on a contaminated/inverted product contract  
**Primary repair spec:** `docs/REPOSITORY_IDENTITY_AND_PRODUCT_INTENT_REPAIR.md`

## Mission

Repair `quantdale/simple-walk-game` before any production implementation begins.

The repository currently contains a comprehensive documentation foundation and an M1 implementation campaign, but a cross-repository audit found two material problems:

1. **Product-intent inversion:** the current docs make minimal foreground attention and very short sessions a hard product objective even though the overhaul that produced these docs was intended to address insufficient meaningful player time / engagement.
2. **Repository-identity bleed:** the Simple Walk Game architecture and active campaign use `WalkGame.*` project/namespace names associated with the distinct `quantdale/walk-game` repository.

Do not execute the superseded M1 campaign. First repair the Simple Walk Game product contract, naming, decisions, roadmap, and execution plan from actual repository truth.

## Required first step

Read `docs/REPOSITORY_IDENTITY_AND_PRODUCT_INTENT_REPAIR.md` in full. Treat it as the detailed acceptance contract for this campaign.

Then read:

- `README.md`;
- `.agent/PLANNER_HANDOFF.md`;
- all files under `docs/`;
- recent Simple Walk Game commit history from the first commit through current branch HEAD;
- the previous M1 planner commit and prompt history;
- `quantdale/walk-game` only as a comparison/control for contamination detection, never as the source of truth for this repository.

## Repository distinction — non-negotiable

Keep these projects separate:

- `quantdale/walk-game` = mature Unity restoration-builder, Ashfall Basin, Builder/Explore implementation, current save-integrity hardening.
- `quantdale/simple-walk-game` = separate product whose independent product identity is being repaired here before implementation.

Generic goal/planner adapters may remain shared. Product docs, milestones, namespaces, code structure, and campaigns must be independently grounded.

## Workstream A — full documentation audit

Audit every repository-specific document for contradictions, copied assumptions, and product-goal inversion. At minimum cover:

- `README.md`;
- `docs/MASTER_PLAN.md`;
- `docs/PRODUCT_SPEC.md`;
- `docs/GAME_SYSTEMS.md`;
- `docs/UX_DESIGN.md`;
- `docs/WORLD_AND_CONTENT.md`;
- `docs/ROADMAP.md`;
- `docs/TECHNICAL_ARCHITECTURE.md`;
- `docs/ACTIVITY_PIPELINE.md`;
- `docs/PERFORMANCE_BUDGETS.md`;
- `docs/TESTING_AND_RELEASE.md`;
- `docs/RISK_REGISTER.md`;
- `docs/DECISIONS.md`;
- `docs/AGENT_EXECUTION_GUIDE.md`.

Do not patch only obvious phrases. Reconcile the complete product story end to end.

## Workstream B — repair the engagement objective

The repaired product must distinguish between:

- low **required maintenance burden**;
- meaningful **voluntary active play**;
- foreground engagement quality;
- real-world movement/progression integration where independently justified;
- healthy retention/engagement goals.

Do not preserve “shorter session length is always better” as a universal product law.

At the same time, do not solve the problem with dark patterns, grind, forced check-ins, punishment, nagging notifications, artificial waiting, or unsafe exercise incentives.

The goal is a game players can use quickly when busy **and** genuinely want to keep playing when they have time.

If longer active play is part of the repaired thesis, specify what the player actually does during those sessions and why it remains meaningful after repetition. Optional active play must be mechanically connected to progression/content, not just passive sightseeing added to inflate session length.

## Workstream C — decontaminate repository naming

Search the entire Simple Walk Game repository for `WalkGame` and classify every occurrence.

Replace Simple-Walk-Game implementation names with a distinct namespace/solution identity. Preferred default:

```text
SimpleWalkGame.Domain
SimpleWalkGame.Application
SimpleWalkGame.Infrastructure
SimpleWalkGame.Unity
SimpleWalkGame.Domain.Tests
SimpleWalkGame.Application.Tests
SimpleWalkGame.Infrastructure.Tests
SimpleWalkGame.Sim
```

Do not rename generic prose references that intentionally discuss the other repository as comparison/history, but label those references clearly.

Also scan for:

- `Ashfall` / `Ashfall Basin`;
- Walk Game-specific phases/milestones;
- Builder/Explore assumptions copied as mandatory without independent justification;
- Walk Game-specific class/file paths;
- Walk Game-specific test counts/status;
- Walk Game-specific native-provider choices copied without a Simple Walk Game decision;
- `quantdale/walk-game` URLs/branch names in Simple Walk Game planning.

## Workstream D — independently re-evaluate inherited systems

Do not delete good ideas merely because both projects use them. Instead, independently justify each major Simple Walk Game system against the repaired product thesis.

Re-evaluate at least:

- movement/activity as primary input;
- Vitality or equivalent activity-derived progression;
- restoration/world transformation;
- project queue/automation;
- producers/offline progression;
- expeditions;
- discoveries;
- optional or central 3D world play;
- Unity as presentation runtime;
- Health Connect / HealthKit or other platform activity sources;
- deterministic domain architecture;
- offline-first local persistence;
- exactly-once reward processing.

Keep what is justified. Reshape or remove what is not. Engineering trust principles such as deterministic state, save integrity, privacy, migration safety, idempotency, and evidence-based verification may remain if they serve the repaired product.

## Workstream E — repair the decision log

Audit every current decision in `docs/DECISIONS.md`.

For each decision:

- verify against actual Simple Walk Game intent;
- keep it if independently justified;
- amend it if partly correct;
- explicitly mark it Superseded or Rejected if the contaminated docs made it incorrect.

Add decisions that explicitly record:

1. Simple Walk Game is separate from `quantdale/walk-game` and uses a distinct internal namespace/product identity.
2. Low maintenance burden is not the same as minimizing voluntary engagement; the game should support meaningful active play without coercion.

Preserve decision history. Do not silently erase superseded reasoning.

## Workstream F — rebuild the roadmap from repaired truth

Once the product/UX/system contracts are coherent, rebuild `docs/ROADMAP.md` from dependencies and risks.

Do not automatically preserve the existing M1–M9 sequence.

Identify the highest-risk assumptions that must be proven first, including product engagement risk—not only data-integrity risk.

If a deterministic trust kernel is still the correct first implementation campaign, retain it for evidence-backed reasons and rename all internals to `SimpleWalkGame.*`. If another implementation slice should come first, document why.

The roadmap must clearly separate:

- product/engagement validation;
- core gameplay risk;
- activity/movement data risk;
- persistence/migration risk;
- runtime/platform risk;
- content/UX risk;
- release qualification.

## Workstream G — repair testing and success criteria

Update acceptance criteria so the project can detect whether the repaired design actually works.

Where applicable, include measures for:

- meaningful voluntary session depth;
- repeatable active gameplay quality;
- player reasons to continue playing beyond a quick check-in;
- progression clarity;
- movement-to-gameplay connection;
- avoidance of mandatory busywork;
- retention/re-entry without punishment;
- correctness, save integrity, privacy, accessibility, performance, and device evidence.

Do not invent user-research results. Define what future testing should measure.

## Workstream H — supersede the old M1 campaign

After the docs are repaired and internally consistent:

1. perform a fresh whole-repository audit;
2. derive the next implementation campaign from the repaired repository truth;
3. replace this repair prompt with the new ACTIVE implementation campaign, or mark this prompt COMPLETE and create the repository's normal next-campaign handoff according to local protocol;
4. explicitly record that the old `7c7687e` M1 prompt was superseded before implementation because its assumptions were derived from the pre-repair contract;
5. never execute the old prompt merely because its engineering workstreams look reasonable in isolation.

## Validation / acceptance gates

This repair is complete only when all of the following are true:

1. Every repository-specific document has been reviewed, not just the obvious ones.
2. README, Master Plan, Product Spec, UX, Game Systems, World/Content, Roadmap, Architecture, Testing, Risks, Decisions, and Agent Guide tell one coherent story.
3. The product no longer treats minimal foreground time as an accidental universal north star if the actual objective is richer engagement.
4. Meaningful voluntary active play is explicitly designed and measurable if retained in the repaired thesis.
5. Quick/low-maintenance use can still exist without making deeper play pointless.
6. No planned Simple Walk Game code/project namespace defaults to `WalkGame.*`.
7. Cross-repo contamination terms have been searched and reviewed across the whole repository.
8. Shared generic agent plumbing remains intact unless independently broken.
9. Major inherited systems are independently justified, not kept by inertia.
10. Existing decisions are reconciled with explicit Superseded/Rejected history where necessary.
11. The roadmap is rebuilt from the repaired product dependency chain.
12. Testing/release criteria measure the repaired product and engineering risks.
13. No unresolved Critical/High documentation contradiction remains.
14. A newly derived next implementation campaign exists after the repair; the old M1 campaign is not resumed.
15. All repair changes are committed and pushed to `repair/product-intent-and-repo-identity`.

## Git / integration requirements

- Work on `repair/product-intent-and-repo-identity` until the repair is complete.
- Do not modify `main` during incomplete repair work.
- Preserve unrelated user changes.
- Commit coherent checkpoints as useful.
- At completion, leave the repair branch clean and pushed.
- If the executing session has explicit authority to integrate after all gates pass, merge/fast-forward the completed repair into `main`, push `main`, and ensure the superseded M1 prompt is not left ACTIVE there.
- If integration authority is not clear, stop with the repair branch ready to merge and report its final SHA.

## Non-goals

- Do not implement production gameplay code during this repair.
- Do not rewrite `quantdale/walk-game` to make it artificially different.
- Do not remove generic reusable agent adapters simply because their hashes match across repositories.
- Do not fabricate analytics, playtest, device, or user-research evidence.
- Do not turn engagement goals into manipulative retention mechanics.
- Do not assume a specific AI model, harness, operating system, or sub-agent system.

## Completion report

When finished, append a concise executor report containing:

- start SHA and final SHA;
- documents changed;
- major intent corrections;
- namespace/repository-identity corrections;
- decisions superseded/retained;
- contamination scan findings;
- newly selected next implementation campaign and rationale;
- whether repair was merged into `main` or left on the repair branch;
- any remaining uncertainties requiring user/product input.
