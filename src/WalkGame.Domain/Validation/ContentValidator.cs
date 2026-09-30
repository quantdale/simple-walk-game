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

        /// <summary>
        /// Every resource a producer can emit must have an explicit, positive, representable
        /// authored cap. A missing cap used to be a silent runtime default of
        /// <see cref="long.MaxValue"/>, which is how the cap/parking machinery in
        /// <c>OfflineAdvancer</c> became unreachable in production (D-054).
        /// </summary>
        private static void ValidateResourceCaps(RegionDefinition content, List<string> violations)
        {
            foreach (var cap in content.ResourceCaps)
            {
                if (cap.Value <= 0L)
                    violations.Add($"Resource cap for {cap.Key} must be positive, but is {cap.Value}.");

                // The producer store capacity a cap implies must stay representable, and
                // the cap must be able to hold at least one whole unit of output.
                if (cap.Value > long.MaxValue / ProducerDefinition.MilliUnitsPerUnit)
                    violations.Add($"Resource cap for {cap.Key} is too large to represent safely.");
            }

            foreach (var producer in content.Producers)
            {
                if (!content.ResourceCaps.ContainsKey(producer.Output))
                {
                    violations.Add(
                        $"Producer '{producer.Id}' emits {producer.Output} but the region authors no cap for it; " +
                        "every produced resource must declare an explicit cap.");
                }
            }
        }

        /// <summary>
        /// A secondary-resource requirement that the region's own producers can never
        /// supply within the cap is a content authoring failure: it would make a project
        /// permanently unactivatable, and if that project is on the path to the closure
        /// milestone the region becomes uncompletable. Both rules name project and resource.
        /// </summary>
        private static void ValidateSecondaryRequirements(RegionDefinition content, List<string> violations)
        {
            // Head-room available for a resource across the design horizon: the authored
            // cap minus what the region's own producers of that resource can deliver in
            // MaxRequirementHorizonDays. Expedition grants are not counted here because an
            // expedition is a bonus, not a guaranteed source; a requirement that only an
            // expedition can satisfy is still satisfiable, so counting producers alone is
            // the conservative check.
            const long MaxRequirementHorizonDays = 400L;

            foreach (var project in content.Projects)
            {
                foreach (var requirement in project.ResourceCosts)
                {
                    var resource = requirement.Key;
                    var cost = requirement.Value;

                    long cap = content.ResourceCap(resource) ?? long.MaxValue;
                    if (cost > cap)
                    {
                        violations.Add(
                            $"Project '{project.Id}' requires {cost} {resource}, which exceeds the region's " +
                            $"authored cap of {cap} {resource}; the project could never be activated.");
                        continue;
                    }

                    if (resource == Economy.ResourceType.Vitality)
                        continue; // Vitality is unbounded by design: it is the activity sink.

                    long producedUnits = 0L;
                    bool anyProducer = false;
                    foreach (var producer in content.Producers)
                    {
                        if (producer.Output != resource)
                            continue;
                        anyProducer = true;
                        // What the region can actually DELIVER inside the horizon is its
                        // production rate, not its store size: parked units flush
                        // repeatedly over time, so total deliverable is bounded by
                        // (whole units/day × horizon), not by capacity. Integer division
                        // matches the simulation's own whole-unit delivery, so the check
                        // cannot disagree with what a tick will really produce.
                        long unitsPerDay = producer.MilliUnitsPerDay / ProducerDefinition.MilliUnitsPerUnit;
                        long perHorizon = unitsPerDay > long.MaxValue / MaxRequirementHorizonDays
                            ? long.MaxValue
                            : unitsPerDay * MaxRequirementHorizonDays;
                        producedUnits = producedUnits > long.MaxValue - perHorizon
                            ? long.MaxValue
                            : producedUnits + perHorizon;
                    }

                    if (!anyProducer)
                    {
                        violations.Add(
                            $"Project '{project.Id}' requires {cost} {resource} but the region authors no producer for {resource}.");
                        continue;
                    }

                    if (producedUnits < cost)
                    {
                        violations.Add(
                            $"Project '{project.Id}' requires {cost} {resource} but the region's producers can " +
                            $"supply only {producedUnits} {resource} within the {MaxRequirementHorizonDays}-day design horizon.");
                    }
                }
            }
        }

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

            // ---- Resource caps and secondary requirements (D-054). ----
            ValidateResourceCaps(content, violations);
            ValidateSecondaryRequirements(content, violations);

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
