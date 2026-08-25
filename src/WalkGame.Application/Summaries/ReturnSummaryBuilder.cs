using System;
using System.Collections.Generic;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.Summaries
{
    /// <summary>
    /// Builds the concise return summary from committed simulation events.
    /// Priority order per docs/GAME_SYSTEMS.md §11:
    /// major transformation → actionable decision → noteworthy discovery → aggregates.
    /// Never presents every tick; never gates progression on being read.
    /// </summary>
    public static class ReturnSummaryBuilder
    {
        public static List<string> Build(IEnumerable<Domain.Simulation.SimulationEvent> events, RegionDefinition content)
        {
            var lines = new List<string>();
            if (events == null)
                return lines;

            long vitalityCredited = 0L;
            int duplicates = 0;
            int completions = 0;
            int activations = 0;
            int availability = 0;
            long producerMilliTotal = 0L;
            string? cappedProducerId = null;
            bool clockSkew = false;

            foreach (var evt in events)
            {
                switch (evt)
                {
                    case Domain.Simulation.ProjectCompleted completed:
                        completions++;
                        lines.Insert(0, Title(content.FindProject(completed.ProjectId)?.TitleKey, completed.ProjectId) + " is complete.");
                        break;
                    case Domain.Simulation.LandmarkStageReached stage:
                        lines.Add(Title(content.FindLandmark(stage.LandmarkId)?.TitleKey, stage.LandmarkId) + $" has reached {stage.Stage}.");
                        break;
                    case Domain.Simulation.ProducerUnlocked producerUnlocked:
                        lines.Add(Title(content.FindProducer(producerUnlocked.ProducerId)?.TitleKey, producerUnlocked.ProducerId) + " is now operating.");
                        break;
                    case Domain.Simulation.ProjectBecameAvailable available:
                        availability++;
                        if (availability <= 3)
                            lines.Add(Title(content.FindProject(available.ProjectId)?.TitleKey, available.ProjectId) + " is ready to queue.");
                        break;
                    case Domain.Simulation.ProjectBecameActive active:
                        activations++;
                        if (activations == 1)
                            lines.Add("Work continues on " + Title(content.FindProject(active.ProjectId)?.TitleKey, active.ProjectId) + ".");
                        break;
                    case Domain.Simulation.ActivityCredited credited:
                        vitalityCredited += credited.VitalityApplied;
                        break;
                    case Domain.Simulation.ActivityDuplicate:
                        duplicates++;
                        break;
                    case Domain.Simulation.ProducerProduced produced:
                        producerMilliTotal += produced.MilliUnitsGained;
                        if (produced.HitCapacity)
                            cappedProducerId = produced.ProducerId;
                        break;
                    case Domain.Simulation.ClockSkewIgnored:
                        clockSkew = true;
                        break;
                }
            }

            if (availability > 3)
                lines.Add($"{availability - 3} more projects became available.");
            if (vitalityCredited > 0L || completions > 0 || activations > 0)
                lines.Add($"Vitality credited: {vitalityCredited:N0}.");
            if (producerMilliTotal >= ProducerDefinition.MilliUnitsPerUnit)
                lines.Add($"Producers delivered {producerMilliTotal / ProducerDefinition.MilliUnitsPerUnit:N0} units.");
            if (cappedProducerId != null)
                lines.Add("A producer storage cap was hit; surplus time did not create waste.");
            if (duplicates > 0)
                lines.Add($"{duplicates} duplicate activity report(s) were safely ignored.");
            if (clockSkew)
                lines.Add("A backwards device-clock change was ignored to protect your progress.");

            return lines;
        }

        private static string Title(string? titleKey, string fallbackId) =>
            string.IsNullOrWhiteSpace(titleKey) ? fallbackId : titleKey;
    }
}
