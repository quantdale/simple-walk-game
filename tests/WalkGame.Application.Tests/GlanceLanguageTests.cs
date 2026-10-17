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

        Assert.Contains("Next up: ", glance.RestoringNowSentence); // D-162 label-first form
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

    [Fact]
    public void ReadingAGlancePerformsNoSaveOperation()
    {
        // The CLI boots through the shared boot flow, which persists before presenting (as
        // ack/dump/validate already did). What THIS surface must not do is add a save of its own.
        var created = NewSession();
        Assert.Equal(StartStatus.NewGameCreated, created.StartNewGame(7UL).Status);

        // A pending summary MUST exist, or the durable row a glance-time save would disturb is
        // absent and this test passes vacuously. Exposed by mutation testing: without a summary
        // an earlier revision survived a GetGlance that persisted.
        WritePendingSummary(NewCurrentSummary());

        var session = NewSession();
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);
        Assert.True(session.GetPendingReturnSummary()!.HasCurrentItems);

        // Snapshot AFTER boot, before any glance read: this is the persisted baseline the CLI
        // would leave for a verb that boots and reads nothing.
        byte[] beforeGlance = File.ReadAllBytes(SavePath);

        session.GetGlance();

        byte[] afterGlance = File.ReadAllBytes(SavePath);

        // A save inside GetGlance would refresh the storedAt stamp and re-encode the payload.
        Assert.Equal(beforeGlance, afterGlance);
    }
    private static PendingReturnSummaryState NewCurrentSummary()
    {
        var state = new PendingReturnSummaryState { GeneratedAtUtc = TestSessions.T0 };
        state.Items.Add(new PendingSummaryItemState
        {
            Kind = SummaryItemKind.Transformation,
            Text = "Old Trailhead is safe to approach.",
            FirstCommittedAtUtc = TestSessions.T0,
        });
        return state;
    }

    /// <summary>
    /// The boot persist is shared and pre-existing: a session that boots and one that boots
    /// and then reads a glance leave the same canonical state (only the persisted wall-clock
    /// stamp differs), so the glance adds nothing to the save.
    /// </summary>
    [Fact]
    public void ReadingAGlanceLeavesTheSameStateABootThatReadsNothingLeaves()
    {
        // The amended spec scenario, tested for real: a boot that reads a glance must leave the
        // same canonical state as a boot that read nothing (what dump / alidate now do).
        //
        // Both sides MUST carry the same pending summary, or neither side has any durable row
        // for a glance-time save to disturb and the comparison passes vacuously. An earlier
        // revision compared two summary-free saves and was caught vacuous by mutation testing.
        var plain = NewTemp();
        var glanced = NewTemp();
        try
        {
            BootIntoWithSummary(plain);
            BootIntoWithSummary(glanced);

            var glanceSession = TestSessions.Create(glanced, new ManualClock(TestSessions.T0),
                new LocalPreferencesStore(glanced), null);
            Assert.Equal(StartStatus.Loaded, glanceSession.Continue().Status);
            glanceSession.GetGlance();

            Assert.Equal(CanonicalState(plain), CanonicalState(glanced));
            // The summary must still be there on the glanced side: a glance never acknowledges.
            Assert.Contains("pendingReturnSummary", CanonicalState(glanced));
        }
        finally
        {
            Directory.Delete(plain, true);
            Directory.Delete(glanced, true);
        }
    }

    /// <summary>Creates a profile, attaches a pending summary, and boots the way the reading verbs do.</summary>
    private static void BootIntoWithSummary(string dir)
    {
        var session = TestSessions.Create(dir, new ManualClock(TestSessions.T0),
            new LocalPreferencesStore(dir), null);
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);
        WritePendingSummaryInto(dir, NewCurrentSummary());

        var reload = TestSessions.Create(dir, new ManualClock(TestSessions.T0),
            new LocalPreferencesStore(dir), null);
        Assert.Equal(StartStatus.Loaded, reload.Continue().Status);
        Assert.True(reload.GetPendingReturnSummary()!.HasCurrentItems);
    }

    private static void WritePendingSummaryInto(string dir, PendingReturnSummaryState state)
    {
        var codec = TestSessions.NewCodec();
        var store = new AtomicFileSaveStore(dir);
        var read = store.ReadPrimary();
        Assert.Equal(SaveReadOutcome.Success, read.Outcome);

        var decoded = codec.Decode(read.EnvelopeBytes!);
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        Assert.NotNull(decoded.State);

        decoded.State!.PendingReturnSummary = state;
        store.WriteAtomic(codec.Encode(decoded.State!, TestSessions.T0));
    }
    private static string NewTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), "walkgame-glance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Canonical state, field-based, decoded from the real envelope.
    ///
    /// The save is {schemaVersion, savedAtUtc, payloadSha256Base64, payloadBase64} and the
    /// state is BASE64 inside payloadBase64, so probing the envelope's own keys is vacuous.
    /// An earlier revision probed the envelope, another filtered by line on a single-line
    /// payload; both compared empty to empty and passed. This one decodes and probes fields.
    /// </summary>
    private static string CanonicalState(string dir)
    {
        var envelope = File.ReadAllText(Path.Combine(dir, "save.json"));
        var payload = DecodePayload(envelope);

        var probe = new System.Text.StringBuilder();
        foreach (var key in new[]
                 {
                     "createdAtUtc", "schemaVersion", "authoredContentVersion",
                     "processedRecords", "ledger", "pendingReturnSummary",
                     "resources", "queue", "region", "landmarkStages", "regionProjects",
                 })
        {
            payload.TryGetPropertyValue(key, out var value);
            probe.Append(key).Append('=').Append(value?.ToJsonString() ?? "null").Append('|');
        }
        return probe.ToString();
    }

    private static System.Text.Json.Nodes.JsonObject DecodePayload(string envelope)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(envelope)!.AsObject();
        var base64 = node["payloadBase64"]?.ToString();
        Assert.False(string.IsNullOrEmpty(base64), "no payloadBase64 in the envelope.");

        var bytes = Convert.FromBase64String(base64!);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        return System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
    }
    // ------------------------------------------------------------------ helpers

    [Fact]
    public void AFinishedValleyNeverOffersAQueueActionEvenWithACurrentSummary()
    {
        // GetHome() prefers PendingReturnSummary over RegionComplete whenever a summary item
        // is still current, so this is the state a finished region actually arrives in —
        // carrying a summary-derived action for work that no longer exists. The design rule
        // is that the conclusion outranks the stale action.
        var home = RegionCompleteWithCurrentSummary();
        var projected = GlanceProjector.Project(
            home,
            PendingReturnSummaryFor(home),
            CompletedOnboarding(),
            ConnectedStatus(),
            Region1Catalog.Create(),
            new PostCompletionProgramDefinition(
                "care.evergreen-tending", "Evergreen Tending", 250L, "proj.valley-closure-milestone"));

        Assert.False(projected.RequiresAttention);
        Assert.Equal("Millbrook Valley's restoration is complete.", projected.AttentionSentence);
        Assert.Null(projected.PrimaryNextAction);
        Assert.DoesNotContain("Queue", projected.AttentionSentence);
    }

    private static HomeReadModel RegionCompleteWithCurrentSummary() => new HomeReadModel(
        regionTitleKey: "Millbrook Valley",
        vitality: 0L, materials: 0L, knowledge: 0L,
        activeProjectId: null, activeProjectTitleKey: null,
        activeProjectInvested: 0L, activeProjectCost: 0L,
        queued: Array.Empty<HomeReadModel.QueuedRow>(),
        completedProjects: 19, totalProjects: 19,
        landmarks: new[] { new HomeReadModel.LandmarkRow("lm.trailhead", "Old Trailhead", RestorationStage.Restored) },
        autoAdvance: true,
        hasPendingSummary: true,
        primaryNextAction: "Queue the next restoration project.",
        requiresAttention: true,
        attentionReason: HomeAttentionReason.PendingReturnSummary,
        bankedVitality: 0L,
        regionComplete: true,
        postCompletionCareInvested: 60L,
        postCompletionCareCostPerCycle: 250L);

    private static ReturnSummaryReadModel PendingReturnSummaryFor(HomeReadModel home) => new(
        new[]
        {
            new ReturnSummaryReadModel.ItemRow(
                SummaryItemKind.Transformation, "Restore the Well is complete.", TestSessions.T0, isCurrent: true),
        },
        home.PrimaryNextAction,
        TestSessions.T0);

    private static OnboardingReadModel CompletedOnboarding() => new(
        OnboardingStage.Complete, OnboardingNextAction.None, true,
        OnboardingActivityStepState.Granted, false);

    private static ActivityStatusReadModel ConnectedStatus() => new(
        ActivityPlayerStatus.ConnectedCurrent, ActivityRecommendedAction.None,
        permissionGranted: true, partiallyGranted: false, hasProcessedAnyRecord: true,
        lastOutcome: IngestionOutcomeKind.Succeeded, lastBatchVitalityCredited: 60L,
        lastProcessedAtUtc: TestSessions.T0, lastSuccessfulRefreshUtc: TestSessions.T0,
        lastAttemptUtc: TestSessions.T0);

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
