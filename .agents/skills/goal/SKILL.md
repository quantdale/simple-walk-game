---
name: goal
description: Resume the repository's planner-generated or native development campaign.
type: prompt
whenToUse: When asked to continue, resume, execute, or finish the current development goal.
disableModelInvocation: false
---
Read applicable `AGENTS.md`, `.agent/PLANNER_HANDOFF.md`, `.agent/EXECUTION_PROMPT.md` if present, and native state.

MANDATORY PREFLIGHT (no bypass): run `scripts/assert-repo-identity.sh` (exit 86 = wrong repository: STOP, touch nothing), then acquire the single-writer lease (`scripts/writer-lease.sh acquire`; exit 87 = another writer owns this tree: STOP). `AGENTS.md` wins over any external prompt.

Reconcile current Git with Planned-From. Resume an ACTIVE prompt from the first incomplete requirement through validation/state/commit/push; otherwise use native continuation or require planning. Never force-push; fetch origin and reconcile deliberately before integrating, then release the lease.