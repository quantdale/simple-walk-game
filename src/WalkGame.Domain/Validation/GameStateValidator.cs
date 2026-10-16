using System;
using System.Collections.Generic;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Summaries;

namespace WalkGame.Domain.Validation
{
    /// <summary>
    /// Validates canonical runtime state against its content definitions and internal
    /// invariants. Used after load/migration and by tooling; violations indicate real
    /// corruption, not recoverable game conditions.
    /// </summary>
    public static class GameStateValidator
    {
        public static List<string> Validate(GameState state, RegionDefinition content)
        {
            var violations = new List<string>();
            if (state == null)
            {
                violations.Add("Game state is null.");
                return violations;
            }
            if (content == null)
            {
                violations.Add("Content definition is null.");
                return violations;
            }

            if (state.SchemaVersion != SchemaVersions.Current)
                violations.Add($"State schema version {state.SchemaVersion} does not match current {SchemaVersions.Current}.");

            if (!string.Equals(state.Region.RegionId, content.Id.Value, StringComparison.Ordinal))
                violations.Add($"State region '{state.Region.RegionId}' does not match loaded content '{content.Id}'.");

            foreach (var pair in state.Resources.Amounts)
            {
                if (pair.Value < 0L)
                    violations.Add($"Resource '{pair.Key}' has negative balance {pair.Value}.");

                // A balance above the authored cap is reported, NEVER clamped (D-054).
                // Clamping on load would silently discard a player's progress and hide a
                // content change that reduced a cap under a live save; failing closed is
                // the posture every other invariant here already takes.
                long? cap = content.ResourceCap(pair.Key);
                if (cap.HasValue && pair.Value > cap.Value)
                    violations.Add($"Resource '{pair.Key}' balance {pair.Value} exceeds the content cap {cap.Value}.");
            }

            var knownProjectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in content.Projects)
                knownProjectIds.Add(definition.Id.Value);

            foreach (var definition in content.Projects)
            {
                var runtime = state.Region.FindProject(definition.Id.Value);
                if (runtime == null)
                {
                    violations.Add($"Missing runtime state for project '{definition.Id}'.");
                    continue;
                }

                if (runtime.VitalityInvested < 0L || runtime.VitalityInvested > definition.VitalityCost)
                {
                    // A project funded under a HIGHER authored cost (D-062: the v2 → v3
                    // rebalance) is legacy over-funding, and it is only sound for a COMPLETED
                    // project: an incomplete project holding more than its cost would be
                    // unspendable, while a completed one is simply a record of what the
                    // restoration actually cost that player. Refusing the state here would
                    // fail a mature profile closed on the very definitions it was earned
                    // against — the data-loss shape this validator exists to prevent.
                    if (runtime.Status != ProjectStatus.Completed || runtime.VitalityInvested <= definition.VitalityCost)
                        violations.Add($"Project '{definition.Id}' invested vitality out of bounds: {runtime.VitalityInvested}/{definition.VitalityCost}.");
                }

                if (runtime.Status == ProjectStatus.Completed && runtime.CompletedAtUtc == null)
                    violations.Add($"Completed project '{definition.Id}' has no completion timestamp.");

                if (knownProjectIds.Count > 0 && runtime.Status == ProjectStatus.Active && definition.Prerequisites.Count > 0)
                {
                    // Prerequisite satisfaction for non-entry projects is validated below via statuses.
                }
            }

            foreach (var pair in state.Region.Projects)
            {
                if (knownProjectIds.Contains(pair.Key))
                    continue;

                // D-059: a RETIRED identifier resolves to its tombstone instead of failing
                // closed. Content was removed or renamed on purpose, and the save's
                // reference is the record of that old content -- refusing it would destroy
                // a mature profile for a change that was reviewed and intentional. The
                // reference stays RESOLVABLE and grants NOTHING (see below).
                var tombstone = content.FindTombstone(pair.Key);
                if (tombstone == null)
                    violations.Add($"Runtime project '{pair.Key}' is unknown to content definitions.");
                else if (pair.Value != null && pair.Value.Status == ProjectStatus.Active)
                {
                    // A retired project must not sit in the active slot granting progress.
                    violations.Add($"Runtime project '{pair.Key}' is tombstoned ({tombstone.Reason}) but is still ACTIVE; a retired project cannot be completed.");
                }
            }

            // Queue/status consistency.
            var queue = state.Queue;
            if (queue.ActiveProjectId != null)
            {
                var active = state.Region.FindProject(queue.ActiveProjectId);
                if (active == null)
                    violations.Add($"Queue references unknown active project '{queue.ActiveProjectId}'.");
                else if (active.Status != ProjectStatus.Active)
                    violations.Add($"Active project '{queue.ActiveProjectId}' has inconsistent status '{active.Status}'.");
            }

            var queuedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var queuedId in queue.QueuedProjectIds)
            {
                if (!queuedIds.Add(queuedId))
                    violations.Add($"Project '{queuedId}' appears twice in the queue.");
                var runtime = state.Region.FindProject(queuedId);
                if (runtime == null)
                    violations.Add($"Queue references unknown project '{queuedId}'.");
                else if (runtime.Status != ProjectStatus.Queued)
                    violations.Add($"Queued project '{queuedId}' has inconsistent status '{runtime.Status}'.");
            }

            // Landmark stage bounds. Ruined is the canonical implicit initial stage.
            foreach (var landmark in content.Landmarks)
            {
                if (!state.Region.LandmarkStages.TryGetValue(landmark.Id.Value, out var stage))
                    continue;
                bool defined = stage == RestorationStage.Ruined;
                foreach (var stageDefinition in landmark.Stages)
                    if (stageDefinition.Stage == stage)
                        defined = true;
                if (!defined)
                    violations.Add($"Landmark '{landmark.Id}' has stage '{stage}' not present in its content definition.");
            }

            // Producer runtimes. Every content producer must own a runtime row: rows are
            // created at game start for the full content set, so a missing row means
            // silent corruption — the producer would never produce or unlock again.
            var knownProducerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in content.Producers)
                knownProducerIds.Add(definition.Id.Value);
            foreach (var definition in content.Producers)
            {
                if (state.Region.FindProducer(definition.Id.Value) == null)
                    violations.Add($"Missing runtime state for producer '{definition.Id}'.");
            }
            foreach (var producer in state.Region.Producers)
            {
                var definition = content.FindProducer(producer.ProducerId);
                if (definition == null)
                {
                    // D-059: a retired producer resolves to its tombstone, but must not be
                    // unlocked and must not hold produced output — otherwise retiring a
                    // producer would leave it minting.
                    var tombstone = content.FindTombstone(producer.ProducerId);
                    if (tombstone == null)
                    {
                        violations.Add($"Producer runtime '{producer.ProducerId}' is unknown to content definitions.");
                    }
                    else if (producer.Unlocked)
                    {
                        violations.Add($"Producer '{producer.ProducerId}' is tombstoned ({tombstone.Reason}) but is unlocked; a retired producer cannot produce.");
                    }
                    continue;
                }
                long storeCapacityMilliUnits = definition.CapacityUnits * ProducerDefinition.MilliUnitsPerUnit;
                if (producer.StoredMilliUnits < 0L || producer.StoredMilliUnits > storeCapacityMilliUnits)
                    violations.Add($"Producer '{producer.ProducerId}' pending store out of bounds: {producer.StoredMilliUnits}/{storeCapacityMilliUnits}.");
                if (producer.TotalProducedMilliUnits < 0L)
                    violations.Add($"Producer '{producer.ProducerId}' total produced is negative.");
                if (producer.Unlocked)
                {
                    var unlocker = state.Region.FindProject(definition.UnlockedByProjectId);
                    if (unlocker == null || unlocker.Status != ProjectStatus.Completed)
                        violations.Add($"Producer '{producer.ProducerId}' is unlocked but its unlock project is not completed.");
                }
                if (!producer.Unlocked && (producer.TotalProducedMilliUnits > 0L || producer.StoredMilliUnits != 0L))
                    violations.Add($"Locked producer '{producer.ProducerId}' has produced output.");
            }

            // Discovery runtimes. Entries exist only once unlocked; absence is valid.
            foreach (var pair in state.Region.Discoveries)
            {
                var definition = content.FindDiscovery(pair.Key);
                if (definition == null)
                {
                    // D-059: a retired discovery resolves to its tombstone and simply stops
                    // being renderable content. It grants nothing, which is already true:
                    // a discovery has no completion effect.
                    if (content.FindTombstone(pair.Key) == null)
                        violations.Add($"Discovery runtime '{pair.Key}' is unknown to content definitions.");
                    continue;
                }
                var discovery = pair.Value;
                if (discovery == null)
                {
                    violations.Add($"Discovery runtime '{pair.Key}' is null.");
                    continue;
                }
                if (!string.Equals(discovery.DiscoveryId, pair.Key, StringComparison.Ordinal))
                    violations.Add($"Discovery dictionary key '{pair.Key}' does not match runtime ID '{discovery.DiscoveryId}'.");
                if (discovery.DiscoveredAtUtc == default)
                    violations.Add($"Discovery '{pair.Key}' has no discovery timestamp.");
                if (discovery.Reviewed != discovery.ReviewedAtUtc.HasValue)
                    violations.Add($"Discovery '{pair.Key}' reviewed flag and timestamp are inconsistent.");
                if (discovery.ReviewedAtUtc.HasValue && discovery.ReviewedAtUtc.Value < discovery.DiscoveredAtUtc)
                    violations.Add($"Discovery '{pair.Key}' was reviewed before it was discovered.");
            }

            // Expedition runtimes. Entries exist only once available; absence means locked.
            foreach (var pair in state.Region.Expeditions)
            {
                var definition = content.FindExpedition(pair.Key);
                if (definition == null)
                {
                    // D-059: a retired expedition resolves to its tombstone. It must not be
                    // recorded as COMPLETED, because completion is what pays its reward.
                    var tombstone = content.FindTombstone(pair.Key);
                    if (tombstone == null)
                    {
                        violations.Add($"Expedition runtime '{pair.Key}' is unknown to content definitions.");
                    }
                    else if (pair.Value != null && pair.Value.CompletedAtUtc != null)
                    {
                        violations.Add($"Expedition '{pair.Key}' is tombstoned ({tombstone.Reason}) but is recorded as completed; a retired expedition cannot award.");
                    }
                    continue;
                }
                var expedition = pair.Value;
                if (expedition == null)
                {
                    violations.Add($"Expedition runtime '{pair.Key}' is null.");
                    continue;
                }
                if (!string.Equals(expedition.ExpeditionId, pair.Key, StringComparison.Ordinal))
                    violations.Add($"Expedition dictionary key '{pair.Key}' does not match runtime ID '{expedition.ExpeditionId}'.");
                if (expedition.AvailableAtUtc == default)
                    violations.Add($"Expedition '{pair.Key}' has no availability timestamp.");
                if (expedition.CompletedAtUtc.HasValue && expedition.CompletedAtUtc.Value < expedition.AvailableAtUtc)
                    violations.Add($"Expedition '{pair.Key}' completed before it became available.");
            }

            // Region progression arc bounds (monotonic advance is enforced by construction).
            if (state.Region.EcologyStage < 0 || state.Region.EcologyStage > content.EcologyProgression.Stages.Count)
                violations.Add($"Ecology stage {state.Region.EcologyStage} is outside its content arc (0..{content.EcologyProgression.Stages.Count}).");
            if (state.Region.SettlementStage < 0 || state.Region.SettlementStage > content.SettlementProgression.Stages.Count)
                violations.Add($"Settlement stage {state.Region.SettlementStage} is outside its content arc (0..{content.SettlementProgression.Stages.Count}).");

            // Region completion consistency.
            if (state.Region.IsCompleted)
            {
                if (string.IsNullOrEmpty(content.CompletionMilestoneProjectId))
                    violations.Add("Region is marked completed but its content defines no completion milestone.");
                else
                {
                    var milestone = state.Region.FindProject(content.CompletionMilestoneProjectId);
                    if (milestone == null || milestone.Status != ProjectStatus.Completed)
                        violations.Add($"Region is marked completed but milestone project '{content.CompletionMilestoneProjectId}' is not completed.");
                }
                if (state.Region.RegionCompletedAtUtc == null)
                    violations.Add("Region is marked completed without a completion timestamp.");
            }
            else if (state.Region.RegionCompletedAtUtc != null)
            {
                violations.Add("Region has a completion timestamp but is not marked completed.");
            }

            // Closure must satisfy the AUTHORED criterion, whichever route produced it
            // (D-062). A completion that no route explains is corruption, and it is exactly the
            // kind of flag that would otherwise silently redefine the ending's meaning.
            if (state.Region.IsCompleted)
            {
                var criterion = content.ClosureCriterion;
                if (criterion != null && !content.MeetsClosureCriterion(state.Region))
                {
                    violations.Add(
                        "Region is marked completed but its authored closure criterion is not satisfied: milestone '"
                        + criterion.MilestoneProjectId + "' is incomplete and "
                        + content.ChainsComplete(state.Region) + " of " + content.Chains.Count
                        + " chains are complete against a requirement of " + criterion.MinimumChainsComplete + ".");
                }
                else if (criterion == null && !content.MeetsClosureCriterion(state.Region))
                {
                    violations.Add(
                        "Region is marked completed but its closure milestone project '"
                        + (content.CompletionMilestoneProjectId ?? "(none authored)") + "' is not completed.");
                }
            }

            // Post-completion program progress (D-061). The state is additive and derived from
            // authored content, so every rule here is checkable against that content: an
            // invested amount above a single cycle means a cycle failed to complete, progress
            // recorded before completion means the sink ran out of order, and progress under
            // content that declares no program is progress with nowhere to live.
            long maxCycleCost = 0L;
            foreach (var program in content.PostCompletionPrograms)
                if (program.VitalityCostPerCycle > maxCycleCost)
                    maxCycleCost = program.VitalityCostPerCycle;

            if (state.Region.PostCompletionInvestedVitality < 0L)
                violations.Add($"Post-completion invested vitality is negative: {state.Region.PostCompletionInvestedVitality}.");
            else if (maxCycleCost > 0L && state.Region.PostCompletionInvestedVitality >= maxCycleCost)
                violations.Add(
                    $"Post-completion invested vitality {state.Region.PostCompletionInvestedVitality} reaches the authored cycle cost {maxCycleCost}; "
                    + "reaching the cycle cost completes a cycle and leaves only the remainder invested.");
            if (state.Region.PostCompletionCyclesCompleted < 0L)
                violations.Add($"Post-completion cycle count is negative: {state.Region.PostCompletionCyclesCompleted}.");

            if (content.PostCompletionPrograms.Count == 0
                && (state.Region.PostCompletionInvestedVitality > 0L || state.Region.PostCompletionCyclesCompleted > 0L))
                violations.Add("Post-completion progress is recorded but the region authors no post-completion program.");

            if (!state.Region.IsCompleted
                && (state.Region.PostCompletionInvestedVitality > 0L || state.Region.PostCompletionCyclesCompleted > 0L))
                violations.Add("Post-completion progress is recorded before the region was completed.");

            // xoshiro256** with an all-zero state emits zeros forever; seeding can never produce it,
            // so only a corrupted or tampered save can (security review 2026-10-16).
            if (state.Rng.S0 == 0UL && state.Rng.S1 == 0UL && state.Rng.S2 == 0UL && state.Rng.S3 == 0UL)
                violations.Add("RNG state is all zero (degenerate generator).");

            // Durable pending return summary.
            var pendingSummary = state.PendingReturnSummary;
            if (pendingSummary != null)
            {
                if (pendingSummary.Items == null)
                    violations.Add("Pending return summary has a null item list.");
                else
                {
                    if (pendingSummary.Items.Count > PendingReturnSummaryState.MaxItems)
                        violations.Add($"Pending return summary exceeds the hard item bound ({pendingSummary.Items.Count}).");
                    foreach (var item in pendingSummary.Items)
                        if (item == null || string.IsNullOrWhiteSpace(item.Text))
                            violations.Add("Pending return summary contains an empty item.");
                        else if (item.Text.Length > PendingReturnSummaryState.MaxItemTextLength)
                            violations.Add($"Pending return summary item exceeds {PendingReturnSummaryState.MaxItemTextLength} characters.");

                    // DELIBERATELY NOT VALIDATED (D-055): that an item's first-commit
                    // timestamp is at or before the summary's own generation timestamp.
                    // That comparison assumes a monotonic clock, and this repository
                    // explicitly does NOT assume one: booting a save with a clock earlier
                    // than the save's own generation is a supported, tested scenario
                    // (ACTIVITY_PIPELINE §12 "the pipeline must not assume device time is
                    // monotonic"; the backward-clock handling in D-053). Adding the check
                    // made three acceptance tests fail closed on a perfectly valid state,
                    // which is the exact "unsound invariant that bricks mature saves" shape
                    // this validator exists to avoid. Any comparison between two persisted
                    // timestamps is unsound without a monotonic clock, so the item currency
                    // is carried for PRESENTATION only and is not a load-time invariant.
                }
                if (pendingSummary.GeneratedAtUtc == default)
                    violations.Add("Pending return summary has no generation timestamp.");
            }

            // Activity ingestion trust invariants.
            foreach (var pair in state.ProcessedRecords.Entries)
            {
                var entry = pair.Value;
                if (entry == null)
                {
                    violations.Add($"Processed-record entry '{pair.Key}' is null.");
                    continue;
                }
                if (!string.Equals(pair.Key, entry.IdentityKey, StringComparison.Ordinal))
                    violations.Add($"Processed-record dictionary key '{pair.Key}' does not match entry identity '{entry.IdentityKey}'.");
                if (entry.ConversionRuleVersion <= 0)
                    violations.Add($"Processed-record entry '{pair.Key}' has non-positive conversion rule version {entry.ConversionRuleVersion}.");
                if (entry.EligibleSteps < 0L || entry.EligibleSteps > ActivityValidationPolicy.MaxStepsPerRecord)
                    violations.Add($"Processed-record entry '{pair.Key}' eligible steps out of bounds: {entry.EligibleSteps}.");
                if (entry.VitalityCredited < 0L)
                    violations.Add($"Processed-record entry '{pair.Key}' credited vitality is negative.");
                if (entry.LastRevision < 1)
                    violations.Add($"Processed-record entry '{pair.Key}' has invalid revision {entry.LastRevision}.");
            }

            if (state.ProcessedRecords.TotalVitalityCredited > state.Ledger.TotalVitalityCredited)
                violations.Add("Processed-record credited vitality exceeds reward ledger total: durable dedup state outruns reward state.");

            foreach (var carry in state.ProcessedRecords.StepCarryByProvider)
            {
                if (string.IsNullOrEmpty(carry.Key) || carry.Value < 0L || carry.Value > StepConversionRuleV1.MaxCarry)
                    violations.Add($"Step carry for provider '{carry.Key}' out of bounds: {carry.Value}.");
            }

            if (state.ProcessedRecords.UnappliedReversalVitality < 0L)
                violations.Add("Unapplied reversal counter is negative.");

            // Days walked (D-103): the maintained day set must be sorted, distinct and cover
            // every retained credited row that carries a local day.
            var days = state.ProcessedRecords.WalkedLocalDays;
            for (int i = 0; i < days.Count; i++)
            {
                if (days[i] < 0 || (i > 0 && days[i] <= days[i - 1]))
                {
                    violations.Add("Days-walked set ProcessedRecords.WalkedLocalDays is not strictly ascending and non-negative.");
                    break;
                }
            }
            if (state.ProcessedRecords.LegacyDaysLowerBound < 0)
                violations.Add("Days-walked legacy lower bound is negative.");

            // Plausible range (D-105): a credited record ends at most 45 days before the profile
            // existed (the new-profile acceptance floor) and never after trusted progress, and its
            // local date is within a day of its UTC date. A tampered count beyond that span, or a day
            // outside it, is rejected instead of being shown to the player as fact.
            DateTimeOffset latest = state.LastAdvancedUtc > state.IngestionCheckpointUtc ? state.LastAdvancedUtc : state.IngestionCheckpointUtc;
            if (state.CreatedAtUtc != default && latest >= state.CreatedAtUtc)
            {
                long firstDay = (long)LocalDayCalendar.UtcDayOf(state.CreatedAtUtc) - 47L;
                long lastDay = (long)LocalDayCalendar.UtcDayOf(latest) + 2L;
                long span = lastDay - firstDay + 1L;
                foreach (var d in days)
                {
                    if (d < firstDay || d > lastDay)
                    {
                        violations.Add($"Days-walked day {d} lies outside the profile's plausible range.");
                        break;
                    }
                }
                if (state.ProcessedRecords.LegacyDaysLowerBound > span || state.ProcessedRecords.DaysWalkedCount > span)
                    violations.Add("Days-walked count exceeds the number of days the profile has existed.");
                if (state.ProcessedRecords.LegacyLastUtcDay is int lastLegacy && (lastLegacy < firstDay || lastLegacy > lastDay))
                    violations.Add("Days-walked legacy boundary lies outside the profile's plausible range.");
            }
            foreach (var pair in state.ProcessedRecords.Entries)
            {
                var e = pair.Value;
                if (e != null && e.LocalDay is int ld && e.EligibleSteps > 0L && days.BinarySearch(ld) < 0)
                {
                    violations.Add($"Processed-record entry '{pair.Key}' local day {ld} is missing from the days-walked set.");
                    break;
                }
            }

            // Maintained lifetime aggregates (D-066) must equal a full recomputation over
            // retained history plus the durably recorded retired total. A mismatch means the
            // O(1) value cannot be trusted, so it is a violation naming the aggregate.
            var processed = state.ProcessedRecords;
            if (processed.MaintainedVitalityCredited.HasValue &&
                processed.MaintainedVitalityCredited.Value != processed.RecomputeLifetimeVitality())
                violations.Add($"Maintained aggregate ProcessedRecords.MaintainedVitalityCredited ({processed.MaintainedVitalityCredited.Value}) does not equal its recomputation ({processed.RecomputeLifetimeVitality()}).");
            if (state.Ledger.MaintainedVitalityCredited.HasValue &&
                state.Ledger.MaintainedVitalityCredited.Value != state.Ledger.RecomputeLifetimeVitality())
                violations.Add($"Maintained aggregate Ledger.MaintainedVitalityCredited ({state.Ledger.MaintainedVitalityCredited.Value}) does not equal its recomputation ({state.Ledger.RecomputeLifetimeVitality()}).");
            if (processed.RetiredCount < 0L || state.Ledger.RetiredCount < 0L)
                violations.Add("Retirement count is negative.");
            if (processed.RetiredCount != processed.RetiredIdentities.Count)
                violations.Add($"Retired-identity set size {processed.RetiredIdentities.Count} does not match retired count {processed.RetiredCount}.");
            foreach (var retired in processed.RetiredIdentities)
            {
                if (processed.Entries.ContainsKey(retired))
                {
                    violations.Add($"Processed-record '{retired}' is both retained and retired.");
                    break;
                }
            }
            if (state.LastIngestionOutcome != null && state.LastIngestionOutcome.RetiredRecordsIgnored < 0)
                violations.Add("Ingestion retired-records-ignored counter is negative.");

            // Durable support diagnostics (D-063): bounded counters must be non-negative and
            // internally consistent. A breakdown that does not sum to the recorded total means
            // the evidence was mutated or truncated, and the one surface whose entire value is
            // being trustworthy has to say so.
            var ingestion = state.LastIngestionOutcome;
            if (ingestion != null)
            {
                if (ingestion.TotalReceived < 0) violations.Add($"Ingestion total received is negative: {ingestion.TotalReceived}.");
                if (ingestion.Accepted < 0) violations.Add($"Ingestion accepted count is negative: {ingestion.Accepted}.");
                if (ingestion.Rejected < 0) violations.Add($"Ingestion rejected count is negative: {ingestion.Rejected}.");
                if (ingestion.DuplicatesIgnored < 0) violations.Add($"Ingestion duplicates-ignored count is negative: {ingestion.DuplicatesIgnored}.");
                if (ingestion.CorrectionsApplied < 0) violations.Add($"Ingestion corrections count is negative: {ingestion.CorrectionsApplied}.");
                if (ingestion.DeletionsApplied < 0) violations.Add($"Ingestion deletions-applied count is negative: {ingestion.DeletionsApplied}.");
                if (ingestion.DeletionsIgnored < 0) violations.Add($"Ingestion deletions-ignored count is negative: {ingestion.DeletionsIgnored}.");
                if (ingestion.StaleRevisionsIgnored < 0) violations.Add($"Ingestion stale-revision count is negative: {ingestion.StaleRevisionsIgnored}.");
                if (ingestion.QuantityClamped < 0) violations.Add($"Ingestion quantity-clamped count is negative: {ingestion.QuantityClamped}.");
                if (ingestion.UnappliedReversalVitality < 0) violations.Add($"Ingestion unapplied reversal vitality is negative: {ingestion.UnappliedReversalVitality}.");

                // DELIBERATELY NOT checked: VitalityCredited is a NET APPLIED delta, not a
                // counter. A correction batch claws value back and is legitimately negative
                // (ACTIVITY_PIPELINE §11), so a non-negativity rule here would fail closed on
                // a valid negative correction — the exact "unsound invariant that bricks
                // mature saves" shape this validator exists to prevent.

                if (ingestion.Accepted > ingestion.TotalReceived)
                    violations.Add(
                        $"Ingestion accepted {ingestion.Accepted} exceeds total received {ingestion.TotalReceived}.");

                if (ingestion.RejectionCountsByReason != null)
                {
                    long breakdownTotal = 0L;
                    foreach (var pair in ingestion.RejectionCountsByReason)
                    {
                        if (pair.Key < 0)
                            violations.Add($"Ingestion rejection breakdown has a negative reason ordinal: {pair.Key}.");
                        if (pair.Value < 0)
                            violations.Add(
                                $"Ingestion rejection breakdown for reason {pair.Key} has a negative count: {pair.Value}.");
                        breakdownTotal += pair.Value;
                    }

                    if (breakdownTotal != ingestion.Rejected)
                        violations.Add(
                            $"Ingestion rejection breakdown sums to {breakdownTotal} but the recorded rejection total is {ingestion.Rejected}.");
                }
            }

            return violations;
        }
    }
}
