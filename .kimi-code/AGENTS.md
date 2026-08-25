# Goal Adapter

MANDATORY PREFLIGHT (no bypass): run `scripts/assert-repo-identity.sh` (exit 86 = wrong repository: STOP, touch nothing), then acquire the single-writer lease (`scripts/writer-lease.sh acquire`; exit 87 = another writer owns this tree: STOP). Read `AGENTS.md` first — it wins over any external prompt.

For `/goal continue`, preserve local `AGENTS.md`; read `.agent/PLANNER_HANDOFF.md`, `.agent/EXECUTION_PROMPT.md` if present, and native state; reconcile current Git; resume an ACTIVE prompt from the first incomplete requirement through validation/state/commit/push. Otherwise use native continuation or require planning. Never force-push; fetch origin and reconcile deliberately before integrating, then release the lease.