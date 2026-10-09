using System;
using System.Linq;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Summaries;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Ux;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Summaries;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// Tasks 4.1-4.8 of define-headless-glance-language (D-065): pins the glance phrase table, the
/// three questions, attention following the existing classification, titles over identifiers,
/// the activity sentences, onboarding including denial, the historical-sentence substitution,
/// and the no-leak/no-mutation guarantees.
/// </summary>
public sealed class GlanceLanguageTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeActivityConnectionPort _port = new();
    private readonly ManualClock _clock = new(TestSessions.T0);

    public void Dispose() => _temp.Dispose();

    private GameSession NewSession(bool withPreferences = true) =>
        TestSessions.Create(
            _temp.Path,
            _clock,
            withPreferences ? new LocalPreferencesStore(_temp.Path) : null,
            _port);

    /// <summary>
    /// A session with no connection port at all: the headless composition the simulation CLI
    /// runs, which must still produce a source-unavailable sentence rather than throwing.
    /// </summary>
    private GameSession NewPortlessSession() =>
        TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), null);

    private static readonly (RestorationStage Stage, string Phrase)[] PhraseTable =
    {
        (RestorationStage.Ruined, "is still damaged"),
        (RestorationStage.Stabilized, "is safe to approach"),
        (RestorationStage.Functional, "is working again"),
        (RestorationStage.Restored, "is restored"),
        (RestorationStage.Flourishing, "is thriving"),
    };

    // 4.1 -------------------------------------------------------------------

    [Fact]
    public void GlancePhrases_EveryStageMapsToItsPinnedPhrase()
    {
        Assert.Equal(
            PhraseTable.Select(p => p.Phrase).ToArray(),
            PhraseTable.Select(p => GlancePhrases.PhraseFor(p.Stage)).ToArray());
    }

    [Fact]
    public void GlancePhrases_AuthoredLandmarkComposesExactly()
    {
        Assert.Equal("Old Trailhead is safe to approach.", GlancePhrases.FormatLandmark("Old Trailhead", RestorationStage.Stabilized));
        Assert.Equal("Old Trailhead is still damaged.", GlancePhrases.FormatLandmark("Old Trailhead", RestorationStage.Ruined));
    }

    [Fact]
    public void GlancePhrases_MissingTitleNeverPrintsAnIdentifier()
    {
        Assert.Equal("A restoration project is still damaged.", GlancePhrases.FormatLandmark(null, RestorationStage.Ruined));
        Assert.Equal("A restoration project is thriving.", GlancePhrases.FormatLandmark("   ", RestorationStage.Flourishing));
    }

    // 4.2 -------------------------------------------------------------------

    [Fact]
    public void AWorldTransformationBeatsAnActivityAggregateAsTheLead()
    {
        var glance = GlanceFor(new PendingSummaryItemState
            {
                Kind = SummaryItemKind.Aggregate,
                Text = "Vitality credited: 4,200.",
                FirstCommittedAtUtc = TestSessions.T0,
            },
            new PendingSummaryItemState
            {
                Kind = SummaryItemKind.Transformation,
                Text = "Old Trailhead is safe to approach.",
                FirstCommittedAtUtc = TestSessions.T0,
            });

        // Aggregate is listed FIRST, yet the transformation leads: the rule is kind priority,
        // not composer order, so a number never outranks a world change.
        Assert.Equal("Old Trailhead is safe to approach.", glance.WhatChangedSentence);
        Assert.DoesNotContain("Vitality credited", glance.WhatChangedSentence);
    }

    [Fact]
    public void FurtherChangesAreCountedNotListed()
    {
        var glance = GlanceFor(
            pendingItem: new PendingSummaryItemState
            {
                Kind = SummaryItemKind.Transformation,
                Text = "Old Trailhead is safe to approach.",
                FirstCommittedAtUtc = TestSessions.T0,
            },
            extraCurrentItem: new PendingSummaryItemState
            {
                Kind = SummaryItemKind.Production,
                Text = "Saw Mill is now operating.",
                FirstCommittedAtUtc = TestSessions.T0,
            },
            secondExtraCurrentItem: new PendingSummaryItemState
            {
                Kind = SummaryItemKind.Notice,
                Text = "Discovery recorded: The Old Well.",
                FirstCommittedAtUtc = TestSessions.T0,
            });

        Assert.Equal(2, glance.FurtherChangeCount);
        Assert.Equal("2 more changes are waiting.", glance.FurtherChangesSentence);
    }

    [Fact]
    public void AQuietGlanceSaysNothingChangedAndNothingNeedsAttention()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        session.AdvanceOnboarding(OnboardingStage.Complete);

        var glance = session.GetGlance();

        Assert.Contains("Nothing has changed", glance.WhatChangedSentence);
        Assert.Equal(0, glance.FurtherChangeCount);
        Assert.Null(glance.FurtherChangesSentence);
        // With a project underway the attention classification is None, not a to-do.
        Assert.Equal("Nothing needs your attention right now.", glance.AttentionSentence);
        Assert.False(glance.RequiresAttention);
        Assert.Null(glance.PrimaryNextAction);
    }

    // 4.3 -------------------------------------------------------------------

    [Theory]
    [InlineData(HomeAttentionReason.PendingReturnSummary)]
    [InlineData(HomeAttentionReason.NoProjectStartedYet)]
    [InlineData(HomeAttentionReason.QueueEmptyWithBankedVitality)]
    [InlineData(HomeAttentionReason.RegionComplete)]
    [InlineData(HomeAttentionReason.None)]
    public void EveryAttentionReasonHasAnExplicitSentence(HomeAttentionReason reason)
    {
        var glance = GlanceForPendingWithReason(reason);

        Assert.False(string.IsNullOrWhiteSpace(glance.AttentionSentence));

        // Every reason must be a DIFFERENT sentence, so a shell never confuses two states.
        if (reason == HomeAttentionReason.None)
            Assert.Equal("Nothing needs your attention right now.", glance.AttentionSentence);
        else
            Assert.NotEqual("Nothing needs your attention right now.", glance.AttentionSentence);
    }

    [Fact]
    public void AFinishedValleyIsAConclusionNotAToDo()
    {
        var glance = GlanceFor(reason: HomeAttentionReason.RegionComplete);

        Assert.False(glance.RequiresAttention);
        Assert.Contains("restoration is complete", glance.AttentionSentence);
        Assert.DoesNotContain("Queue", glance.AttentionSentence);
        Assert.Null(glance.PrimaryNextAction);
    }

    [Fact]
    public void BankedProgressBeforeCompletionStillAsksForAChoice()
    {
        var glance = GlanceFor(reason: HomeAttentionReason.QueueEmptyWithBankedVitality);

        Assert.True(glance.RequiresAttention);
        Assert.Equal("Restoration is paused. Your movement is saved until you choose a project.", glance.AttentionSentence);
        Assert.Equal("Choose a restoration project.", glance.PrimaryNextAction);
    }

    [Fact]
    public void TheNamedAcceptanceScenarioStillPassesThroughTheRealPath()
    {
        // The acceptance scenario this change must not disturb (tasks 4.3): a real session
        // that queues restoration work still answers the glance.
        var content = Region1Catalog.Create();
        var session = NewSession();
        session.StartNewGame(7UL);

        var home = session.GetHome();
        if (home.ActiveProjectId == null && home.Queued.Count == 0)
            foreach (var projectId in content.Projects.Select(p => p.Id.Value))
                if (session.EnqueueProject(projectId).IsSuccess)
                    break;

        var glance = session.GetGlance();
        Assert.False(string.IsNullOrWhiteSpace(glance.AttentionSentence));
    }

    // 4.4 -------------------------------------------------------------------

    [Fact]
    public void AnActiveProjectIsNamedByTitleAndProgress()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);

        var glance = session.GetGlance();
        var home = session.GetHome();

        Assert.Contains(home.ActiveProjectTitleKey!, glance.RestoringNowSentence);
        Assert.Contains($"{home.ActiveProjectInvested} of {home.ActiveProjectCost}", glance.RestoringNowSentence);
        Assert.DoesNotContain(TestSessions.EntryProjectId, glance.RestoringNowSentence);
    }

    [Fact]
    public void AQueuedProjectIsNamedAsWhatIsNext()
    {
        var glance = GlanceFor(active: false);

        Assert.Contains("Next up is ", glance.RestoringNowSentence);
        Assert.DoesNotContain("proj.", glance.RestoringNowSentence);
    }

    [Fact]
    public void ACompletedRegionDescribesTheAuthoredPostCompletionProgram()
    {
        var glance = GlanceFor(
            active: false,
            queued: false,
            reason: HomeAttentionReason.RegionComplete,
            postCompletionProgram: new PostCompletionProgramDefinition(
                "care.evergreen-tending", "Evergreen Tending", 250L, "proj.valley-closure-milestone"));

        Assert.Contains("Evergreen Tending continues", glance.RestoringNowSentence);
        Assert.DoesNotContain("care.evergreen-tending", glance.RestoringNowSentence);
    }

    // 4.5 -------------------------------------------------------------------

    [Theory]
    [InlineData(ActivityPlayerStatus.ConnectedCurrent, "Your movement is connected.")]
    [InlineData(ActivityPlayerStatus.PermissionNeeded,
        "Connect movement to restore this world. Only walking activity is needed, and you can decide later.")]
    [InlineData(ActivityPlayerStatus.PermissionDenied,
        "Movement access is off. Your progress is still here, and you can keep looking around.")]
    [InlineData(ActivityPlayerStatus.SourceUnavailable,
        "Movement cannot be refreshed on this device. Your progress is still here.")]
    [InlineData(ActivityPlayerStatus.WaitingForFirstData,
        "Connected. The world is waiting for your first walk.")]
    [InlineData(ActivityPlayerStatus.RefreshTemporarilyFailed,
        "Movement could not be refreshed just now. Nothing was lost.")]
    public void EveryActivityStatusHasItsSentence(ActivityPlayerStatus status, string expected)
    {
        var glance = GlanceFor(activityStatus: status);
        Assert.Equal(expected, glance.ActivitySentence);
    }

    [Fact]
    public void PermissionDenialLeavesTheThreeAnswersReadable()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        _port.Permission = ActivityPermissionState.Denied;

        var glance = session.GetGlance();

        Assert.Equal(
            "Movement access is off. Your progress is still here, and you can keep looking around.",
            glance.ActivitySentence);
        Assert.False(string.IsNullOrWhiteSpace(glance.WhatChangedSentence));
        Assert.False(string.IsNullOrWhiteSpace(glance.RestoringNowSentence));
        Assert.False(string.IsNullOrWhiteSpace(glance.AttentionSentence));
    }

    [Fact]
    public void APortlessSessionAnswersSourceUnavailableInsteadOfThrowing()
    {
        var session = NewPortlessSession();
        session.StartNewGame(7UL);

        var glance = session.GetGlance();

        Assert.Equal(
            "Movement cannot be refreshed on this device. Your progress is still here.",
            glance.ActivitySentence);
    }

    // 4.6 -------------------------------------------------------------------

    [Theory]
    [InlineData(OnboardingNextAction.ExplainPremise, "Your movement restores this world.")]
    [InlineData(OnboardingNextAction.OfferActivityConnection,
        "Connect movement to restore this world. Only walking activity is needed, and you can decide later.")]
    [InlineData(OnboardingNextAction.ChooseFirstProject, "Choose the first thing to restore.")]
    [InlineData(OnboardingNextAction.DemonstrateProgression, "Progress can continue while you are away.")]
    [InlineData(OnboardingNextAction.ShowExitMessage, "You do not need to keep this open.")]
    public void EveryOnboardingStepHasItsSentence(OnboardingNextAction step, string expected)
    {
        var glance = GlanceFor(onboardingStep: step);
        Assert.Equal(expected, glance.OnboardingSentence);
    }

    [Fact]
    public void TheWorldBaselineStepPointsAtADamagedLandmark()
    {
        var glance = GlanceFor(onboardingStep: OnboardingNextAction.ShowWorldBaseline);

        Assert.Equal("Old Trailhead is still damaged.", glance.OnboardingSentence);
    }

    [Fact]
    public void DenialOnTheConnectionStepStillReadsItAndKeepsNavigable()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        _port.Permission = ActivityPermissionState.Denied;
        // Stage WorldBaseline is the state whose NEXT action is OfferActivityConnection, i.e.
        // the player is standing on the connection step right now.
        session.AdvanceOnboarding(OnboardingStage.WorldBaseline);

        var glance = session.GetGlance();

        Assert.Equal(
            "Connect movement to restore this world. Only walking activity is needed, and you can decide later.",
            glance.OnboardingSentence);
        Assert.False(session.GetOnboarding().IsComplete);
        Assert.True(session.GetOnboarding().NavigableDespitePermissionDenied);
        Assert.False(string.IsNullOrWhiteSpace(glance.RestoringNowSentence));
    }

    [Fact]
    public void CompletedOnboardingAddsNoTourSentence()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        // Complete is canonical-gated: it requires a first project chosen through the real
        // project operations, so the test takes that path rather than asserting past it.
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        Assert.True(session.AdvanceOnboarding(OnboardingStage.Complete).IsSuccess);

        Assert.True(session.GetOnboarding().IsComplete);
        Assert.Null(session.GetGlance().OnboardingSentence);
    }

    // 4.7 -------------------------------------------------------------------

    [Fact]
    public void AHistoricalLandmarkSentenceSurvivesAGlance()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        // The stored historical fixture, composed the way an older build wrote it.
        var stored = new PendingReturnSummaryState
        {
            PrimaryNextAction = "Queue the next restoration project.",
            GeneratedAtUtc = _clock.UtcNow,
        };
        stored.Items.Add(new PendingSummaryItemState
        {
            Kind = SummaryItemKind.Transformation,
            Text = "Old Trailhead has reached Stabilized.",
            FirstCommittedAtUtc = _clock.UtcNow,
        });
        WritePendingSummary(stored);

        // A session that boots from the rewritten save, which is the point of the scenario.
        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        var glance = reloaded.GetGlance();

        // The durable row is untouched...
        var after = reloaded.GetPendingReturnSummary();
        Assert.NotNull(after);
        Assert.Equal("Old Trailhead has reached Stabilized.", after!.Items[0].Text);
        // ...while the glance re-renders it in the phrase form without echoing the enum.
        Assert.Equal("Old Trailhead is safe to approach.", glance.WhatChangedSentence);
        Assert.DoesNotContain("Stabilized", glance.WhatChangedSentence);
        Assert.DoesNotContain("has reached", glance.WhatChangedSentence);
    }

    // 4.8 -------------------------------------------------------------------

    [Fact]
    public void AHostileExceptionMessageNeverBecomesGlanceCopy()
    {
        const string Marker = "rec-9f27c1a4-3b8d-42e1-9c77";
        _port.TechnicalDetail = "adapter dump: " + Marker;

        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.Throws<IOException>(() => session.IngestFromSource(
            new ThrowingRecordSource(new IOException("provider " + Marker + " died")),
            TestSessions.T0, TestSessions.T0.AddHours(1)));

        var first = Describe(session.GetGlance());
        Assert.DoesNotContain(Marker, first);
        Assert.DoesNotContain("IOException", first);

        // Reading twice is a pure function of the same inputs.
        Assert.Equal(first, Describe(session.GetGlance()));
    }

    [Fact]
    public void ReadingAGlanceChangesNothing()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);

        var homeBefore = session.GetHome();
        var glanceBefore = Describe(session.GetGlance());

        session.GetGlance();

        Assert.Equal(homeBefore.Vitality, session.GetHome().Vitality);
        Assert.Equal(glanceBefore, Describe(session.GetGlance()));
        Assert.Equal(homeBefore.CompletedProjects, session.GetHome().CompletedProjects);
        // The summary is still pending: reading a glance never acknowledges it.
        Assert.NotNull(session.GetPendingReturnSummary());
    }

    // ------------------------------------------------------------------ helpers

    private GlanceReadModel GlanceFor(
        PendingSummaryItemState? pendingItem = null,
        PendingSummaryItemState? extraCurrentItem = null,
        PendingSummaryItemState? secondExtraCurrentItem = null,
        HomeAttentionReason reason = HomeAttentionReason.None,
        bool active = false,
        bool queued = true,
        PostCompletionProgramDefinition? postCompletionProgram = null,
        ActivityPlayerStatus activityStatus = ActivityPlayerStatus.ConnectedCurrent,
        OnboardingNextAction onboardingStep = OnboardingNextAction.ExplainPremise)
    {
        var items = new List<ReturnSummaryReadModel.ItemRow>();
        if (pendingItem != null)
            items.Add(new ReturnSummaryReadModel.ItemRow(
                pendingItem.Kind, pendingItem.Text, TestSessions.T0, isCurrent: true));
        if (extraCurrentItem != null)
            items.Add(new ReturnSummaryReadModel.ItemRow(
                extraCurrentItem.Kind, extraCurrentItem.Text, TestSessions.T0, isCurrent: true));
        if (secondExtraCurrentItem != null)
            items.Add(new ReturnSummaryReadModel.ItemRow(
                secondExtraCurrentItem.Kind, secondExtraCurrentItem.Text, TestSessions.T0, isCurrent: true));

        var summary = new ReturnSummaryReadModel(
            items,
            reason == HomeAttentionReason.PendingReturnSummary
                ? "Queue the next restoration project."
                : null,
            TestSessions.T0);

        var home = new HomeReadModel(
            regionTitleKey: "Millbrook Valley",
            vitality: active ? 40L : 900L,
            materials: 0L,
            knowledge: 0L,
            activeProjectId: active ? TestSessions.EntryProjectId : null,
            activeProjectTitleKey: active ? "Clear the Trailhead" : null,
            activeProjectInvested: active ? 40L : 0L,
            activeProjectCost: active ? 200L : 0L,
            queued: active || !queued
                ? Array.Empty<HomeReadModel.QueuedRow>()
                : new[] { new HomeReadModel.QueuedRow("proj.restore-the-well", "Restore the Well") },
            completedProjects: 0,
            totalProjects: 20,
            landmarks: new[]
            {
                new HomeReadModel.LandmarkRow("lm.trailhead", "Old Trailhead", RestorationStage.Ruined),
                new HomeReadModel.LandmarkRow("lm.mill-wheel", "Mill Wheel", RestorationStage.Functional),
            },
            autoAdvance: true,
            hasPendingSummary: items.Count > 0,
            primaryNextAction: reason == HomeAttentionReason.PendingReturnSummary
                ? "Queue the next restoration project."
                : null,
            requiresAttention: reason is HomeAttentionReason.PendingReturnSummary
                or HomeAttentionReason.QueueEmptyWithBankedVitality,
            attentionReason: reason,
            bankedVitality: active ? 0L : 900L,
            regionComplete: reason == HomeAttentionReason.RegionComplete,
            postCompletionCareInvested: 60L,
            postCompletionCareCostPerCycle: 250L);

        var onboarding = new OnboardingReadModel(
            onboardingStep == OnboardingNextAction.None ? OnboardingStage.Complete : OnboardingStage.ActivityConnection,
            onboardingStep,
            firstProjectChosen: false,
            activityStep: ActivityStepFor(activityStatus),
            navigableDespitePermissionDenied: activityStatus == ActivityPlayerStatus.PermissionDenied);

        var status = new ActivityStatusReadModel(
            activityStatus,
            ActivityRecommendedAction.None,
            permissionGranted: activityStatus is ActivityPlayerStatus.ConnectedCurrent
                or ActivityPlayerStatus.WaitingForFirstData,
            partiallyGranted: false,
            hasProcessedAnyRecord: activityStatus == ActivityPlayerStatus.ConnectedCurrent,
            lastOutcome: IngestionOutcomeKind.Succeeded,
            lastBatchVitalityCredited: 0L,
            lastProcessedAtUtc: null,
            lastSuccessfulRefreshUtc: null,
            lastAttemptUtc: null);

        return GlanceProjector.Project(
            home, items.Count == 0 ? null : summary, onboarding, status,
            Region1Catalog.Create(), postCompletionProgram);
    }

    /// <summary>Like <see cref="GlanceFor"/> but with an already-known pending summary row.</summary>
    private GlanceReadModel GlanceForPendingWithReason(HomeAttentionReason reason) =>
        GlanceFor(
            pendingItem: new PendingSummaryItemState
            {
                Kind = SummaryItemKind.Transformation,
                Text = "Old Trailhead is safe to approach.",
                FirstCommittedAtUtc = TestSessions.T0,
            },
            reason: reason);

    private static OnboardingActivityStepState ActivityStepFor(ActivityPlayerStatus status) =>
        status switch
        {
            ActivityPlayerStatus.PermissionDenied => OnboardingActivityStepState.Denied,
            ActivityPlayerStatus.PermissionNeeded => OnboardingActivityStepState.NotYetRequested,
            ActivityPlayerStatus.SourceUnavailable => OnboardingActivityStepState.SourceUnavailable,
            _ => OnboardingActivityStepState.Granted,
        };

    /// <summary>
    /// Writes a durable pending summary through the real codec and save store, so the
    /// historical-text scenario starts from a save that an older build actually wrote.
    /// </summary>
    private void WritePendingSummary(PendingReturnSummaryState state)
    {
        var store = new AtomicFileSaveStore(_temp.Path);
        var codec = TestSessions.NewCodec();

        var read = store.ReadPrimary();
        Assert.Equal(SaveReadOutcome.Success, read.Outcome);

        var decoded = codec.Decode(read.EnvelopeBytes!);
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        Assert.NotNull(decoded.State);

        decoded.State!.PendingReturnSummary = state;
        store.WriteAtomic(codec.Encode(decoded.State!, _clock.UtcNow));
    }

    private string SavePath => Path.Combine(_temp.Path, "save.json");

    /// <summary>Flattened sentence view: the strongest no-leak and idempotence assertion.</summary>
    private static string Describe(GlanceReadModel glance)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(glance.WhatChangedSentence).Append('|');
        builder.Append(glance.FurtherChangesSentence).Append('|');
        builder.Append(glance.RestoringNowSentence).Append('|');
        builder.Append(glance.AttentionSentence).Append('|');
        builder.Append(glance.PrimaryNextAction).Append('|');
        builder.Append(glance.ActivitySentence).Append('|');
        builder.Append(glance.OnboardingSentence);
        return builder.ToString();
    }
}
