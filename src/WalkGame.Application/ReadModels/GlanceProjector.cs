using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
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

        /// <summary>
        /// D-122 slice 3: the stable overlay id for a glance phrase ("glance.&lt;name&gt;#text").
        /// Glance sentences are read-time presentation only; stored summary lines keep using the
        /// English forms below, so a locale never reaches a save.
        /// </summary>
        public static string PhraseId(string name) => "glance." + name;

        /// <summary>The overlay field used by every glance phrase.</summary>
        public const string PhraseField = "text";

        /// <summary>A glance phrase through the overlay; English when absent.</summary>
        public static string Localize(ContentText? text, string name, string english) =>
            text == null ? english : text.Resolve(PhraseId(name), PhraseField, english);

        /// <summary>Stage phrase through the overlay (glance.stage.&lt;stage&gt;).</summary>
        public static string PhraseFor(RestorationStage stage, ContentText? text) =>
            Localize(text, "stage." + stage.ToString().ToLowerInvariant(), PhraseFor(stage));

        /// <summary>
        /// The glance form of "{title} {phrase}." through the overlay. The sentence pattern is
        /// itself an overlay entry (glance.landmark-sentence, "{0} {1}.") so word order can change.
        /// </summary>
        public static string FormatLandmark(string? title, RestorationStage stage, ContentText? text)
        {
            if (text == null) return FormatLandmark(title, stage);
            string subject = string.IsNullOrWhiteSpace(title) ? Localize(text, "untitled-project", UntitledProject) : title!;
            return Format(text, "landmark-sentence", "{0} {1}.", subject, PhraseFor(stage, text));
        }

        /// <summary>A pattern phrase through the overlay; a broken localized pattern falls back to English.</summary>
        public static string Format(ContentText? text, string name, string englishPattern, params object[] args)
        {
            string pattern = Localize(text, name, englishPattern);
            try { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
            catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, englishPattern, args); }
        }

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
        public static string PlayerLeadFor(string? text) => PlayerLeadFor(text, null);

        /// <summary>
        /// <see cref="PlayerLeadFor(string?)"/> with the D-122 overlay for the opaque lead and the
        /// re-rendered legacy form. Other stored text is shown as stored (never translated).
        /// </summary>
        public static string PlayerLeadFor(string? text, ContentText? locale)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Localize(locale, "opaque-change", OpaqueTransformationLead);

            var legacy = LegacyStageLine.Match(text!);
            if (legacy.Success)
            {
                var stage = Enum.TryParse(legacy.Groups["stage"].Value, ignoreCase: false,
                    out RestorationStage parsed) ? parsed : RestorationStage.Ruined;
                return FormatLandmark(legacy.Groups["title"].Value, stage, locale);
            }

            return ContainsStageIdentifier(text) ? Localize(locale, "opaque-change", OpaqueTransformationLead) : text!;
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
            PostCompletionProgramDefinition? postCompletionProgram,
            ContentText? text = null)
        {
            if (home == null) throw new ArgumentNullException(nameof(home));
            if (onboarding == null) throw new ArgumentNullException(nameof(onboarding));
            if (activityStatus == null) throw new ArgumentNullException(nameof(activityStatus));
            if (content == null) throw new ArgumentNullException(nameof(content));

            var (leadSentence, furtherChanges) = ProjectWhatChanged(pendingSummary, text);
            string restoringNow = ProjectRestoringNow(home, content, postCompletionProgram, text);
            (string attentionSentence, string? primaryAction, bool requiresAttention) =
                ProjectAttention(home, leadSentence, text);

            return new GlanceReadModel(
                leadSentence,
                furtherChanges,
                restoringNow,
                requiresAttention,
                attentionSentence,
                primaryAction,
                ActivitySentenceFor(activityStatus, text),
                OnboardingSentenceFor(onboarding, home, text),
                ProjectPayoffSentence(home, content, text));
        }

        // ------------------------------------------------------------------ payoff (D-145)

        /// <summary>
        /// "{title}: {n}% restored. About {steps} more steps to ..." for a funded, incomplete
        /// active project; null otherwise. The percent is at least 1 once anything is invested
        /// so a short first walk never reads as zero.
        /// </summary>
        private static string? ProjectPayoffSentence(HomeReadModel home, RegionDefinition content, ContentText? text)
        {
            var payoff = home.ActiveProjectPayoff;
            if (payoff == null || home.ActiveProjectId == null) return null;
            string title = DisplayTitle(LocalTitle(text, home.ActiveProjectId, content.FindProject(home.ActiveProjectId)?.TitleKey), home.ActiveProjectTitleKey, text);
            string percent = payoff.Value.Percent.ToString(CultureInfo.InvariantCulture);
            string steps = payoff.Value.StepsToNext.ToString("N0", CultureInfo.InvariantCulture);
            return payoff.Value.NextIsBuildStage
                ? GlancePhrases.Format(text, "payoff-next-stage", "{0}: {1}% restored. About {2} more steps to its next visible stage.", title, percent, steps)
                : GlancePhrases.Format(text, "payoff-finish", "{0}: {1}% restored. About {2} more steps to finish it.", title, percent, steps);
        }

        // ------------------------------------------------------------------ what changed

        /// <summary>
        /// Lead uses CURRENT items only (a summary of pure history is not news), lowest kind
        /// ordinal first so a world transformation always beats a numeric activity aggregate,
        /// composer order breaking ties. The remainder of the current items becomes the bounded
        /// further-change count instead of being dumped on the player.
        /// </summary>
        private static (string Sentence, int FurtherChanges) ProjectWhatChanged(
            ReturnSummaryReadModel? pendingSummary, ContentText? text)
        {
            string nothing = GlancePhrases.Localize(text, "nothing-changed", "Nothing has changed since you were last here.");
            if (pendingSummary == null)
                return (nothing, 0);

            var current = new List<ReturnSummaryReadModel.ItemRow>();
            foreach (var item in pendingSummary.Items)
                if (item.IsCurrent)
                    current.Add(item);

            if (current.Count == 0)
                return (nothing, 0);

            // Stable ranking by kind ordinal, ties keeping composer order. A single linear
            // pass is enough and cannot depend on a sort implementation's stability.
            int leadIndex = 0;
            for (int i = 1; i < current.Count; i++)
                if ((int)current[i].Kind < (int)current[leadIndex].Kind)
                    leadIndex = i;

            return (GlancePhrases.PlayerLeadFor(current[leadIndex].Text, text), current.Count - 1);
        }

        // ------------------------------------------------------------------ restoring now

        private static string ProjectRestoringNow(
            HomeReadModel home, RegionDefinition content, PostCompletionProgramDefinition? postCompletionProgram,
            ContentText? text)
        {
            if (home.ActiveProjectId != null)
            {
                string title = DisplayTitle(LocalTitle(text, home.ActiveProjectId, content.FindProject(home.ActiveProjectId)?.TitleKey), home.ActiveProjectTitleKey, text);
                return GlancePhrases.Format(text, "underway", "{0} is underway ({1} of {2}).", title,
                    home.ActiveProjectInvested.ToString(CultureInfo.InvariantCulture),
                    home.ActiveProjectCost.ToString(CultureInfo.InvariantCulture));
            }

            if (home.Queued.Count > 0)
            {
                string next = DisplayTitle(LocalTitle(text, home.Queued[0].ProjectId, content.FindProject(home.Queued[0].ProjectId)?.TitleKey), home.Queued[0].TitleKey, text);
                return GlancePhrases.Format(text, "next-up", "Next up is {0}.", next);
            }

            if (home.RegionComplete && postCompletionProgram != null)
            {
                return GlancePhrases.Format(text, "care-continues", "{0} continues ({1} of {2} this cycle).",
                    DisplayTitle(LocalTitle(text, postCompletionProgram.Id, postCompletionProgram.TitleKey), null, text),
                    home.PostCompletionCareInvested.ToString(CultureInfo.InvariantCulture),
                    home.PostCompletionCareCostPerCycle.ToString(CultureInfo.InvariantCulture));
            }

            return GlancePhrases.Localize(text, "nothing-restoring", "Nothing is being restored right now.");
        }

        /// <summary>D-122: an authored title through the overlay by stable id (null stays null).</summary>
        private static string? LocalTitle(ContentText? text, string stableId, string? authoredEnglish) =>
            text == null || authoredEnglish == null ? authoredEnglish : text.Resolve(stableId, ContentText.Fields.Title, authoredEnglish);

        /// <summary>
        /// A display title, never a stable content identifier.
        ///
        /// Typing is explicit (D-125): every read-model title field carries display text only
        /// (authored English or its overlay), and an unknown title is blank, never an id. So
        /// any non-blank value is text, even when a translation contains a '.' ("St. Ives").
        /// There is deliberately no dot heuristic: the id never reaches this method.
        /// </summary>
        private static string DisplayTitle(string? titleText, string? fallbackTitleText, ContentText? text)
        {
            if (IsDisplayTitle(titleText)) return titleText!;
            if (IsDisplayTitle(fallbackTitleText) && !GlancePhrases.ContainsStageIdentifier(fallbackTitleText))
                return fallbackTitleText!;
            return GlancePhrases.Localize(text, "untitled-project", GlancePhrases.UntitledProject);
        }

        private static bool IsDisplayTitle(string? value) => !string.IsNullOrWhiteSpace(value);

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
            HomeReadModel home, string leadSentence, ContentText? text)
        {
            // A finished region outranks any attention reason: the queue is empty forever.
            if (home.RegionComplete)
                return (Complete(home, text), null, false);

            switch (home.AttentionReason)
            {
                case HomeAttentionReason.PendingReturnSummary:
                    // The action is only forwarded while the region is genuinely unfinished,
                    // so a stale summary string cannot resurrect work that was completed.
                    return (leadSentence, home.PrimaryNextAction, home.RequiresAttention);

                case HomeAttentionReason.QueueEmptyWithBankedVitality:
                    return (GlancePhrases.Localize(text, "paused", "Restoration is paused. Your movement is saved until you choose a project."),
                        GlancePhrases.Localize(text, "choose-project-action", "Choose a restoration project."), true);

                case HomeAttentionReason.NoProjectStartedYet:
                    return (GlancePhrases.Localize(text, "choose-first", "Choose the first thing to restore."), null, false);

                case HomeAttentionReason.RegionComplete:
                    return (Complete(home, text), null, false);

                default:
                    return (GlancePhrases.Localize(text, "nothing-needed", "Nothing needs your attention right now."), null, false);
            }
        }

        private static string Complete(HomeReadModel home, ContentText? text) =>
            GlancePhrases.Format(text, "region-complete", "{0}'s restoration is complete.", DisplayRegionTitle(home.RegionTitleKey, text));

        private static string DisplayRegionTitle(string? regionTitleKey, ContentText? text) =>
            string.IsNullOrWhiteSpace(regionTitleKey) ? GlancePhrases.Localize(text, "this-valley", "This valley") : regionTitleKey!;

        // ------------------------------------------------------------------ activity status

        private static string ActivitySentenceFor(ActivityStatusReadModel status, ContentText? text) =>
            GlancePhrases.Localize(text, "activity." + status.Status.ToString().ToLowerInvariant(), status.Status switch
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
            });

        // ------------------------------------------------------------------ onboarding

        /// <summary>
        /// The sentence for the CURRENT onboarding step only — never a later one, and nothing at
        /// all once onboarding is complete. Denial does not skip, block, or repeat the
        /// connection step: the step's sentence is the same whether permission was denied or
        /// simply not requested yet, so a denial cannot become a trap or a loop.
        /// </summary>
        private static string? OnboardingSentenceFor(OnboardingReadModel onboarding, HomeReadModel home, ContentText? text)
        {
            if (onboarding.NextAction == OnboardingNextAction.ShowWorldBaseline) return BaselineLandmarkSentence(home, text);
            string? english = onboarding.NextAction switch
            {
                OnboardingNextAction.ExplainPremise => "Your movement restores this world.",
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
            if (english == null) return null;
            string name = onboarding.NextAction == OnboardingNextAction.OfferActivityConnection
                && onboarding.ActivityStep == OnboardingActivityStepState.ProviderUpdateRequired
                ? "onboarding.offeractivityconnection.update"
                : "onboarding." + onboarding.NextAction.ToString().ToLowerInvariant();
            return GlancePhrases.Localize(text, name, english);
        }

        /// <summary>
        /// One damaged landmark as the world baseline: the first in content order whose current
        /// stage is Ruined, else the first landmark. Prefers a damaged one so the prompt to
        /// restore is visible; degrades to a generic line rather than an id.
        /// </summary>
        private static string BaselineLandmarkSentence(HomeReadModel home, ContentText? text)
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
                ? GlancePhrases.Localize(text, "any-landmark-baseline", GlancePhrases.AnyLandmarkBaseline)
                : GlancePhrases.FormatLandmark(chosen.TitleKey, RestorationStage.Ruined, text);
        }
    }
}
