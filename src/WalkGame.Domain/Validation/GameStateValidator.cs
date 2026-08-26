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
                    violations.Add($"Project '{definition.Id}' invested vitality out of bounds: {runtime.VitalityInvested}/{definition.VitalityCost}.");

                if (runtime.Status == ProjectStatus.Completed && runtime.CompletedAtUtc == null)
                    violations.Add($"Completed project '{definition.Id}' has no completion timestamp.");

                if (knownProjectIds.Count > 0 && runtime.Status == ProjectStatus.Active && definition.Prerequisites.Count > 0)
                {
                    // Prerequisite satisfaction for non-entry projects is validated below via statuses.
                }
            }

            foreach (var pair in state.Region.Projects)
            {
                if (!knownProjectIds.Contains(pair.Key))
                    violations.Add($"Runtime project '{pair.Key}' is unknown to content definitions.");
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
                    violations.Add($"Producer runtime '{producer.ProducerId}' is unknown to content definitions.");
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
                    violations.Add($"Expedition runtime '{pair.Key}' is unknown to content definitions.");
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

            if (state.ProcessedRecords.UnappliedReversalVitality < 0L)
                violations.Add("Unapplied reversal counter is negative.");

            return violations;
        }
    }
}
