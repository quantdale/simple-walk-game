using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using WalkGame.Infrastructure.Persistence;
using ActivityCategory = WalkGame.Domain.Activity.ActivityCategory;
using ActivityUnits = WalkGame.Domain.Activity.ActivityUnits;
using ActivityValidationStatus = WalkGame.Domain.Activity.ActivityValidationStatus;
using CodecStatus = WalkGame.Application.Persistence.CodecStatus;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-063 support diagnostics: why a player's activity was not credited survives a restart,
/// and the source's health is classified from durable evidence instead of a watermark age that
/// floors to zero for anything under a day.
/// </summary>
public sealed class SupportDiagnosticsTests : IDisposable
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(T0);
    private readonly FakeActivityConnectionPort _port = new();

    public void Dispose() => _temp.Dispose();

    private GameSession NewSession() =>
        TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), _port);

    private static NormalizedActivityRecord Record(
        DateTimeOffset startUtc, DateTimeOffset endUtc, long steps, string? id = null, int revision = 1,
        bool isDeletion = false) =>
        new("dev.test", id, ActivityCategory.Walking, ActivityUnits.Steps, steps,
            startUtc, endUtc, revision, isDeletion);

    // ------------------------------------------------------------------
    // 1. The per-reason breakdown is durable and survives restarts.
    // ------------------------------------------------------------------

    [Fact]
    public void RejectionBreakdown_SurvivesARestart_WithTheReasonLost()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        // A record far outside the acceptance window, plus a zero-quantity record: one is a
        // product-relevant loss, the other is ordinary source noise, and before D-063 both
        // collapsed into a single aggregate "Rejected" count.
        session.IngestActivityBatch(new[]
        {
            Record(T0.AddDays(-90), T0.AddDays(-90).AddHours(2), 9000L, "old.backlog"),
            Record(T0.AddHours(-2), T0, 0L, "zero.quantity"),
        });

        var before = session.GetDiagnostics().LastIngestion!;
        Assert.NotNull(before.RejectionCountsByReason);
        Assert.Equal(2, before.Rejected);
        Assert.Equal(1, before.RejectionCountsByReason![ActivityValidationStatus.OutsideHorizon]);
        Assert.Equal(1, before.RejectionCountsByReason[ActivityValidationStatus.ZeroQuantity]);

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        var after = reloaded.GetDiagnostics().LastIngestion!;
        Assert.NotNull(after.RejectionCountsByReason);
        Assert.Equal(1, after.RejectionCountsByReason![ActivityValidationStatus.OutsideHorizon]);
        Assert.Equal(1, after.RejectionCountsByReason[ActivityValidationStatus.ZeroQuantity]);
        Assert.Equal(after.Rejected, after.RejectionCountsByReason.Values.Sum());
        Assert.Empty(GameStateValidator.Validate(Decode(), Region1Catalog.Create()));
    }

    [Fact]
    public void CleanBatch_RecordsAnEmptyBreakdown_WhichIsDistinguishableFromNotRecorded()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.IngestActivityBatch(new[] { Record(T0.AddHours(-2), T0, 6000L, "clean") });

        var clean = session.GetDiagnostics().LastIngestion!;
        Assert.NotNull(clean.RejectionCountsByReason);
        Assert.Empty(clean.RejectionCountsByReason!);
        Assert.Equal(0, clean.Rejected);

        // A save that predates the field decodes as "not recorded" rather than "nothing
        // rejected": an older build's silence is not evidence of a clean batch.
        var legacy = Decode();
        legacy.LastIngestionOutcome!.RejectionCountsByReason = null;
        Persist(legacy);
        Assert.Empty(GameStateValidator.Validate(Decode(), Region1Catalog.Create()));

        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        var row = reloaded.GetDiagnostics().LastIngestion!;
        Assert.Null(row.RejectionCountsByReason);
    }

    [Fact]
    public void Breakdown_IsBoundedByTheReasonSet_RegardlessOfBatchSize()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        var records = new List<NormalizedActivityRecord>();
        for (int i = 0; i < 200; i++)
            records.Add(Record(T0.AddHours(-3), T0, 0L, "zero." + i));

        session.IngestActivityBatch(records);

        var row = session.GetDiagnostics().LastIngestion!;
        Assert.Equal(200, row.Rejected);
        Assert.Single(row.RejectionCountsByReason!);
        Assert.Equal(200, row.RejectionCountsByReason![ActivityValidationStatus.ZeroQuantity]);

        // The map is bounded by the reason set, not by the batch: 9 statuses at most.
        Assert.True(row.RejectionCountsByReason.Count <= Enum.GetValues<ActivityValidationStatus>().Length);
    }

    [Fact]
    public void Dispositions_TheBatchResultAlreadyComputed_AreNowDurable()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        // A first delivery, then a DELETION MARKER carrying a revision that is not newer than
        // the trusted one (a stale redelivery of a removal) plus a quantity above the bounded
        // ceiling: both are dispositions the batch result already computed and the durable
        // outcome used to drop.
        session.IngestActivityBatch(new[] { Record(T0.AddHours(-2), T0, 6000L, "stale.seed", revision: 3) });
        session.IngestActivityBatch(new[]
        {
            Record(T0.AddHours(-2), T0, 6000L, "stale.seed", revision: 2, isDeletion: true),
            Record(T0.AddHours(-2), T0, 1_000_000L, "clamped.big"),
        });

        var row = session.GetDiagnostics().LastIngestion!;
        Assert.True(row.QuantityClamped >= 1, "bounded-quantity handling is durable evidence");
        Assert.Equal(1, row.StaleRevisionsIgnored);
        // Every disposition is now accounted for, not just the first three.
        Assert.Equal(
            row.TotalReceived,
            row.Accepted + row.Rejected + row.DuplicatesIgnored + row.StaleRevisionsIgnored);
        Assert.Empty(GameStateValidator.Validate(Decode(), Region1Catalog.Create()));
    }

    // ------------------------------------------------------------------
    // 2. Classified source health.
    // ------------------------------------------------------------------

    [Fact]
    public void ReconciliationState_IsNeverAttempted_Current_Stale_AndFailedSinceLastSuccess()
    {
        var session = NewSession();
        session.StartNewGame(7UL);

        // Never attempted: no fetch has run.
        Assert.Equal(ReconciliationState.NeverAttempted, session.GetDiagnostics().ReconciliationState);

        // Current: a recent successful fetch.
        session.IngestActivityBatch(new[] { Record(T0.AddHours(-2), T0, 6000L, "fresh") });
        Assert.Equal(ReconciliationState.Current, session.GetDiagnostics().ReconciliationState);

        // Stale: the newest durable evidence is older than the threshold.
        _clock.Set(T0.AddDays(10));
        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Equal(ReconciliationState.Stale, reloaded.GetDiagnostics().ReconciliationState);

        // Failed since last success: a fetch throws after the last good one.
        var failing = new ThrowingRecordSource(new IOException("source unavailable"));
        Assert.Throws<IOException>(() => reloaded.IngestFromSource(
            failing, T0.AddDays(10), T0.AddDays(11)));

        var afterFailure = NewSession();
        Assert.Equal(StartStatus.Loaded, afterFailure.Continue().Status);
        var diagnostics = afterFailure.GetDiagnostics();
        Assert.Equal(ReconciliationState.FailedSinceLastSuccess, diagnostics.ReconciliationState);
        Assert.NotNull(diagnostics.LastIngestion!.ErrorCategory);
        Assert.Equal(60L, diagnostics.LifetimeVitalityCredited);
        Assert.True(diagnostics.ReconciliationPending);
    }

    [Fact]
    public void BackwardClock_CannotMakeAStaleSourceLookCurrent()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.IngestActivityBatch(new[] { Record(T0.AddHours(-2), T0, 6000L, "stamped") });

        // The device clock jumps BACKWARD behind the durable evidence. The classification must
        // fail toward stale rather than reassuring the player that everything is fine.
        _clock.Set(T0.AddDays(-30));
        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        Assert.Equal(ReconciliationState.Stale, reloaded.GetDiagnostics().ReconciliationState);
    }

    [Fact]
    public void WatermarkAge_KeepsItsWholeDayContract()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.IngestActivityBatch(new[] { Record(T0.AddHours(-2), T0, 6000L, "aged") });

        _clock.Set(T0.AddDays(30));
        var reloaded = NewSession();
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);

        // M5H1ShellAcceptanceTests.Scenario05 depends on this contract: whole days only.
        var age = reloaded.GetDiagnostics().CheckpointWatermarkAgeDays;
        Assert.NotNull(age);
        Assert.InRange(age!.Value, 28, 31);
    }

    // ------------------------------------------------------------------
    // 3. Validator.
    // ------------------------------------------------------------------

    [Fact]
    public void InconsistentBreakdown_IsAStateViolation()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.IngestActivityBatch(new[]
        {
            Record(T0.AddHours(-2), T0, 6000L, "valid"),
            Record(T0.AddHours(-2), T0, 0L, "rejected"),
        });

        var state = Decode();
        state.LastIngestionOutcome!.RejectionCountsByReason!.Clear(); // no longer sums to Rejected
        Persist(state);

        Assert.Contains(GameStateValidator.Validate(Decode(), Region1Catalog.Create()),
            v => v.Contains("breakdown sums to"));
    }

    [Fact]
    public void AcceptedAboveTotalReceived_IsAStateViolation()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.IngestActivityBatch(new[] { Record(T0.AddHours(-2), T0, 6000L, "valid") });

        var state = Decode();
        state.LastIngestionOutcome!.Accepted = state.LastIngestionOutcome!.TotalReceived + 1;
        Persist(state);

        Assert.Contains(GameStateValidator.Validate(Decode(), Region1Catalog.Create()),
            v => v.Contains("exceeds total received"));
    }

    // ------------------------------------------------------------------
    // 4. Privacy: classified reasons only.
    // ------------------------------------------------------------------

    [Fact]
    public void Diagnostics_NeverCarryProviderText()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.IngestActivityBatch(new[]
        {
            Record(T0.AddHours(-2), T0, 6000L, "provider-record-id-SECRET"),
            Record(T0.AddDays(-90), T0.AddDays(-90).AddHours(2), 9000L, "another-secret-id"),
        });

        var diagnostics = session.GetDiagnostics();
        var row = diagnostics.LastIngestion!;

        // Classified reasons only: the map's keys are enum values, never record identifiers.
        Assert.All(row.RejectionCountsByReason!.Keys, k => Enum.IsDefined(typeof(ActivityValidationStatus), k));
        Assert.DoesNotContain("provider-record-id-SECRET", Describe(diagnostics));
        Assert.DoesNotContain("another-secret-id", Describe(diagnostics));
    }

    private static string Describe(object graph) =>
        string.Join("|", Walk(graph));

    private static IEnumerable<string> Walk(object? node)
    {
        if (node == null)
            yield break;

        Type type = node.GetType();
        if (node is string text)
        {
            yield return text;
            yield break;
        }

        if (type.IsPrimitive || node is DateTimeOffset || node is TimeSpan || node is Guid)
        {
            yield return node.ToString() ?? string.Empty;
            yield break;
        }

        if (node is System.Collections.IDictionary dictionary)
        {
            foreach (var key in dictionary.Keys)
                foreach (var item in Walk(key))
                    yield return item;
            foreach (var value in dictionary.Values)
                foreach (var item in Walk(value))
                    yield return item;
            yield break;
        }

        if (node is System.Collections.IEnumerable sequence)
        {
            foreach (var element in sequence)
                foreach (var item in Walk(element))
                    yield return item;
            yield break;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
                continue;
            object? value;
            try
            {
                value = property.GetValue(node);
            }
            catch (TargetInvocationException)
            {
                continue;
            }
            foreach (var item in Walk(value))
                yield return item;
        }
    }

    private GameState Decode()
    {
        var decoded = TestSessions.NewCodec()
            .Decode(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }

    private void Persist(GameState state) =>
        new AtomicFileSaveStore(_temp.Path)
            .WriteAtomic(TestSessions.NewCodec().Encode(state, _clock.UtcNow));
}
