using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Economy;
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

        /// <summary>
        /// Per-kind retention bounds (D-055). These sum to exactly
        /// <see cref="PendingReturnSummaryState.MaxItems"/> (12), and the rotation rule is
        /// applied PER KIND: when a kind is full, its own oldest item is retired so a newer
        /// fact of the same kind can appear. Without this the summary only ever grew, and
        /// after roughly the first dozen region events every new Transformation item was
        /// dropped in favour of the oldest ones and no Production, Notice or Aggregate item
        /// was ever shown again — so the least-engaged player, who is exactly the player
        /// this surface exists for, got the least useful information.
        ///
        /// A lower-priority item is NEVER promoted above a higher-priority one to make
        /// room: a full Transformation bucket cannot push an Aggregate out.
        /// </summary>
        private static readonly (SummaryItemKind Kind, int Max)[] KindBounds =
        {
            (SummaryItemKind.Transformation, 5),
            (SummaryItemKind.ActionableDecision, 3),
            (SummaryItemKind.Production, 2),
            (SummaryItemKind.Notice, 1),
            (SummaryItemKind.Aggregate, 1),
        };

        private static int BoundFor(SummaryItemKind kind)
        {
            foreach (var (boundKind, max) in KindBounds)
                if (boundKind == kind)
                    return max;
            return 0;
        }

        public static PendingReturnSummaryState Compose(
            IEnumerable<SimulationEvent> events,
            RegionDefinition content,
            PendingReturnSummaryState? existing,
            DateTimeOffset generatedAtUtc,
            bool regionCompleted = false)
        {
            var merged = new List<PendingSummaryItemState>();
            if (existing != null)
                merged.AddRange(existing.Items);

            AppendEventItems(events, content, merged);
            StampCurrency(merged, generatedAtUtc);
            return Finalize(merged, generatedAtUtc, regionCompleted);
        }

        /// <summary>
        /// Stamps every item that does not already carry a currency timestamp (D-055).
        /// Items carried over from a previous composition keep their ORIGINAL first-commit
        /// time, which is what makes rotation meaningful: an item that has been sitting in
        /// the summary for a week is visibly older than one committed today.
        /// </summary>
        private static void StampCurrency(List<PendingSummaryItemState> items, DateTimeOffset generatedAtUtc)
        {
            foreach (var item in items)
                if (item.FirstCommittedAtUtc == default)
                    item.FirstCommittedAtUtc = generatedAtUtc;
        }

        /// <summary>Appends an operational notice (backup recovery, save warnings) to pending state.</summary>
        public static PendingReturnSummaryState WithNotice(
            PendingReturnSummaryState? state, DateTimeOffset generatedAtUtc, string noticeText)
        {
            var merged = new List<PendingSummaryItemState>();
            if (state != null)
                merged.AddRange(state.Items);
            merged.Add(new PendingSummaryItemState
            {
                Kind = SummaryItemKind.Notice,
                Text = noticeText,
                FirstCommittedAtUtc = generatedAtUtc,
            });
            StampCurrency(merged, generatedAtUtc);
            return Finalize(merged, generatedAtUtc, false);
        }

        private static void AppendEventItems(IEnumerable<SimulationEvent>? events, RegionDefinition content, List<PendingSummaryItemState> merged)
        {
            if (events == null)
                return;

            int completions = 0;
            int duplicates = 0;
            long vitalityCredited = 0L;
            long producerMilliDelivered = 0L;
            string? resourceCappedProducerId = null;
            string? storeCappedProducerId = null;
            bool clockSkew = false;
            int availableNamed = 0;
            int availableExtra = 0;
            var unlockedProducers = new List<string>();
            var unlockedDiscoveries = new List<string>();
            var availableExpeditions = new List<string>();
            var completedExpeditions = new List<(string Id, ResourceType? RewardType, long Units)>();

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
                        // D-065: newly composed lines use the shared glance phrase form, so a stored
                        // sentence and the read-time glance never disagree about the same milestone.
                        // Already-stored lines are NOT migrated — historical text keeps its form.
                        AddDeduped(merged, SummaryItemKind.Transformation,
                            GlancePhrases.FormatLandmark(content.FindLandmark(stage.LandmarkId)?.TitleKey, stage.Stage));
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
                        if (produced.HitResourceCap)
                            resourceCappedProducerId = produced.ProducerId;
                        else if (produced.HitCapacity)
                            storeCappedProducerId = produced.ProducerId;
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

                    case DiscoveryUnlocked discovery:
                        if (!unlockedDiscoveries.Contains(discovery.DiscoveryId))
                            unlockedDiscoveries.Add(discovery.DiscoveryId);
                        break;

                    case ExpeditionAvailable expeditionAvailable:
                        if (!availableExpeditions.Contains(expeditionAvailable.ExpeditionId))
                            availableExpeditions.Add(expeditionAvailable.ExpeditionId);
                        break;

                    case ExpeditionCompleted expeditionCompleted:
                        if (!completedExpeditions.Exists(e => e.Id == expeditionCompleted.ExpeditionId))
                            completedExpeditions.Add((expeditionCompleted.ExpeditionId, expeditionCompleted.RewardType, expeditionCompleted.UnitsGranted));
                        break;

                    case StoryChapterUnlocked chapter:
                        AddDeduped(merged, SummaryItemKind.Transformation,
                            "New chapter: " + Title(FindChapterTitle(content, chapter.ChapterId), chapter.ChapterId) + ".");
                        break;

                    case RegionProgressionAdvanced progression:
                        AddDeduped(merged, SummaryItemKind.Transformation,
                            ProgressionLine(progression.Axis == RegionProgressionAxis.Ecology, progression.Stage));
                        break;

                    case RegionCompleted:
                        AddDeduped(merged, SummaryItemKind.Transformation,
                            content.TitleKey + "'s restoration is complete.");
                        break;
                }
            }

            foreach (var producerId in unlockedProducers)
                AddDeduped(merged, SummaryItemKind.Production,
                    Title(content.FindProducer(producerId)?.TitleKey, producerId) + " is now operating.");

            foreach (var discoveryId in unlockedDiscoveries)
                AddDeduped(merged, SummaryItemKind.Notice,
                    "Discovery recorded: " + Title(content.FindDiscovery(discoveryId)?.TitleKey, discoveryId) + ".");

            foreach (var expeditionId in availableExpeditions)
                AddDeduped(merged, SummaryItemKind.Notice,
                    Title(content.FindExpedition(expeditionId)?.TitleKey, expeditionId) + " route is ready.");

            foreach (var (expeditionId, rewardType, units) in completedExpeditions)
                AddDeduped(merged, SummaryItemKind.Transformation,
                    Title(content.FindExpedition(expeditionId)?.TitleKey, expeditionId) + " expedition complete"
                    + (rewardType == null ? "." : $" (+{units:N0} {rewardType})."));

            if (availableExtra > 0)
                AddDeduped(merged, SummaryItemKind.ActionableDecision, $"{availableExtra} more projects became ready to queue.");

            if (producerMilliDelivered >= ProducerDefinition.MilliUnitsPerUnit)
                AddDeduped(merged, SummaryItemKind.Production,
                    $"Producers delivered {producerMilliDelivered / ProducerDefinition.MilliUnitsPerUnit:N0} units.");

            if (resourceCappedProducerId != null)
            {
                // States plainly that nothing was lost, and how it comes back: the surplus is
                // waiting in the producer's store and flushes on its own once the player
                // spends some. Without this line a capped producer is indistinguishable from
                // one that simply did not run (D-054).
                AddDeduped(merged, SummaryItemKind.Production,
                    Title(content.FindProducer(resourceCappedProducerId)?.TitleKey, resourceCappedProducerId)
                    + " is at its storage cap; the surplus is waiting and returns on its own. Nothing was lost.");
            }

            if (storeCappedProducerId != null)
            {
                AddDeduped(merged, SummaryItemKind.Production,
                    Title(content.FindProducer(storeCappedProducerId)?.TitleKey, storeCappedProducerId)
                    + " storage filled; surplus time created no waste.");
            }

            if (completions > 1)
                AddDeduped(merged, SummaryItemKind.Transformation, $"{completions} restoration projects are complete.");

            if (vitalityCredited != 0L)
                AddDeduped(merged, SummaryItemKind.Aggregate, $"Vitality credited: {vitalityCredited:N0}.");

            if (duplicates > 0)
                AddDeduped(merged, SummaryItemKind.Aggregate, $"{duplicates} duplicate activity report(s) were safely ignored.");

            if (clockSkew)
                AddDeduped(merged, SummaryItemKind.Notice, "A backwards device-clock change was ignored to protect your progress.");
        }

        private static PendingReturnSummaryState Finalize(List<PendingSummaryItemState> merged, DateTimeOffset generatedAtUtc, bool regionCompleted)
        {
            // EXPLICIT stable ordering (D-055): kind ordinal first, then first-commit order
            // within a kind. The previous code called List<T>.Sort with a kind-only
            // comparator and then claimed "insertion order within a kind" in a comment —
            // List.Sort is NOT stable, so the claim was false and the presentation order
            // within a kind was arbitrary. Indexing the input makes the guarantee real
            // without depending on a sort implementation detail.
            var indexed = new List<(PendingSummaryItemState Item, int Index)>(merged.Count);
            for (int i = 0; i < merged.Count; i++)
                if (merged[i] != null)
                    indexed.Add((merged[i], i));

            indexed.Sort((a, b) =>
            {
                int byKind = ((int)a.Item.Kind).CompareTo((int)b.Item.Kind);
                return byKind != 0 ? byKind : a.Index.CompareTo(b.Index);
            });

            var state = new PendingReturnSummaryState { GeneratedAtUtc = generatedAtUtc };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var perKindCount = new Dictionary<SummaryItemKind, int>();

            foreach (var (item, _) in indexed)
            {
                if (string.IsNullOrWhiteSpace(item.Text))
                    continue;
                if (!seen.Add(item.Kind + "|" + item.Text))
                    continue;

                // Per-kind bound: retire this kind's oldest retained item to make room.
                int bound = BoundFor(item.Kind);
                perKindCount.TryGetValue(item.Kind, out int held);
                // An item with no currency (a pre-D-055 save) is treated as current: it
                // genuinely is the most recent thing that happened to the player.
                var committedAt = item.FirstCommittedAtUtc == default
                    ? generatedAtUtc
                    : item.FirstCommittedAtUtc;

                if (held >= bound)
                {
                    // D-163 (KI-026): the oldest batch still rotates out first (D-055), but within
                    // that batch the lowest-value line goes first, so a crowded day keeps the
                    // project's own completion line and drops the landmark line that repeats it.
                    int index = PickRetired(state.Items, item.Kind, committedAt, item.Text);
                    if (index == IncomingRetired)
                        continue;
                    if (index < 0)
                        continue;
                    state.Items.RemoveAt(index);
                    held--;
                }

                state.Items.Add(new PendingSummaryItemState
                {
                    Kind = item.Kind,
                    Text = item.Text,
                    FirstCommittedAtUtc = committedAt,
                });
                perKindCount[item.Kind] = held + 1;
            }

            // The per-kind sums already bound the total, but the hard bound is the
            // documented contract and is enforced independently so a future KindBounds
            // edit cannot silently exceed it.
            if (state.Items.Count > PendingReturnSummaryState.MaxItems)
            {
                var excess = state.Items.Count - PendingReturnSummaryState.MaxItems;
                for (int i = state.Items.Count - 1; i >= 0 && excess > 0; i--)
                {
                    if (state.Items[i].Kind == SummaryItemKind.Aggregate)
                    {
                        state.Items.RemoveAt(i);
                        excess--;
                    }
                }
            }

            state.PrimaryNextAction = DerivePrimaryNextAction(state.Items, regionCompleted);
            return state;
        }

        private const int IncomingRetired = int.MaxValue;

        /// <summary>
        /// D-163 (KI-026): which item a full kind bucket retires. Oldest first-commit time first
        /// (D-055 rotation, unchanged across batches); within that oldest batch the lowest-value line
        /// (<see cref="RetentionRank"/>); then the oldest held item. The incoming item competes too:
        /// it is retired (not added) when it is in the oldest batch and worth less than every held
        /// item there. Returns <see cref="IncomingRetired"/> for the incoming item, or -1 when the
        /// kind holds nothing.
        /// </summary>
        private static int PickRetired(List<PendingSummaryItemState> held, SummaryItemKind kind, DateTimeOffset incomingAt, string incomingText)
        {
            int best = -1;
            DateTimeOffset bestAt = default;
            int bestRank = 0;
            for (int i = 0; i < held.Count; i++)
            {
                if (held[i].Kind != kind) continue;
                var at = held[i].FirstCommittedAtUtc;
                int rank = Rank(kind, held[i].Text);
                if (best < 0 || at < bestAt || (at == bestAt && rank > bestRank))
                {
                    best = i; bestAt = at; bestRank = rank;
                }
            }
            if (best < 0) return -1;
            if (incomingAt == bestAt && Rank(kind, incomingText) > bestRank) return IncomingRetired;
            return best;
        }

        private static int Rank(SummaryItemKind kind, string? text) =>
            kind == SummaryItemKind.Transformation ? RetentionRank(text) : 3;

        /// <summary>
        /// D-163: how much a Transformation line is worth keeping when its bucket is full (0 keeps
        /// longest): region complete, a project's own completion line, a new chapter (it carries the
        /// "read the new chapter" action), an expedition (also counted on Discoveries and in the
        /// celebration recap), the "N projects are complete" count, the ecology/settlement arc lines,
        /// and last the landmark stage line, which the summary screen already repeats under the
        /// project's completion line (D-100). Other kinds all share one rank, so their rotation is
        /// exactly D-055's.
        /// </summary>
        public static int RetentionRank(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 3;
            string t = text!;
            if (t.EndsWith("'s restoration is complete.", StringComparison.Ordinal)) return 0;
            if (t.EndsWith(" restoration projects are complete.", StringComparison.Ordinal)) return 4;
            if (t.EndsWith(" is complete.", StringComparison.Ordinal)) return 1;
            if (t.StartsWith("New chapter: ", StringComparison.Ordinal)) return 2;
            if (t.Contains(" expedition complete")) return 3;
            foreach (var line in EcologyLines) if (t == line) return 5;
            foreach (var line in SettlementLines) if (t == line) return 5;
            if (t.EndsWith(" has reached stage", StringComparison.Ordinal) || t.Contains(" has reached stage ")) return 5;
            foreach (RestorationStage stage in Enum.GetValues(typeof(RestorationStage)))
                if (t.EndsWith(" " + GlancePhrases.PhraseFor(stage) + ".", StringComparison.Ordinal)) return 6;
            return 3;
        }

        /// <summary>
        /// One primary next action: queue/choose restoration work when something changed;
        /// otherwise null — an explicit "nothing needs attention" state.
        ///
        /// Completion-aware (D-061): a completed region has no queueable work left, so the
        /// action is never offered once the closure milestone is reached, whatever items a
        /// future event source happens to contribute.
        /// </summary>
        private static string? DerivePrimaryNextAction(List<PendingSummaryItemState> items, bool regionCompleted)
        {
            if (regionCompleted)
                return null;
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
        /// <summary>
        /// D-140 (Region 1 narrative polish): authored sentences for the ecology and settlement arcs,
        /// replacing "has reached stage N" (a mechanical number the glance had to hide as an opaque
        /// change). Newly composed lines only; already-stored lines keep their form (D-065 precedent).
        /// A stage outside the authored table keeps the old numbered form.
        /// </summary>
        public static readonly IReadOnlyList<string> EcologyLines = new[]
        {
            "The valley's ecology is stirring: the woods are open to the light again.",
            "The valley's ecology is recovering: clear water is held in the reservoir.",
            "The valley's ecology is taking root: native sedges line the water's edge.",
            "The valley's ecology is thriving: birds are nesting on the new islets.",
        };

        public static readonly IReadOnlyList<string> SettlementLines = new[]
        {
            "The settlement is waking: there is work at the bench again.",
            "The settlement is growing: clean water runs to every house.",
            "The settlement is gathering: the market hall is open again.",
            "The settlement is lit: the square glows after dark.",
        };

        public static string ProgressionLine(bool ecology, int stage)
        {
            var table = ecology ? EcologyLines : SettlementLines;
            if (stage >= 1 && stage <= table.Count) return table[stage - 1];
            return (ecology ? "The valley's ecology" : "The settlement") + $" has reached stage {stage}.";
        }

        private static string? FindChapterTitle(RegionDefinition content, string chapterId)
        {
            foreach (var c in content.Chapters)
                if (c.Id == chapterId) return c.TitleKey;
            return null;
        }

        private static string Title(string? titleKey, string fallbackId) =>
            string.IsNullOrWhiteSpace(titleKey) ? fallbackId : titleKey;
    }
}
