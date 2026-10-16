using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using WalkGame.Application.Activity;
using WalkGame.Application.Summaries;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// The closed phrase table for landmark stages, shared by the glance projection and by
    /// newly composed durable summary lines.
    ///
    /// Lives here rather than in Domain because Domain must not grow player copy, and shared
    /// because a glance sentence and a stored summary line describing the same landmark advance
    /// must use the same wording. The phrases are the authored, pinned set from the change's
    /// spec — a sixth mapping is a new change, not an implementation convenience.
    /// </summary>
    public static class GlancePhrases
    {
        /// <summary>Fallback when a project's display title is unavailable. Never an id.</summary>
        public const string UntitledProject = "A restoration project";

        /// <summary>Lead used when stored text cannot be shown without echoing a stage id.</summary>
        public const string OpaqueTransformationLead = "The valley changed.";

        /// <summary>Baseline sentence when no damaged landmark exists to point at.</summary>
        public const string AnyLandmarkBaseline = "The valley needs your help.";

        public static string PhraseFor(RestorationStage stage) => stage switch
        {
            RestorationStage.Ruined => "is still damaged",
            RestorationStage.Stabilized => "is safe to approach",
            RestorationStage.Functional => "is working again",
            RestorationStage.Restored => "is restored",
            RestorationStage.Flourishing => "is thriving",
            _ => "is still damaged",
        };

        /// <summary>"{title} {phrase}." — the one form both the glance and new summaries use.</summary>
        public static string FormatLandmark(string? title, RestorationStage stage)
        {
            string subject = string.IsNullOrWhiteSpace(title) ? UntitledProject : title!;
            return subject + " " + PhraseFor(stage) + ".";
        }

        // Whole-word stage identifiers, matched exactly. "is restored" contains the lowercase
        // word and is deliberately NOT matched: the check is the enum spelling as a whole word.
        private static readonly Regex StageIdentifier = new Regex(
            @"\b(Ruined|Stabilized|Functional|Restored|Flourishing)\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex LegacyStageLine = new Regex(
            @"^(?<title>.+?) has reached (?<stage>Ruined|Stabilized|Functional|Restored|Flourishing)\.$",
            RegexOptions.CultureInvariant);

        /// <summary>True when text would echo a stage identifier to the player.</summary>
        public static bool ContainsStageIdentifier(string? text) =>
            text != null && StageIdentifier.IsMatch(text);

        /// <summary>
        /// The glance-safe lead sentence for a stored summary item's text.
        ///
        /// A historical line of the form "{known landmark title} has reached {Stage}." is
        /// re-rendered in the phrase form at read time WITHOUT writing the substitution back —
        /// stored summary text is never migrated. Any other text that would echo a stage
        /// identifier falls back to the opaque lead rather than leaking the enum spelling.
        /// </summary>
        public static string PlayerLeadFor(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return OpaqueTransformationLead;

            var legacy = LegacyStageLine.Match(text!);
            if (legacy.Success)
            {
                var stage = Enum.TryParse(legacy.Groups["stage"].Value, ignoreCase: false,
                    out RestorationStage parsed) ? parsed : RestorationStage.Ruined;
                return FormatLandmark(legacy.Groups["title"].Value, stage);
            }

            return ContainsStageIdentifier(text) ? OpaqueTransformationLead : text!;
        }
    }

    /// <summary>
    /// Pure, deterministic projection of already-known facts into glance sentences (design.md
    /// §3). It consumes <see cref="HomeReadModel"/>'s attention classification instead of
    /// re-deriving it, and never reads canonical state, saves, or the clock — so the same
    /// inputs always produce the same sentences and reading one mutates nothing.
    /// </summary>
    public static class GlanceProjector
    {
        public static GlanceReadModel Project(
            HomeReadModel home,
            ReturnSummaryReadModel? pendingSummary,
            OnboardingReadModel onboarding,
            ActivityStatusReadModel activityStatus,
            RegionDefinition content,
            PostCompletionProgramDefinition? postCompletionProgram)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            if (onboarding == null) throw new ArgumentNullException(nameof(onboarding));
            if (activityStatus == null) throw new ArgumentNullException(nameof(activityStatus));
            if (content == null) throw new ArgumentNullException(nameof(content));

            var (leadSentence, furtherChanges) = ProjectWhatChanged(pendingSummary);
            string restoringNow = ProjectRestoringNow(home, content, postCompletionProgram);
            (string attentionSentence, string? primaryAction, bool requiresAttention) =
                ProjectAttention(home, leadSentence);

            return new GlanceReadModel(
                leadSentence,
                furtherChanges,
                restoringNow,
                requiresAttention,
                attentionSentence,
                primaryAction,
                ActivitySentenceFor(activityStatus),
                OnboardingSentenceFor(onboarding, home));
        }

        // ------------------------------------------------------------------ what changed

        /// <summary>
        /// Lead uses CURRENT items only (a summary of pure history is not news), lowest kind
        /// ordinal first so a world transformation always beats a numeric activity aggregate,
        /// composer order breaking ties. The remainder of the current items becomes the bounded
        /// further-change count instead of being dumped on the player.
        /// </summary>
        private static (string Sentence, int FurtherChanges) ProjectWhatChanged(
            ReturnSummaryReadModel? pendingSummary)
        {
            if (pendingSummary == null)
                return ("Nothing has changed since you were last here.", 0);

            var current = new List<ReturnSummaryReadModel.ItemRow>();
            foreach (var item in pendingSummary.Items)
                if (item.IsCurrent)
                    current.Add(item);

            if (current.Count == 0)
                return ("Nothing has changed since you were last here.", 0);

            // Stable ranking by kind ordinal, ties keeping composer order. A single linear
            // pass is enough and cannot depend on a sort implementation's stability.
            int leadIndex = 0;
            for (int i = 1; i < current.Count; i++)
                if ((int)current[i].Kind < (int)current[leadIndex].Kind)
                    leadIndex = i;

            return (GlancePhrases.PlayerLeadFor(current[leadIndex].Text), current.Count - 1);
        }

        // ------------------------------------------------------------------ restoring now

        private static string ProjectRestoringNow(
            HomeReadModel home, RegionDefinition content, PostCompletionProgramDefinition? postCompletionProgram)
        {
            if (home.ActiveProjectId != null)
            {
                string title = DisplayTitle(content.FindProject(home.ActiveProjectId)?.TitleKey, home.ActiveProjectTitleKey);
                return title + " is underway (" + home.ActiveProjectInvested.ToString(CultureInfo.InvariantCulture)
                    + " of " + home.ActiveProjectCost.ToString(CultureInfo.InvariantCulture) + ").";
            }

            if (home.Queued.Count > 0)
            {
                string next = DisplayTitle(content.FindProject(home.Queued[0].ProjectId)?.TitleKey, home.Queued[0].TitleKey);
                return "Next up is " + next + ".";
            }

            if (home.RegionComplete && postCompletionProgram != null)
            {
                return DisplayTitle(postCompletionProgram.TitleKey, null)
                    + " continues (" + home.PostCompletionCareInvested.ToString(CultureInfo.InvariantCulture)
                    + " of " + home.PostCompletionCareCostPerCycle.ToString(CultureInfo.InvariantCulture)
                    + " this cycle).";
            }

            return "Nothing is being restored right now.";
        }

        /// <summary>
        /// A display title, never a stable content identifier.
        ///
        /// Display titles in the authored catalog are English phrases ("Old Trailhead") while
        /// stable ids carry a dotted namespace (`proj.*`, `lm.*`, `care.*`). The fallbacks that
        /// reach here are the id-using ones assembled by <see cref="HomeReadModel"/>, so a
        /// dotted value is treated as "no title" and replaced rather than read out as copy.
        /// </summary>
        private static string DisplayTitle(string? titleKey, string? fallbackTitleKey)
        {
            if (IsDisplayTitle(titleKey)) return titleKey!;
            if (IsDisplayTitle(fallbackTitleKey) && !GlancePhrases.ContainsStageIdentifier(fallbackTitleKey))
                return fallbackTitleKey!;
            return GlancePhrases.UntitledProject;
        }

        private static bool IsDisplayTitle(string? value) =>
            !string.IsNullOrWhiteSpace(value) && value!.IndexOf('.') < 0;

        // ------------------------------------------------------------------ attention

        /// <summary>
        /// One sentence per existing <see cref="HomeAttentionReason"/>, plus the attention
        /// flag the glance reports for it.
        ///
        /// <see cref="HomeReadModel.RegionComplete"/> is checked BEFORE the reason switch
        /// because <c>GetHome()</c> prefers <see cref="HomeAttentionReason.PendingReturnSummary"/>
        /// whenever a summary item is still current, so a region that has finished can arrive
        /// here carrying a summary-derived action and a summary-derived attention flag. The
        /// completed region is a conclusion: it names the region, offers no queue action, and
        /// never reports that attention is required.
        /// </summary>
        private static (string Sentence, string? Action, bool RequiresAttention) ProjectAttention(
            HomeReadModel home, string leadSentence)
        {
            // A finished region outranks any attention reason: the queue is empty forever.
            if (home.RegionComplete)
                return (DisplayRegionTitle(home.RegionTitleKey) + "'s restoration is complete.", null, false);

            switch (home.AttentionReason)
            {
                case HomeAttentionReason.PendingReturnSummary:
                    // The action is only forwarded while the region is genuinely unfinished,
                    // so a stale summary string cannot resurrect work that was completed.
                    return (leadSentence, home.PrimaryNextAction, home.RequiresAttention);

                case HomeAttentionReason.QueueEmptyWithBankedVitality:
                    return ("Restoration is paused. Your movement is saved until you choose a project.",
                        "Choose a restoration project.", true);

                case HomeAttentionReason.NoProjectStartedYet:
                    return ("Choose the first thing to restore.", null, false);

                case HomeAttentionReason.RegionComplete:
                    return (DisplayRegionTitle(home.RegionTitleKey) + "'s restoration is complete.", null, false);

                default:
                    return ("Nothing needs your attention right now.", null, false);
            }
        }

        private static string DisplayRegionTitle(string? regionTitleKey) =>
            string.IsNullOrWhiteSpace(regionTitleKey) ? "This valley" : regionTitleKey!;

        // ------------------------------------------------------------------ activity status

        private static string ActivitySentenceFor(ActivityStatusReadModel status) =>
            status.Status switch
            {
                ActivityPlayerStatus.ConnectedCurrent => "Your movement is connected.",
                ActivityPlayerStatus.PermissionNeeded
                    => "Connect movement to restore this world. Only walking activity is needed, and you can decide later.",
                ActivityPlayerStatus.PermissionDenied
                    => "Movement access is off. Your progress is still here, and you can keep looking around.",
                ActivityPlayerStatus.SourceUnavailable
                    => "Movement cannot be refreshed on this device. Your progress is still here.",
                ActivityPlayerStatus.WaitingForFirstData
                    => "Connected. The world is waiting for your first walk.",
                ActivityPlayerStatus.ProviderUpdateRequired
                    => "Your movement app needs an update before new walks can count. Your progress is still here.",
                _ => "Movement could not be refreshed just now. Nothing was lost.",
            };

        // ------------------------------------------------------------------ onboarding

        /// <summary>
        /// The sentence for the CURRENT onboarding step only — never a later one, and nothing at
        /// all once onboarding is complete. Denial does not skip, block, or repeat the
        /// connection step: the step's sentence is the same whether permission was denied or
        /// simply not requested yet, so a denial cannot become a trap or a loop.
        /// </summary>
        private static string? OnboardingSentenceFor(OnboardingReadModel onboarding, HomeReadModel home) =>
            onboarding.NextAction switch
            {
                OnboardingNextAction.ExplainPremise => "Your movement restores this world.",
                OnboardingNextAction.ShowWorldBaseline => BaselineLandmarkSentence(home),
                OnboardingNextAction.OfferActivityConnection
                    when onboarding.ActivityStep == OnboardingActivityStepState.ProviderUpdateRequired
                    => "Your movement app needs an update before new walks can count. You can keep going; nothing is locked.",
                OnboardingNextAction.OfferActivityConnection
                    => "Connect movement to restore this world. Only walking activity is needed, and you can decide later.",
                OnboardingNextAction.ChooseFirstProject => "Choose the first thing to restore.",
                OnboardingNextAction.DemonstrateProgression => "Progress can continue while you are away.",
                OnboardingNextAction.ShowExitMessage => "You do not need to keep this open.",
                _ => null,
            };

        /// <summary>
        /// One damaged landmark as the world baseline: the first in content order whose current
        /// stage is Ruined, else the first landmark. Prefers a damaged one so the prompt to
        /// restore is visible; degrades to a generic line rather than an id.
        /// </summary>
        private static string BaselineLandmarkSentence(HomeReadModel home)
        {
            HomeReadModel.LandmarkRow? damaged = null;
            HomeReadModel.LandmarkRow? first = null;
            foreach (var landmark in home.Landmarks)
            {
                if (first == null) first = landmark;
                if (landmark.Stage == RestorationStage.Ruined)
                {
                    damaged = landmark;
                    break;
                }
            }

            var chosen = damaged ?? first;
            return chosen == null
                ? GlancePhrases.AnyLandmarkBaseline
                : GlancePhrases.FormatLandmark(chosen.TitleKey, RestorationStage.Ruined);
        }
    }
}
