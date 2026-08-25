using System;
using System.Collections.Generic;
using WalkGame.Domain.Economy;
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
        /// Integer-only deterministic production. Output flows directly into canonical
        /// resource balances (no manual claiming), with a milli-unit carry preserving the
        /// sub-unit remainder so repeated short ticks sum identically to one long tick.
        /// Resource-level caps are enforced by <see cref="ResourceBalances"/>.
        /// </summary>
        public static void TickProducers(GameState game, RegionDefinition content, DateTimeOffset nowUtc, List<SimulationEvent> events)
        {
            foreach (var runtime in game.Region.Producers)
            {
                var definition = content.FindProducer(runtime.ProducerId);
                if (definition == null || !runtime.Unlocked)
                    continue;

                long elapsedTicks = nowUtc.UtcTicks - runtime.LastTickUtc.UtcTicks;
                runtime.LastTickUtc = nowUtc;
                if (elapsedTicks <= 0L)
                    continue;

                long cappedTicks = Math.Min(elapsedTicks, MaxProducerInterval.Ticks);
                long producedMilliUnits = (cappedTicks / TimeSpan.TicksPerDay) * definition.MilliUnitsPerDay
                                          + (cappedTicks % TimeSpan.TicksPerDay) * definition.MilliUnitsPerDay / TimeSpan.TicksPerDay;

                long totalMilliUnits = runtime.CarryMilliUnits + producedMilliUnits;
                long wholeUnits = totalMilliUnits / ProducerDefinition.MilliUnitsPerUnit;
                runtime.CarryMilliUnits = totalMilliUnits % ProducerDefinition.MilliUnitsPerUnit;
                if (wholeUnits <= 0L)
                    continue;

                long appliedUnits = game.Resources.Add(definition.Output, wholeUnits);
                runtime.TotalProducedMilliUnits += appliedUnits * ProducerDefinition.MilliUnitsPerUnit;
                bool hitCapacity = appliedUnits < wholeUnits;
                events.Add(new ProducerProduced(nowUtc, runtime.ProducerId, appliedUnits * ProducerDefinition.MilliUnitsPerUnit, hitCapacity));
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

            UpdateAvailability(game, content, nowUtc, events);
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
