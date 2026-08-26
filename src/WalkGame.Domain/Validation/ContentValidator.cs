using System;
using System.Collections.Generic;
using WalkGame.Domain.Expeditions;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Domain.Validation
{
    /// <summary>
    /// Static validation for authored content. Invalid content must fail validation before
    /// any runtime exists — never appear as silent runtime corruption. This is a release
    /// gate for the M4 Region 1 contract (campaign workstream E): reference integrity,
    /// cycle/deadlock freedom, reachability of every piece of content and of the closure
    /// milestone, arc monotonicity, discovery/expedition integrity, and overflow safety.
    ///
    /// Validation is order-independent: every ID set is collected before any reference is
    /// resolved, so valid forward references are never falsely rejected.
    /// </summary>
    public static class ContentValidator
    {
        /// <summary>Sanity bound keeping summed chain costs far away from overflow.</summary>
        private const long MaxTotalVitalityCost = 1_000_000_000L;

        /// <summary>Sane upper bound on authored progression stages per axis.</summary>
        private const int MaxStagesPerArc = 10;

        /// <summary>Producer rates multiply by up to MaxProducerInterval days; keep products representable.</summary>
        private const long MaxMilliUnitsPerDay = 1_000_000_000_000L;

        public static List<string> Validate(RegionDefinition? content)
        {
            var violations = new List<string>();
            if (content == null)
            {
                violations.Add("Region definition is null.");
                return violations;
            }

            // ---- Pass 1: collect every ID set before resolving any reference. ----
            var projectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in content.Projects)
                projectIds.Add(project.Id.Value);

            var landmarkIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var landmark in content.Landmarks)
                landmarkIds.Add(landmark.Id.Value);

            var discoveryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var discovery in content.Discoveries)
                discoveryIds.Add(discovery.Id.Value);

            var expeditionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var expedition in content.Expeditions)
                expeditionIds.Add(expedition.Id.Value);

            // ---- Projects: identity, cost sanity, forward-safe references. ----
            var seenProjects = new HashSet<string>(StringComparer.Ordinal);
            long totalCost = 0L;
            foreach (var project in content.Projects)
            {
                if (!seenProjects.Add(project.Id.Value))
                    violations.Add($"Duplicate project ID '{project.Id}'.");
                if (project.VitalityCost <= 0L)
                    violations.Add($"Project '{project.Id}' has non-positive cost.");
                totalCost = checked(totalCost + project.VitalityCost);
                if (totalCost > MaxTotalVitalityCost)
                    violations.Add($"Combined project costs exceed the representable budget at '{project.Id}'.");
                foreach (var prerequisite in project.Prerequisites)
                    if (!projectIds.Contains(prerequisite.Value))
                        violations.Add($"Project '{project.Id}' references missing prerequisite '{prerequisite}'.");
            }

            // ---- Landmarks: unique stages, ascending monotonic triggers. ----
            var seenLandmarks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var landmark in content.Landmarks)
            {
                if (!seenLandmarks.Add(landmark.Id.Value))
                    violations.Add($"Duplicate landmark ID '{landmark.Id}'.");

                RestorationStage previous = RestorationStage.Ruined;
                foreach (var stage in landmark.Stages)
                {
                    if (!projectIds.Contains(stage.UnlockedByProjectId))
                        violations.Add($"Landmark '{landmark.Id}' stage {stage.Stage} references missing project '{stage.UnlockedByProjectId}'.");
                    if (stage.Stage <= previous && previous != RestorationStage.Ruined)
                        violations.Add($"Landmark '{landmark.Id}' stages are not strictly ascending.");
                    else if (stage.Stage == RestorationStage.Ruined)
                        violations.Add($"Landmark '{landmark.Id}' defines a Ruined stage; Ruined is the implicit initial state.");
                    previous = stage.Stage;
                }
            }

            // ---- Producers: unique unlockers, representable rate/capacity. ----
            var producerUnlockProjects = new HashSet<string>(StringComparer.Ordinal);
            var seenProducers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var producer in content.Producers)
            {
                if (!seenProducers.Add(producer.Id.Value))
                    violations.Add($"Duplicate producer ID '{producer.Id}'.");
                if (!projectIds.Contains(producer.UnlockedByProjectId))
                    violations.Add($"Producer '{producer.Id}' references missing unlock project '{producer.UnlockedByProjectId}'.");
                if (producer.MilliUnitsPerDay > MaxMilliUnitsPerDay)
                    violations.Add($"Producer '{producer.Id}' rate {producer.MilliUnitsPerDay} exceeds the safe representable maximum.");
                if (producer.CapacityUnits > long.MaxValue / ProducerDefinition.MilliUnitsPerUnit)
                    violations.Add($"Producer '{producer.Id}' capacity is too large to represent safely.");
                if (producer.CapacityUnits <= 0L)
                    violations.Add($"Producer '{producer.Id}' capacity must be positive.");
                producerUnlockProjects.Add(producer.UnlockedByProjectId);
            }

            // ---- Discoveries: key integrity and trigger resolution. ----
            var seenDiscoveries = new HashSet<string>(StringComparer.Ordinal);
            foreach (var discovery in content.Discoveries)
            {
                if (!seenDiscoveries.Add(discovery.Id.Value))
                    violations.Add($"Duplicate discovery ID '{discovery.Id}'.");
                if (!projectIds.Contains(discovery.UnlockedByProjectId))
                    violations.Add($"Discovery '{discovery.Id}' references missing unlock project '{discovery.UnlockedByProjectId}'.");
            }

            // ---- Expeditions: requirement/reward integrity. ----
            var seenExpeditions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var expedition in content.Expeditions)
            {
                if (!seenExpeditions.Add(expedition.Id.Value))
                    violations.Add($"Duplicate expedition ID '{expedition.Id}'.");
                foreach (var projectId in expedition.RequiredProjectIds)
                    if (!projectIds.Contains(projectId))
                        violations.Add($"Expedition '{expedition.Id}' requires missing project '{projectId}'.");
                foreach (var requirement in expedition.RequiredStages)
                {
                    var landmark = content.FindLandmark(requirement.LandmarkId);
                    if (landmark == null)
                    {
                        violations.Add($"Expedition '{expedition.Id}' requires unknown landmark '{requirement.LandmarkId}'.");
                        continue;
                    }
                    bool stageDefined = false;
                    foreach (var stage in landmark.Stages)
                        if (stage.Stage == requirement.Stage)
                            stageDefined = true;
                    if (requirement.Stage != RestorationStage.Ruined && !stageDefined)
                        violations.Add($"Expedition '{expedition.Id}' requires landmark '{requirement.LandmarkId}' at stage {requirement.Stage}, which its content never reaches.");
                }
                if (expedition.Reward != null && expedition.Reward.Units > MaxTotalVitalityCost)
                    violations.Add($"Expedition '{expedition.Id}' reward is unrepresentably large.");
            }

            // ---- Progression arcs: strictly ascending, resolvable, bounded. ----
            ValidateArc(content.EcologyProgression, projectIds, "ecology", MaxStagesPerArc, violations);
            ValidateArc(content.SettlementProgression, projectIds, "settlement", MaxStagesPerArc, violations);

            // ---- Reachability: entry path, no hidden deadlock, closure reachable. ----
            bool hasReachableEntry = false;
            foreach (var project in content.Projects)
            {
                if (project.Prerequisites.Count != 0)
                    continue;
                hasReachableEntry = true;
                break;
            }
            if (!hasReachableEntry)
                violations.Add("Region has no entry project (all projects have prerequisites).");

            var cycleRoot = FindAnyCycle(content);
            if (cycleRoot != null)
                violations.Add($"Project prerequisite chain contains a cycle at '{cycleRoot}'.");

            if (hasReachableEntry)
            {
                var reachable = ComputeReachable(content);
                if (content.CompletionMilestoneProjectId != null &&
                    !reachable.Contains(content.CompletionMilestoneProjectId))
                {
                    violations.Add($"Completion milestone '{content.CompletionMilestoneProjectId}' is not reachable from an entry project.");
                }
                if (content.CompletionMilestoneProjectId != null &&
                    !projectIds.Contains(content.CompletionMilestoneProjectId))
                {
                    violations.Add($"Completion milestone '{content.CompletionMilestoneProjectId}' is not a defined project.");
                }

                foreach (var project in content.Projects)
                    if (!reachable.Contains(project.Id.Value))
                        violations.Add($"Project '{project.Id}' is unreachable from any entry project (hidden deadlock).");
                foreach (var landmark in content.Landmarks)
                    foreach (var stage in landmark.Stages)
                        if (!reachable.Contains(stage.UnlockedByProjectId))
                            violations.Add($"Landmark '{landmark.Id}' depends on unreachable project '{stage.UnlockedByProjectId}'.");
                foreach (var producerId in producerUnlockProjects)
                    if (!reachable.Contains(producerId))
                        violations.Add($"Producer unlock depends on unreachable project '{producerId}'.");
                foreach (var discovery in content.Discoveries)
                    if (!reachable.Contains(discovery.UnlockedByProjectId))
                        violations.Add($"Discovery '{discovery.Id}' depends on unreachable project '{discovery.UnlockedByProjectId}'.");
                foreach (var expedition in content.Expeditions)
                    foreach (var projectId in expedition.RequiredProjectIds)
                        if (!reachable.Contains(projectId))
                            violations.Add($"Expedition '{expedition.Id}' depends on unreachable project '{projectId}'.");
            }

            return violations;
        }

        private static void ValidateArc(
            RegionProgressionDefinition arc,
            HashSet<string> projectIds,
            string axisName,
            int maxStages,
            List<string> violations)
        {
            int previousStage = 0;
            foreach (var stage in arc.Stages)
            {
                if (stage.Stage <= previousStage)
                    violations.Add($"{axisName} progression stages are not strictly ascending at stage {stage.Stage}.");
                if (!projectIds.Contains(stage.UnlockedByProjectId))
                    violations.Add($"{axisName} progression stage {stage.Stage} references missing project '{stage.UnlockedByProjectId}'.");
                previousStage = stage.Stage;
            }
            if (arc.Stages.Count > maxStages)
                violations.Add($"{axisName} progression arc exceeds the stage bound ({arc.Stages.Count}).");
        }

        /// <summary>
        /// Fixpoint reachability over AND-prerequisites: a project becomes reachable when
        /// every one of its prerequisites is reachable (entry projects have none).
        /// </summary>
        private static HashSet<string> ComputeReachable(RegionDefinition content)
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var project in content.Projects)
                {
                    if (reachable.Contains(project.Id.Value))
                        continue;
                    bool satisfied = true;
                    foreach (var prerequisite in project.Prerequisites)
                        if (!reachable.Contains(prerequisite.Value))
                            satisfied = false;
                    if (!satisfied)
                        continue;
                    reachable.Add(project.Id.Value);
                    changed = true;
                }
            }
            return reachable;
        }

        private static string? FindAnyCycle(Regions.RegionDefinition content)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack = new HashSet<string>(StringComparer.Ordinal);

            bool Visit(string projectId)
            {
                if (visited.Contains(projectId))
                    return false;
                if (!stack.Add(projectId))
                    return true;

                var definition = content.FindProject(projectId);
                if (definition != null)
                {
                    foreach (var prerequisite in definition.Prerequisites)
                        if (Visit(prerequisite.Value))
                            return true;
                }

                stack.Remove(projectId);
                visited.Add(projectId);
                return false;
            }

            foreach (var project in content.Projects)
                if (Visit(project.Id.Value))
                    return project.Id.Value;
            return null;
        }
    }
}
