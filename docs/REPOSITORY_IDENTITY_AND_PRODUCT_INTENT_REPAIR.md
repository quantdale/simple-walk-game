# Simple Walk Game — Repository Identity & Product-Intent Repair

**Status:** REQUIRED BEFORE IMPLEMENTATION  
**Repository:** `quantdale/simple-walk-game`  
**Repair branch:** `repair/product-intent-and-repo-identity`  
**Baseline:** `main@7c7687e8e9d908db623935fd2d9f3c46f675ff4d`  
**Affected planning origin:** documentation overhaul commit `0d9afb656714cc22c6341cb84482ff5fb51bac68` and the M1 planner commit built on top of it.

## 1. Why this repair exists

`quantdale/simple-walk-game` and `quantdale/walk-game` are separate products and must remain separate in naming, product goals, roadmap, implementation decisions, and campaign selection.

A cross-repository audit found that Simple Walk Game's current documentation and active campaign contain material signs of product-intent inversion and naming bleed:

- the repository was effectively empty before the documentation overhaul;
- the entire current Simple Walk Game product/engineering contract was introduced in one documentation commit;
- that contract strongly optimizes for minimal foreground attention and explicitly rejects maximizing session length;
- the prior user intent associated with the Simple Walk Game overhaul was to address insufficient meaningful player time / engagement, so the current low-attention doctrine may invert the requested objective;
- Simple Walk Game's proposed internal solution/project names are `WalkGame.Domain`, `WalkGame.Application`, `WalkGame.Infrastructure`, `WalkGame.Unity`, and `WalkGame.Sim`, which collide conceptually with the distinct `quantdale/walk-game` repository;
- the current ACTIVE M1 execution prompt repeats those `WalkGame.*` names and derives its implementation priorities from the potentially contaminated documentation.

No production Simple Walk Game implementation has landed yet. This makes the repair cheap and should be completed before any M1 implementation work begins.

## 2. Repository identity contract

Treat these repositories as permanently distinct:

### `quantdale/walk-game`

A mature Unity restoration-builder with an existing Ashfall Basin vertical slice, Builder/Explore synchronization, Unity runtime code, native movement providers, domain verification, persistence, and a current M8.1 save-integrity hardening campaign.

Do not copy its product assumptions, milestone numbering, Ashfall-specific content, internal namespaces, implementation status, or current hardening work into Simple Walk Game unless a decision is independently justified for Simple Walk Game.

### `quantdale/simple-walk-game`

A separate product. It currently has documentation and agent handoff files but no production `src/`, Unity project, tests, or CI implementation at the repair baseline.

The repair must establish an independent product identity and independent internal namespace. Use `SimpleWalkGame.*` (or another explicitly Simple-Walk-Game-specific namespace chosen and documented during the repair), never `WalkGame.*` by default.

## 3. What is safe to share

The following shared infrastructure is intentional and does not need to be forked merely to make the repositories look different:

- generic `.agents` goal skill;
- generic `.claude` goal command;
- generic `.kimi-code` goal adapter;
- generic `.opencode` goal command;
- generic `.agent/PLANNER_HANDOFF.md` protocol.

Shared planner/executor plumbing is fine. Product requirements, code namespaces, milestones, implementation prompts, and repository-specific docs are not generic and must be independently grounded.

## 4. Product-intent repair objective

Reconstruct Simple Walk Game's actual product contract from repository history and user intent, rather than assuming the current documentation is correct merely because it is comprehensive.

The key contradiction to resolve is:

> The current docs make extremely low required screen time a hard north-star constraint, while the overhaul request that led to this repository state was intended to address insufficient meaningful player time / engagement.

Do not solve this by simply maximizing addiction metrics, taps, nagging, or forced session length. The repaired direction should distinguish:

- **required maintenance time** — should remain low and respectful;
- **meaningful optional active play** — should be substantially stronger, richer, and more compelling than the current documentation allows;
- **foreground engagement quality** — should create reasons a player *wants* to stay, not obligations that force them to stay;
- **real-world movement integration** — should remain meaningful if still part of the independently confirmed product thesis;
- **retention/engagement goals** — should be explicit, testable product outcomes rather than accidentally defined as “shorter is always better.”

The repaired docs must make it possible for Simple Walk Game to support both fast utility/check-in flows and genuinely compelling longer play sessions when that matches the intended product.

## 5. Required audit before editing

Before changing documents, inspect the entire Simple Walk Game repository and recent history, including:

- first commit `563155b71250e2091b26fecc3db6a9f46850f67e`;
- documentation overhaul `0d9afb656714cc22c6341cb84482ff5fb51bac68`;
- agent-adapter commits through `1c9a7ee1aae8aa83426162f0f5c491f875508692`;
- M1 planner commit `7c7687e8e9d908db623935fd2d9f3c46f675ff4d`;
- all files under `docs/`;
- `README.md`;
- `.agent/EXECUTION_PROMPT.md`;
- `.agent/PLANNER_HANDOFF.md`.

Also inspect `quantdale/walk-game` only as a comparison/control to detect copied assumptions. Do not use Walk Game as the source of truth for Simple Walk Game.

## 6. Required documentation corrections

Audit and reconcile every repository-specific document, not just the lines already identified.

At minimum review and repair:

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

### Mandatory corrections

1. Remove accidental `WalkGame.*` naming from Simple Walk Game architecture and planned implementation. Use a distinct namespace such as `SimpleWalkGame.*` and a distinct solution/tool naming scheme.
2. Remove Ashfall-specific or mature-Walk-Game implementation assumptions if any are found.
3. Re-evaluate every “low attention,” “minimal screen time,” “under one minute,” “do not maximize session length,” and similar rule against actual Simple Walk Game intent.
4. Preserve low-friction check-ins only where useful; do not make minimal foreground time the automatic overriding product objective if the goal is richer engagement.
5. Add explicit design goals for **meaningful voluntary active play** and measurable engagement quality.
6. Define what players can actively do for 5–20+ minutes (and, if appropriate, longer) that is mechanically meaningful, replayable, and connected to progression rather than merely optional sightseeing.
7. Ensure progression is not so automated that the game becomes a passive dashboard with little reason to play.
8. Ensure active gameplay does not become repetitive busywork added only to inflate time spent.
9. Re-evaluate whether every major system inherited from the current docs—Vitality, restoration, project queues, producers, expeditions, discoveries, Visit World, Unity, Health Connect/HealthKit, etc.—is independently justified for Simple Walk Game. Keep good ideas; remove or reshape copied assumptions.
10. Keep safety, privacy, save integrity, deterministic state, idempotent reward handling, and evidence-based qualification where they still serve the product; those are engineering quality principles, not repo contamination by themselves.

## 7. Architecture/name repair

If the repaired architecture remains C#/.NET/Unity-oriented, use names that cannot be confused with `quantdale/walk-game`.

Preferred default unless a better name is explicitly chosen:

```text
src/
  SimpleWalkGame.Domain/
  SimpleWalkGame.Application/
  SimpleWalkGame.Infrastructure/
  SimpleWalkGame.Unity/

tests/
  SimpleWalkGame.Domain.Tests/
  SimpleWalkGame.Application.Tests/
  SimpleWalkGame.Infrastructure.Tests/

tools/
  SimpleWalkGame.Sim/
```

Search the entire repository for `WalkGame` and classify every occurrence. Repository-generic prose about the other project may remain only when clearly labeled as comparison/history; implementation namespaces and Simple Walk Game code examples must not use it.

## 8. Decision-log repair

`docs/DECISIONS.md` currently marks several product choices as Accepted that may have been created by the contaminated documentation overhaul.

Do not silently preserve them simply because they are marked Accepted.

For each existing decision:

- verify it against actual Simple Walk Game intent;
- retain if independently justified;
- amend if partly correct;
- mark Superseded/Rejected if it conflicts with the repaired product direction;
- add a new decision recording the repository identity and namespace separation from `quantdale/walk-game`;
- add a decision explicitly distinguishing low required maintenance from meaningful voluntary engagement.

Decision history should remain understandable; do not erase evidence of superseded direction without explanation.

## 9. Roadmap repair

After the product contract is repaired, rebuild the roadmap dependency chain from that contract.

Do not automatically preserve the current M1 → M9 sequence.

The next implementation campaign should be whichever campaign best proves the repaired Simple Walk Game thesis. If a deterministic trust kernel remains the correct first dependency, keep it—but with Simple-Walk-Game-specific names and revised acceptance criteria. If a different foundation is needed, document why.

The roadmap must separate:

- product validation risks;
- core gameplay/engagement risks;
- movement/activity integration risks;
- persistence/data-integrity risks;
- runtime/platform risks;
- content/UX risks;
- release qualification.

## 10. Active campaign repair

The current `.agent/EXECUTION_PROMPT.md` on `main` is not safe to execute until this repair is complete.

On the repair branch:

1. use the branch-local execution prompt to perform this documentation/product repair first;
2. once documentation is internally consistent, re-audit the repository from scratch;
3. replace the invalidated M1 campaign with a newly derived next implementation campaign based on the repaired docs;
4. do not implement the old M1 campaign merely because parts of it are technically reasonable;
5. do not mark the old campaign complete—supersede it explicitly so history is clear.

## 11. Cross-repository contamination scan

Before declaring repair complete, search Simple Walk Game for terms/concepts that may have leaked from Walk Game, including at minimum:

- `WalkGame` namespaces/project names;
- `Ashfall` / `Ashfall Basin`;
- Builder View / Explore View assumptions;
- Core Motion / `TYPE_STEP_COUNTER` assumptions that were copied rather than independently chosen;
- Walk Game milestone/phase numbering;
- Walk Game-specific test counts or verification claims;
- Walk Game-specific file paths/classes;
- persistence campaign wording from Walk Game;
- any repo URL or branch name pointing at `quantdale/walk-game`.

Not every shared concept is contamination. Classify each occurrence by whether it is independently justified in Simple Walk Game.

Also verify that `quantdale/walk-game` itself does not contain Simple-Walk-Game-specific product doctrine or namespace changes. The previous audit found no material reverse contamination; re-check if repository state has changed.

## 12. Validation gates

The repair is complete only when:

- all Simple Walk Game docs tell one coherent product story;
- the docs no longer encode “shorter session length is always better” as an accidental universal objective;
- meaningful voluntary active play has explicit mechanics, goals, and acceptance criteria if it is part of the repaired intent;
- no Simple Walk Game implementation plan uses `WalkGame.*` namespaces;
- all decisions are reconciled rather than silently contradictory;
- roadmap and product spec agree;
- architecture and roadmap agree;
- UX and product goals agree;
- testing/release gates measure the repaired product rather than the contaminated one;
- a whole-repository `WalkGame` contamination search is reviewed;
- the next implementation campaign is freshly derived from repaired repository truth;
- there are no unresolved Critical/High documentation contradictions;
- all changes are committed and pushed on the repair branch.

## 13. Main-branch integration rule

Do not mutate `main` while the repair is incomplete.

When the repair branch is coherent and reviewed by the executing session:

- leave a clean, pushed branch with the repaired docs and superseding execution prompt;
- if the session has explicit authority to integrate, fast-forward/merge the repair into `main` only after all repair gates pass;
- otherwise stop with the repair branch ready for review/merge and report the exact final SHA.

No production implementation should begin on top of the contaminated `main` documentation before this repair is integrated.

## 14. Non-goals

- Do not rewrite `quantdale/walk-game` merely to make it different.
- Do not remove generic shared agent adapters that are intentionally reusable.
- Do not add production code during the documentation repair unless a tiny tooling check is strictly necessary to validate the docs.
- Do not invent player research evidence that does not exist.
- Do not turn “more meaningful player time” into dark patterns, forced grind, notification spam, punishment loops, or unsafe exercise incentives.
- Do not assume a specific AI model, harness, operating system, or sub-agent system.

## 15. Handoff summary

The immediate priority is not M1 implementation. The immediate priority is to restore Simple Walk Game's independent product identity, correct its engagement objective, remove `WalkGame.*` naming bleed, reconcile all repository-specific documents, and then derive a new implementation campaign from the repaired truth.
