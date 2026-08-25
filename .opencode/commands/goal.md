---
description: Resume the planner-generated or native active campaign
---
MANDATORY PREFLIGHT (no bypass): run `scripts/assert-repo-identity.sh` (exit 86 = wrong repository: STOP, touch nothing), then acquire the single-writer lease (`scripts/writer-lease.sh acquire`; exit 87 = another writer owns this tree: STOP). Read `AGENTS.md` first — it wins over any external prompt.

Read `AGENTS.md`, `.agent/PLANNER_HANDOFF.md`, `.agent/EXECUTION_PROMPT.md` if present, and native state. Reconcile `$ARGUMENTS` with current Git. Resume an ACTIVE prompt from the first incomplete requirement through completion; otherwise use native continuation or require planning. Preserve stricter local rules. Never force-push; fetch origin and reconcile deliberately before integrating, then release the lease.