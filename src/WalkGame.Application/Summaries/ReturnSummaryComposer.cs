using System;
using System.Collections.Generic;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Summaries;

namespace WalkGame.Application.Summaries
{
    /// <summary>
    /// Composes committed simulation events into the durable, bounded return summary
    /// (GAME_SYSTEMS §11 / UX_DESIGN §4). Deterministic priority: transformation →
    /// actionable decision → production/notice → concise aggregates. Repeated or replayed
    /// events dedupe by text; output is hard-bounded to
    /// <see cref="PendingReturnSummaryState.MaxItems"/> so a 5–15 second glance always
    /// suffices. Composing never alters earned progression.
    /// </summary>
    public static class ReturnSummaryComposer
    {
        private const int NamedAvailableProjectLimit = 3;

        public static PendingReturnSummaryState Compose(
            IEnumerable<SimulationEvent> events,
            RegionDefinition content,
            PendingReturnSummaryState? existing,
            DateTimeOffset generatedAtUtc)
        {
            var merged = new List<PendingSummaryItemState>();
            if (existing != null)
                merged.AddRange(existing.Items);

            AppendEventItems(events, content, merged);
            return Finalize(merged, generatedAtUtc);
        }

        /// <summary>Appends an operational notice (backup recovery, save warnings) to pending state.</summary>
        public static PendingReturnSummaryState WithNotice(
            PendingReturnSummaryState state, DateTimeOffset generatedAtUtc, string noticeText)
        {
            var merged = new List<PendingSummaryItemState>();
            if (state != null)
                merged.AddRange(state.Items);
            merged.Add(new PendingSummaryItemState { Kind = SummaryItemKind.Notice, Text = noticeText });
            return Finalize(merged, generatedAtUtc);
        }

        private static void AppendEventItems(IEnumerable<SimulationEvent>? events, RegionDefinition content, List<PendingSummaryItemState> merged)
        {
            if (events == null)
                return;

            int completions = 0;
            int duplicates = 0;
            long vitalityCredited = 0L;
            long producerMilliDelivered = 0L;
            string? cappedProducerId = null;
            bool clockSkew = false;
            int availableNamed = 0;
            int availableExtra = 0;
            var unlockedProducers = new List<string>();

            foreach (var evt in events)
            {
                switch (evt)
                {
                    case ProjectCompleted completed:
                        completions++;
                        AddDeduped(merged, SummaryItemKind.Transformation,
                            Title(content.FindProject(completed.ProjectId)?.TitleKey, completed.ProjectId) + " is complete.");
                        break;

                    case LandmarkStageReached stage:
                        AddDeduped(merged, SummaryItemKind.Transformation,
                            Title(content.FindLandmark(stage.LandmarkId)?.TitleKey, stage.LandmarkId) + $" has reached {stage.Stage}.");
                        break;

                    case ProducerUnlocked producerUnlocked:
                        if (!unlockedProducers.Contains(producerUnlocked.ProducerId))
                            unlockedProducers.Add(producerUnlocked.ProducerId);
                        break;

                    case ProjectBecameAvailable available:
                        if (availableNamed < NamedAvailableProjectLimit)
                        {
                            availableNamed++;
                            AddDeduped(merged, SummaryItemKind.ActionableDecision,
                                Title(content.FindProject(available.ProjectId)?.TitleKey, available.ProjectId) + " is ready to queue.");
                        }
                        else
                        {
                            availableExtra++;
                        }
                        break;

                    case ProducerProduced produced:
                        producerMilliDelivered += produced.MilliUnitsGained;
                        if (produced.HitCapacity)
                            cappedProducerId = produced.ProducerId;
                        break;

                    case ActivityCredited credited:
                        vitalityCredited += credited.VitalityApplied;
                        break;

                    case ActivityCorrected corrected:
                        vitalityCredited += corrected.VitalityApplied;
                        break;

                    case ActivityDuplicate:
                        duplicates++;
                        break;

                    case ClockSkewIgnored:
                        clockSkew = true;
                        break;
                }
            }

            foreach (var producerId in unlockedProducers)
                AddDeduped(merged, SummaryItemKind.Production,
                    Title(content.FindProducer(producerId)?.TitleKey, producerId) + " is now operating.");

            if (availableExtra > 0)
                AddDeduped(merged, SummaryItemKind.ActionableDecision, $"{availableExtra} more projects became ready to queue.");

            if (producerMilliDelivered >= ProducerDefinition.MilliUnitsPerUnit)
                AddDeduped(merged, SummaryItemKind.Production,
                    $"Producers delivered {producerMilliDelivered / ProducerDefinition.MilliUnitsPerUnit:N0} units.");

            if (cappedProducerId != null)
                AddDeduped(merged, SummaryItemKind.Production,
                    Title(content.FindProducer(cappedProducerId)?.TitleKey, cappedProducerId)
                    + " storage filled; surplus time created no waste.");

            if (completions > 1)
                AddDeduped(merged, SummaryItemKind.Transformation, $"{completions} restoration projects are complete.");

            if (vitalityCredited != 0L)
                AddDeduped(merged, SummaryItemKind.Aggregate, $"Vitality credited: {vitalityCredited:N0}.");

            if (duplicates > 0)
                AddDeduped(merged, SummaryItemKind.Aggregate, $"{duplicates} duplicate activity report(s) were safely ignored.");

            if (clockSkew)
                AddDeduped(merged, SummaryItemKind.Notice, "A backwards device-clock change was ignored to protect your progress.");
        }

        private static PendingReturnSummaryState Finalize(List<PendingSummaryItemState> merged, DateTimeOffset generatedAtUtc)
        {
            // Stable priority sort: kind order, then insertion order within a kind.
            var ordered = new List<PendingSummaryItemState>(merged);
            ordered.Sort((a, b) => ((int)a.Kind).CompareTo((int)b.Kind));

            var state = new PendingReturnSummaryState { GeneratedAtUtc = generatedAtUtc };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in ordered)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Text))
                    continue;
                if (!seen.Add(item.Kind + "|" + item.Text))
                    continue;
                state.Items.Add(new PendingSummaryItemState { Kind = item.Kind, Text = item.Text });
                if (state.Items.Count >= PendingReturnSummaryState.MaxItems)
                    break;
            }

            state.PrimaryNextAction = DerivePrimaryNextAction(state.Items);
            return state;
        }

        /// <summary>
        /// One primary next action: queue/choose restoration work when something changed;
        /// otherwise null — an explicit "nothing needs attention" state.
        /// </summary>
        private static string? DerivePrimaryNextAction(List<PendingSummaryItemState> items)
        {
            foreach (var item in items)
                if (item.Kind == SummaryItemKind.ActionableDecision)
                    return "Queue the next restoration project.";
            return null;
        }

        private static void AddDeduped(List<PendingSummaryItemState> target, SummaryItemKind kind, string text)
        {
            foreach (var existing in target)
                if (existing.Kind == kind && existing.Text == text)
                    return;
            target.Add(new PendingSummaryItemState { Kind = kind, Text = text });
        }

        private static string Title(string? titleKey, string fallbackId) =>
            string.IsNullOrWhiteSpace(titleKey) ? fallbackId : titleKey;
    }
}
