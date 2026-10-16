# Add a Region 2 storyline and content (PROPOSED — human decision gate)

**Status:** Proposed. **Not approved for implementation.** D-011 says Region 1 must be release-qualified before Region 2 production, and the ROADMAP post-MVP decision gate says "After M9, do not automatically begin Region 2 … None are pre-authorized by this roadmap." M9 is not exited: the device lane is blocked (KI-002). An agent may not approve this change. It waits in the KNOWN_ISSUES morning queue for Michael's decision.

## Why

Region 1 (Millbrook Valley) has a complete arc: 19 projects, discoveries, expeditions, closure (D-061), a ten-chapter storyline (D-076) and an epilogue on Home (D-090). After closure, the post-completion care program (D-061) keeps walking meaningful but adds no new story. WORLD_AND_CONTENT §10 (item 10) and MASTER_PLAN §"One complete region before Region 2" want Region 2 to be *expansion*, not compensation. This proposal records what that expansion would be, so the decision gate has a concrete option to judge.

## What Changes (if approved)

- **A second authored region** (`Region2Catalog`, working title "Ashford Downs", an upland of dry-stone walls, a silted reservoir and a closed observatory), built with the same `RegionDefinition` machinery as `Region1Catalog`: projects, chains, discoveries, expeditions, landmarks and chapters. No parallel content path.
- **Storyline continuity:** Region 2 chapters reuse `StoryChapterDefinition` and `StoryProgress.Evaluate` unchanged. The Region 2 prologue requires Region 1 closure; chapter rules (milestones only, in order, never relock) are inherited from the `storyline` capability.
- **Unlock:** Region 2 opens only after Region 1 closure, as an offer on Home ("A path leads over the ridge"). Region 1 stays visitable and its care program continues. Nothing is taken away.
- **Save and content compatibility:** new canonical state is needed (which region is current, Region 2 project progress). That needs a schema bump, a registered `ISaveMigration` and a mature-save fixture test (openspec/config.yaml rule). Content version 5 → 6 is additive.
- **Pacing:** Region 2 has its own pacing contract per §13 profile, checked by the existing pacing harness and committed evidence (`verify-pacing-evidence.sh`).

## Out of scope

Region 3, multiple simultaneous active regions, region choice/branching, any network content delivery, new activity categories, new resources, monetisation, and any change to Region 1 numbers (its pacing evidence must reproduce byte-for-byte).

## Compatibility and dependencies

- Depends on: the post-MVP decision gate (ROADMAP), which depends on M9 exit (KI-002 device lane).
- Save: schema change with migration; a Region-1-only save must load as Region 1 with Region 2 locked until closure is (re)derived.
- Fail closed: a save that names an unknown region id is `ContentIncompatible`, never reset (D-059).

## Suggested first slice (only after approval)

Domain-only: `RegionDefinition` list in a `WorldDefinition`, `Region2Catalog` with the prologue chapter and 3 projects, validator rules, and tests. No save change yet, no Unity work.
