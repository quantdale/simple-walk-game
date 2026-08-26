using System;
using System.Collections.Generic;
using WalkGame.Domain.Discoveries;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Expeditions;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Domain.Simulation
{
    /// <summary>
    /// Deterministic offline advancement: producer ticks plus queue allocation.
    /// All time-dependent behavior advances from explicit checkpoints; backward clock
    /// movement is defensively ignored rather than allowed to corrupt state.
    /// </summary>
    public static class OfflineAdvancer
    {
        /// <summary>
        /// Suspicious elapsed intervals are capped so a wildly wrong clock cannot mint
        /// unbounded production in one tick.
        /// </summary>
        public static readonly TimeSpan MaxProducerInterval = TimeSpan.FromDays(3650L);

        public static void Advance(GameState game, RegionDefinition content, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (content == null) throw new ArgumentNullException(nameof(content));

            if (nowUtc < game.LastAdvancedUtc)
            {
                events.Add(new ClockSkewIgnored(nowUtc, game.LastAdvancedUtc - nowUtc));
                AllocateVitality(game, content, nowUtc, events);
                return;
            }

            TickProducers(game, content, nowUtc, events);
            AllocateVitality(game, content, nowUtc, events);
            game.LastAdvancedUtc = nowUtc;
        }

        /// <summary>
        /// Integer-only deterministic production. Output mints into the producer's
        /// bounded pending store (<c>min(capacityRemaining, rate × elapsed)</c>; surplus
        /// time beyond the store creates no waste), then whole units auto-deliver into
        /// canonical resource balances (no manual claiming), clamped by any resource-level
        /// cap; blocked units stay parked in the store and flush on a later tick.
        /// Checkpoints are monotonic: a backward clock never regresses LastTickUtc, so no
        /// callable path can backdate a producer into future overproduction.
        /// </summary>
        public static void TickProducers(GameState game, RegionDefinition content, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            foreach (var runtime in game.Region.Producers)
            {
                var definition = content.FindProducer(runtime.ProducerId);
                if (definition == null || !runtime.Unlocked)
                    continue;

                long elapsedTicks = nowUtc.UtcTicks - runtime.LastTickUtc.UtcTicks;
                if (elapsedTicks < 0L)
                {
                    // Backward clock at this callable boundary: refuse both production and
                    // checkpoint regression so misuse cannot create later overproduction.
                    events.Add(new ClockSkewIgnored(nowUtc, TimeSpan.FromTicks(-elapsedTicks)));
                    continue;
                }

                bool hitStoreCapacity = false;
                if (elapsedTicks > 0L)
                {
                    runtime.LastTickUtc = nowUtc;

                    long cappedTicks = Math.Min(elapsedTicks, MaxProducerInterval.Ticks);
                    long earnedMilliUnits = (cappedTicks / TimeSpan.TicksPerDay) * definition.MilliUnitsPerDay
                                              + (cappedTicks % TimeSpan.TicksPerDay) * definition.MilliUnitsPerDay / TimeSpan.TicksPerDay;

                    long storeCapacityMilliUnits = definition.CapacityUnits * ProducerDefinition.MilliUnitsPerUnit;
                    long roomInStore = storeCapacityMilliUnits - runtime.StoredMilliUnits;
                    if (roomInStore > 0L)
                    {
                        long mintedMilliUnits = Math.Min(earnedMilliUnits, roomInStore);
                        runtime.StoredMilliUnits += mintedMilliUnits;
                        hitStoreCapacity = mintedMilliUnits < earnedMilliUnits;
                    }
                    else if (earnedMilliUnits > 0L)
                    {
                        hitStoreCapacity = true;
                    }
                }

                DeliverStoredOutput(runtime, definition, game.Resources, nowUtc, events, hitStoreCapacity);
            }
        }

        /// <summary>
        /// Moves whole stored units downstream. Delivery is attempted on every tick call
        /// (even zero-elapsed ones) so units parked behind a full resource cap flush as
        /// soon as space frees, without requiring new production.
        /// </summary>
        private static void DeliverStoredOutput(
            ProducerRuntimeState runtime,
            ProducerDefinition definition,
            Economy.ResourceBalances balances,
            DateTimeOffset nowUtc,
            List<SimulationEvent> events,
            bool hitStoreCapacity)
        {
            long deliverableWholeUnits = runtime.StoredMilliUnits / ProducerDefinition.MilliUnitsPerUnit;
            long deliveredMilliUnits = 0L;
            bool hitResourceCap = false;

            if (deliverableWholeUnits > 0L)
            {
                long appliedUnits = balances.Add(definition.Output, deliverableWholeUnits);
                runtime.StoredMilliUnits -= appliedUnits * ProducerDefinition.MilliUnitsPerUnit;
                runtime.TotalProducedMilliUnits += appliedUnits * ProducerDefinition.MilliUnitsPerUnit;
                deliveredMilliUnits = appliedUnits * ProducerDefinition.MilliUnitsPerUnit;
                hitResourceCap = appliedUnits < deliverableWholeUnits;
            }

            if (deliveredMilliUnits > 0L || hitStoreCapacity)
            {
                events.Add(new ProducerProduced(nowUtc, runtime.ProducerId, deliveredMilliUnits,
                    hitStoreCapacity || hitResourceCap));
            }
        }

        /// <summary>
        /// Streams available Vitality into the active project; rolls surplus into the next
        /// queued project when AutoAdvance is enabled. Leftover Vitality stays banked when
        /// the queue is empty, so activity is never wasted because the app was closed.
        /// </summary>
        public static void AllocateVitality(GameState game, RegionDefinition content, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (content == null) throw new ArgumentNullException(nameof(content));

            var queue = game.Queue;
            var balances = game.Resources;
            var region = game.Region;

            while (true)
            {
                EnsureActiveSlot(game, content, nowUtc, events);

                string? activeProjectId = queue.ActiveProjectId;
                if (activeProjectId == null)
                    return;

                long availableVitality = balances.Get(ResourceType.Vitality);
                if (availableVitality <= 0L)
                    return;

                var projectDefinition = content.FindProject(activeProjectId);
                var projectState = region.FindProject(activeProjectId);
                if (projectDefinition == null || projectState == null)
                {
                    queue.ActiveProjectId = null;
                    continue;
                }

                long remaining = projectDefinition.VitalityCost - projectState.VitalityInvested;
                if (remaining <= 0L)
                {
                    CompleteProject(game, content, projectState, nowUtc, events);
                    continue;
                }

                long take = Math.Min(availableVitality, remaining);
                if (!balances.TryConsume(ResourceType.Vitality, take))
                    return;

                projectState.VitalityInvested += take;

                if (projectState.VitalityInvested >= projectDefinition.VitalityCost)
                    CompleteProject(game, content, projectState, nowUtc, events);
            }
        }

        private static void EnsureActiveSlot(GameState game, RegionDefinition content, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            var queue = game.Queue;
            if (queue.ActiveProjectId != null)
            {
                var state = game.Region.FindProject(queue.ActiveProjectId);
                if (state == null || state.Status == ProjectStatus.Completed)
                    queue.ActiveProjectId = null;
                else
                    return;
            }

            if (!queue.AutoAdvance || queue.QueuedProjectIds.Count == 0)
                return;

            string nextProjectId = queue.QueuedProjectIds[0];
            queue.QueuedProjectIds.RemoveAt(0);

            var nextRuntime = game.Region.FindProject(nextProjectId);
            if (nextRuntime == null || nextRuntime.Status != ProjectStatus.Queued)
                return;

            nextRuntime.Status = ProjectStatus.Active;
            queue.ActiveProjectId = nextProjectId;
            events.Add(new ProjectBecameActive(nowUtc, nextProjectId));
        }

        private static void CompleteProject(GameState game, RegionDefinition content, ProjectState projectState, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            var queue = game.Queue;
            projectState.Status = ProjectStatus.Completed;
            projectState.CompletedAtUtc = nowUtc;
            if (queue.ActiveProjectId == projectState.ProjectId)
                queue.ActiveProjectId = null;

            events.Add(new ProjectCompleted(nowUtc, projectState.ProjectId));
            ApplyCompletionEffects(game, content, projectState.ProjectId, nowUtc, events);
        }

        private static void ApplyCompletionEffects(GameState game, RegionDefinition content, string completedProjectId, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            var region = game.Region;

            foreach (var landmark in content.Landmarks)
            {
                var currentStage = region.LandmarkStages.TryGetValue(landmark.Id.Value, out var stage)
                    ? stage
                    : RestorationStage.Ruined;

                foreach (var stageDefinition in landmark.Stages)
                {
                    if (stageDefinition.UnlockedByProjectId != completedProjectId)
                        continue;
                    if (stageDefinition.Stage <= currentStage)
                        continue;

                    region.LandmarkStages[landmark.Id.Value] = stageDefinition.Stage;
                    events.Add(new LandmarkStageReached(nowUtc, landmark.Id.Value, stageDefinition.Stage));
                    currentStage = stageDefinition.Stage;
                }
            }

            foreach (var runtime in region.Producers)
            {
                var definition = content.FindProducer(runtime.ProducerId);
                if (definition != null && !runtime.Unlocked && definition.UnlockedByProjectId == completedProjectId)
                {
                    runtime.Unlocked = true;
                    runtime.LastTickUtc = nowUtc;
                    events.Add(new ProducerUnlocked(nowUtc, runtime.ProducerId));
                }
            }

            AdvanceProgressionArcs(region, content, completedProjectId, nowUtc, events);
            EvaluateDiscoveries(region, content, completedProjectId, nowUtc, events);
            EvaluateExpeditions(game, content, nowUtc, events);
            EvaluateRegionCompletion(game, content, nowUtc, events);

            UpdateAvailability(game, content, nowUtc, events);
        }

        /// <summary>
        /// Advances both region-level arcs through every stage unlocked by this project.
        /// Stages are discrete, strictly monotonic and idempotent — reprocessing the same
        /// completion can never regress or duplicate a stage.
        /// </summary>
        private static void AdvanceProgressionArcs(RegionState region, RegionDefinition content, string completedProjectId, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            region.EcologyStage = AdvanceArc(content.EcologyProgression, region.EcologyStage, completedProjectId, nowUtc, RegionProgressionAxis.Ecology, events);
            region.SettlementStage = AdvanceArc(content.SettlementProgression, region.SettlementStage, completedProjectId, nowUtc, RegionProgressionAxis.Settlement, events);
        }

        private static int AdvanceArc(
            RegionProgressionDefinition arc,
            int currentStage,
            string completedProjectId,
            DateTimeOffset nowUtc,
            RegionProgressionAxis axis,
            List<SimulationEvent> events)
        {
            int stage = currentStage;
            foreach (var stageDefinition in arc.Stages)
            {
                if (stageDefinition.Stage <= stage)
                    continue;
                if (stageDefinition.UnlockedByProjectId != completedProjectId)
                    continue;

                stage = stageDefinition.Stage;
                events.Add(new RegionProgressionAdvanced(nowUtc, axis, stage));
            }
            return stage;
        }

        /// <summary>Unlocks discoveries triggered by this project, at most once each.</summary>
        private static void EvaluateDiscoveries(RegionState region, RegionDefinition content, string completedProjectId, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            foreach (var definition in content.Discoveries)
            {
                if (definition.UnlockedByProjectId != completedProjectId)
                    continue;
                if (region.Discoveries.ContainsKey(definition.Id.Value))
                    continue;

                region.Discoveries[definition.Id.Value] = new DiscoveryRuntimeState
                {
                    DiscoveryId = definition.Id.Value,
                    DiscoveredAtUtc = nowUtc,
                    Reviewed = false,
                    ReviewedAtUtc = null,
                };
                events.Add(new DiscoveryUnlocked(nowUtc, definition.Id.Value));
            }
        }

        /// <summary>
        /// Deterministic expedition hooks: routes become available when their required
        /// projects are all completed, then complete when their required landmark stages
        /// are all reached. Each transition fires at most once; rewards apply cap-clamped
        /// in the same state transition as the completion timestamp.
        /// </summary>
        private static void EvaluateExpeditions(GameState game, RegionDefinition content, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            var expeditions = game.Region.Expeditions;
            foreach (var definition in content.Expeditions)
            {
                if (!expeditions.TryGetValue(definition.Id.Value, out var runtime))
                {
                    if (!AreProjectsCompleted(game, definition))
                        continue;

                    runtime = new ExpeditionRuntimeState
                    {
                        ExpeditionId = definition.Id.Value,
                        AvailableAtUtc = nowUtc,
                        CompletedAtUtc = null,
                    };
                    expeditions[definition.Id.Value] = runtime;
                    events.Add(new ExpeditionAvailable(nowUtc, definition.Id.Value));
                }

                if (runtime.CompletedAtUtc != null)
                    continue;
                if (!AreStagesReached(game, definition))
                    continue;

                ResourceType? rewardType = null;
                long grantedUnits = 0L;
                if (definition.Reward != null)
                {
                    rewardType = definition.Reward.Type;
                    grantedUnits = game.Resources.Add(definition.Reward.Type, definition.Reward.Units);
                }

                runtime.CompletedAtUtc = nowUtc;
                events.Add(new ExpeditionCompleted(nowUtc, definition.Id.Value, rewardType, grantedUnits));
            }
        }

        private static bool AreProjectsCompleted(GameState game, ExpeditionDefinition definition)
        {
            foreach (var projectId in definition.RequiredProjectIds)
            {
                var state = game.Region.FindProject(projectId);
                if (state == null || state.Status != ProjectStatus.Completed)
                    return false;
            }
            return true;
        }

        private static bool AreStagesReached(GameState game, ExpeditionDefinition definition)
        {
            foreach (var requirement in definition.RequiredStages)
            {
                var reached = game.Region.LandmarkStages.TryGetValue(requirement.LandmarkId, out var stage)
                    ? stage
                    : RestorationStage.Ruined;
                if (reached < requirement.Stage)
                    return false;
            }
            return true;
        }

        /// <summary>Detects the closure milestone exactly once; post-completion state never resets.</summary>
        private static void EvaluateRegionCompletion(GameState game, RegionDefinition content, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            string? milestoneId = content.CompletionMilestoneProjectId;
            if (string.IsNullOrEmpty(milestoneId) || game.Region.IsCompleted)
                return;

            var milestone = game.Region.FindProject(milestoneId);
            if (milestone == null || milestone.Status != ProjectStatus.Completed)
                return;

            game.Region.IsCompleted = true;
            game.Region.RegionCompletedAtUtc = nowUtc;
            events.Add(new RegionCompleted(nowUtc, milestoneId));
        }

        /// <summary>Promotes Locked projects whose prerequisites are all Completed.</summary>
        public static void UpdateAvailability(GameState game, RegionDefinition content, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            foreach (var definition in content.Projects)
            {
                var state = game.Region.FindProject(definition.Id.Value);
                if (state == null || state.Status != ProjectStatus.Locked)
                    continue;

                bool satisfied = true;
                foreach (var prerequisite in definition.Prerequisites)
                {
                    var prerequisiteState = game.Region.FindProject(prerequisite.Value);
                    if (prerequisiteState == null || prerequisiteState.Status != ProjectStatus.Completed)
                    {
                        satisfied = false;
                        break;
                    }
                }

                if (!satisfied)
                    continue;

                state.Status = ProjectStatus.Available;
                events.Add(new ProjectBecameAvailable(nowUtc, definition.Id.Value));
            }
        }
    }
}
