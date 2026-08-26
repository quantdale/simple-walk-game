using System;
using System.IO;
using System.Text;
using WalkGame.Application.Activity;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Ux;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// Workstream G: targeted deterministic hardening around the new M5 state/persistence
/// boundaries — hostile payloads, interrupted writes, rapid transitions, interruption at
/// every durable onboarding step, and leak sweeps. Table-driven over large mock stacks.
/// </summary>
public sealed class M5H1ContractHardeningTests : IDisposable
{
    private const string Marker = "RAW-HEALTH-BLOB-MARKER";
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(TestSessions.T0);

    public void Dispose() => _temp.Dispose();

    private GameSession NewSession(FakeActivityConnectionPort? port = null) =>
        TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), port ?? new FakeActivityConnectionPort());

    private static readonly string[] HostilePayloads =
    {
        "",
        "   ",
        "null",
        "not json at all",
        "{",
        "[]",
        "{\"schemaVersion\":\"one\"}",
        "{\"schemaVersion\":true}",
        "{\"schemaVersion\":1,\"onboardingStage\":\"notARealStage\"}",
        "{\"schemaVersion\":1,\"dailyReminderMinutesOfDay\":99999999}",
        "{\"schemaVersion\":1,\"reducedMotion\":\"yes\"}",
        "{\"schemaVersion\":1,\"hapticsEnabled\":{\"nested\":true}}",
        new string('{', 5000),
        "{\"schemaVersion\":1,\"unknownFutureKey\":{\"deeply\":[\"nested\",1]}}",
    };

    [Theory]
    [MemberData(nameof(HostilePayloadData))]
    public void HostilePreferencesPayloads_NeverThrow_NeverPartial_Interpret(string payload)
    {
        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json"), payload);

        var store = new LocalPreferencesStore(_temp.Path);
        var result = store.Load(); // must not throw

        if (result.Outcome == UxPreferencesLoadOutcome.Success)
            Assert.NotNull(result.State);
        else
            Assert.Null(result.State);

        // The session layer must boot gameplay regardless.
        var session = TestSessions.Create(_temp.Path, _clock, store);
        session.StartNewGame(7UL);
        Assert.True(session.GetOnboarding().CurrentStage >= OnboardingStage.NotStarted);
    }

    public static TheoryData<string> HostilePayloadData() =>
        new TheoryData<string>(HostilePayloads);

    [Fact]
    public void InterruptedPreferenceWrite_TempOnlyNeverBecomesState()
    {
        // Crash between temp write and replace, with NO previous generation.
        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json.tmp"),
            "{\"schemaVersion\":1,\"reducedMotion\":true}");

        var store = new LocalPreferencesStore(_temp.Path);
        Assert.Equal(UxPreferencesLoadOutcome.NotFound, store.Load().Outcome);

        // A later successful write heals the directory and ignores the stale temp.
        store.Save(UxPreferencesState.CreateDefault());
        Assert.Equal(UxPreferencesLoadOutcome.Success, store.Load().Outcome);
    }

    [Fact]
    public void RepeatedIdenticalWrites_ConvergeByteIdentically_UnderChurn()
    {
        var store = new LocalPreferencesStore(_temp.Path);
        var state = UxPreferencesState.CreateDefault();
        state.NotificationsOptIn = true;
        state.DailyReminderMinutesOfDay = 432;

        byte[]? reference = null;
        for (int i = 0; i < 5; i++)
        {
            // Interleave divergent writes between identical ones: final bytes must
            // depend only on the final state, never the path taken.
            var churned = state.Clone();
            churned.ReducedMotion = true;
            churned.OnboardingStage = OnboardingStage.Simulation;
            store.Save(churned);
            store.Save(state.Clone());

            var current = File.ReadAllBytes(Path.Combine(_temp.Path, "ux-preferences.json"));
            reference ??= current;
            Assert.Equal(reference, current);
            Assert.False(current[0] == 0, "empty write");
        }
    }

    [Fact]
    public void RapidStatusTransitionSequences_EndInExactlyTheDocumentedClassification()
    {
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Granted };
        var session = NewSession(port);
        session.StartNewGame(7UL);
        _clock.Set(TestSessions.T0.AddHours(2));

        (ActivityPermissionState p, ActivitySourceAvailability a, ActivityPlayerStatus expect)[] sequence =
        {
            (ActivityPermissionState.NotRequested, ActivitySourceAvailability.Available, ActivityPlayerStatus.PermissionNeeded),
            (ActivityPermissionState.Denied, ActivitySourceAvailability.Available, ActivityPlayerStatus.PermissionDenied),
            (ActivityPermissionState.PartiallyGranted, ActivitySourceAvailability.Available, ActivityPlayerStatus.WaitingForFirstData),
            (ActivityPermissionState.PartiallyGranted, ActivitySourceAvailability.Unsupported, ActivityPlayerStatus.SourceUnavailable),
            (ActivityPermissionState.PartiallyGranted, ActivitySourceAvailability.TemporarilyUnavailable, ActivityPlayerStatus.RefreshTemporarilyFailed),
            (ActivityPermissionState.Revoked, ActivitySourceAvailability.Available, ActivityPlayerStatus.PermissionDenied),
            (ActivityPermissionState.Granted, ActivitySourceAvailability.Available, ActivityPlayerStatus.WaitingForFirstData),
            (ActivityPermissionState.Granted, ActivitySourceAvailability.Available, ActivityPlayerStatus.ConnectedCurrent),
        };

        bool ingested = false;
        foreach (var step in sequence)
        {
            port.Permission = step.p;
            port.Availability = step.a;

            if (!ingested && step.expect == ActivityPlayerStatus.ConnectedCurrent)
            {
                session.IngestFromSource(new StaticRecordSource(
                        M5H1Records.Steps(1000L, TestSessions.T0, TimeSpan.FromHours(1), sourceId: "seq")),
                    TestSessions.T0, TestSessions.T0.AddDays(1));
                ingested = true;
            }

            Assert.Equal(step.expect, session.GetActivityStatus().Status);
        }
    }

    [Fact]
    public void ReadModels_StaySideEffectFree_WithZeroRecords_AndWithMatureHistories()
    {
        byte[] Snapshot() => File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));

        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Denied };
        var before = NewSession(port);
        before.StartNewGame(7UL);
        var zeroRecordBytes = Snapshot();

        SweepAllReadModels(before);

        Assert.Equal(zeroRecordBytes, Snapshot());

        // Mature history variant: months of processed activity then long silence.
        port.Permission = ActivityPermissionState.Granted;
        var mature = NewSession(port);
        mature.StartNewGame(7UL);
        _clock.Set(TestSessions.T0.AddDays(3));
        mature.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(20000L, TestSessions.T0, TimeSpan.FromHours(6), sourceId: "mature")),
            TestSessions.T0, TestSessions.T0.AddDays(1));
        _clock.Set(TestSessions.T0.AddDays(120));
        mature.Continue();

        var matureBytes = Snapshot();
        SweepAllReadModels(mature);
        Assert.Equal(matureBytes, Snapshot());
    }

    private static void SweepAllReadModels(GameSession session)
    {
        for (int i = 0; i < 3; i++)
        {
            _ = session.GetHome();
            _ = session.GetProjects();
            _ = session.GetRegion();
            _ = session.GetDiscoveries();
            _ = session.GetExpeditions();
            _ = session.GetPendingReturnSummary();
            _ = session.GetSettings();
            _ = session.GetOnboarding();
            _ = session.GetActivityStatus();
            _ = session.GetDiagnostics();
        }
    }

    [Fact]
    public void StaleDiagnosticSnapshots_AreFrozen_AndNeverBecomeCanonicalInputs()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.EnqueueProject(TestSessions.EntryProjectId);
        _clock.Set(TestSessions.T0.AddHours(2));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(3000L, TestSessions.T0, TimeSpan.FromHours(2), sourceId: "stale")),
            TestSessions.T0, TestSessions.T0.AddHours(3));

        var stale = session.GetDiagnostics();
        int staleCount = stale.ProcessedRecordCount;
        long staleCredited = stale.LifetimeVitalityCredited;

        // State moves on...
        _clock.Set(TestSessions.T0.AddHours(6));
        session.IngestFromSource(new StaticRecordSource(
                M5H1Records.Steps(1000L, TestSessions.T0.AddHours(4), TimeSpan.FromHours(1), sourceId: "new")),
            TestSessions.T0.AddHours(4), TestSessions.T0.AddHours(7));

        // ...the old snapshot is immutable evidence of its moment, not a live view.
        Assert.Equal(staleCount, stale.ProcessedRecordCount);
        Assert.Equal(staleCredited, stale.LifetimeVitalityCredited);
        Assert.True(session.GetDiagnostics().ProcessedRecordCount > staleCount);

        // Canonical progression equals ledger truth, not any diagnostic aggregate:
        Assert.Equal(40L,
            session.GetHome().Vitality + session.GetHome().ActiveProjectInvested);
    }

    [Fact]
    public void OnboardingInterruption_AtEveryDurableStep_ResumesExactlyThere()
    {
        var stages = new[]
        {
            OnboardingStage.Premise,
            OnboardingStage.WorldBaseline,
            OnboardingStage.ActivityConnection,
            OnboardingStage.FirstProject,
            OnboardingStage.Simulation,
            OnboardingStage.Exit,
        };

        foreach (var stage in stages)
        {
            string dir = Path.Combine(_temp.Path, "stage-" + stage);
            var writer = TestSessions.Create(dir, _clock, new LocalPreferencesStore(dir), new FakeActivityConnectionPort());
            writer.StartNewGame(9UL);
            Assert.True(writer.AdvanceOnboarding(stage).IsSuccess);

            // Hard restart mid-flow.
            var reloaded = TestSessions.Create(dir, _clock, new LocalPreferencesStore(dir), new FakeActivityConnectionPort());
            Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
            Assert.Equal(stage, reloaded.GetOnboarding().CurrentStage);

            // The canonical gate survives restarts too.
            var gateAfterRestart = reloaded.AdvanceOnboarding(OnboardingStage.Complete);
            if (!reloaded.GetOnboarding().FirstProjectChosen)
            {
                Assert.False(gateAfterRestart.IsSuccess);
                Assert.Equal(UxErrorCodes.OnboardingPrerequisite, gateAfterRestart.Error!.Code);
            }

            // And completing through real operations still works after any interruption.
            Assert.True(reloaded.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
            Assert.True(reloaded.AdvanceOnboarding(OnboardingStage.Complete).IsSuccess);
        }
    }

    [Fact]
    public void PreferencesSchemaPolicy_IsExplicit_ForEveryVersionClass()
    {
        var path = Path.Combine(_temp.Path, "ux-preferences.json");
        var store = new LocalPreferencesStore(_temp.Path);

        // v1 with all keys absent-but-framed: success with default merge.
        File.WriteAllText(path, "{\"schemaVersion\":1}");
        Assert.Equal(UxPreferencesLoadOutcome.Success, store.Load().Outcome);

        // v0/pre-history: malformed by policy, not silently upgraded.
        File.WriteAllText(path, "{\"schemaVersion\":0}");
        Assert.Equal(UxPreferencesLoadOutcome.Malformed, store.Load().Outcome);

        // future: never interpreted.
        File.WriteAllText(path, "{\"schemaVersion\":2,\"anything\":true}");
        var future = store.Load();
        Assert.Equal(UxPreferencesLoadOutcome.FutureVersion, future.Outcome);
        Assert.Null(future.State);
    }

    [Fact]
    public void PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads()
    {
        var port = new FakeActivityConnectionPort
        {
            Permission = ActivityPermissionState.Granted,
            TechnicalDetail = "adapter dump: " + Marker,
        };
        var session = NewSession(port);
        session.StartNewGame(7UL);

        Assert.Throws<IOException>(() => session.IngestFromSource(
            new ThrowingRecordSource(new IOException(Marker)),
            TestSessions.T0, TestSessions.T0.AddHours(1)));

        // Player-facing surfaces (status/onboarding/settings/home/projects):
        Assert.DoesNotContain(Marker, Describe(session.GetActivityStatus()));
        Assert.DoesNotContain(Marker, Describe(session.GetOnboarding()));
        Assert.DoesNotContain(Marker, Describe(session.GetSettings()));
        Assert.DoesNotContain(Marker, Describe(session.GetHome()));
        Assert.DoesNotContain(Marker, Describe(session.GetProjects()));
        Assert.DoesNotContain(Marker, Describe(session.GetRegion()));

        // Diagnostics may carry adapter technical detail, but bounded and gated there.
        var diagText = Describe(session.GetDiagnostics());
        Assert.Contains("IOException", diagText); // classification only

        // Even the adapter detail arrives truncated, never as unbounded text.
        Assert.True((session.GetDiagnostics().ConnectionTechnicalDetail?.Length ?? 0) <= 300);
    }

    private static string Describe(object model)
    {
        // Reflection walk over every string-typed property: stronger than ToString().
        var builder = new StringBuilder();
        Walk(model, builder, depth: 0);
        return builder.ToString();

        static void Walk(object? value, StringBuilder sb, int depth)
        {
            if (value == null || depth > 3) return;
            if (value is string s) { sb.Append(s).Append('|'); return; }
            if (value.GetType().IsPrimitive || value is Enum || value is DateTimeOffset or DateTime) return;

            foreach (var property in value.GetType().GetProperties())
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
                try
                {
                    var child = property.GetValue(value);
                    if (child is System.Collections.IEnumerable enumerable and not string)
                        foreach (var item in enumerable) Walk(item, sb, depth + 1);
                    else
                        Walk(child, sb, depth + 1);
                }
                catch (Exception)
                {
                    // Property access failures are contract bugs surfaced elsewhere.
                }
            }
        }
    }
}
