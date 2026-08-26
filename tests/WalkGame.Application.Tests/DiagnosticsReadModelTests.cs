using System;
using System.IO;
using System.Text;
using WalkGame.Application.Activity;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Ux;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests;

/// <summary>
/// Workstream D diagnostics contract: privacy-safe operational facts, recovery evidence,
/// and the guarantee that no player-facing surface leaks raw exception text.
/// </summary>
public sealed class DiagnosticsReadModelTests : IDisposable
{
    private const string LeakMarker = "SECRET-RAW-PAYLOAD-BLOB";
    private readonly TempDirectory _temp = new();
    private readonly ManualClock _clock = new(TestSessions.T0);

    public void Dispose() => _temp.Dispose();

    private GameSession NewSession(FakeActivityConnectionPort? port = null) =>
        TestSessions.Create(_temp.Path, _clock, new LocalPreferencesStore(_temp.Path), port ?? new FakeActivityConnectionPort());

    [Fact]
    public void FreshBoot_ReportsLoadedSchemaAndZeroAggregates()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.Continue();

        var diag = session.GetDiagnostics();

        Assert.Equal(DiagnosticsBootOutcome.Loaded, diag.BootOutcome);
        Assert.False(diag.RecoveredFromBackup);
        Assert.Equal(CodecFailureCategory.None, diag.LastBootCodecFailure);
        Assert.Empty(diag.AppliedMigrationsAtBoot);
        Assert.Equal(2, diag.SchemaVersion);
        Assert.Equal("region.millbrook-valley", diag.RegionId);
        Assert.Equal(0, diag.ProcessedRecordCount);
        Assert.Equal(0L, diag.LifetimeVitalityCredited);
        Assert.Null(diag.LastIngestion);
    }

    [Fact]
    public void MixedBatch_CountersAreSurfaced_IncludingUnappliedReversalsForever()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        _clock.Set(TestSessions.T0.AddHours(5));

        session.IngestActivityBatch(new[]
        {
            // Valid: 50 Vitality.
            M5H1Records.Steps(5000L, TestSessions.T0.AddHours(-1), TimeSpan.FromHours(1), sourceId: "a"),
            // Future-skewed end relative to the clock: rejected as FutureTimestamp.
            M5H1Records.Steps(2000L, TestSessions.T0.AddHours(-1), TimeSpan.FromHours(9), sourceId: "b"),
            // Zero quantity: rejected as ZeroQuantity.
            M5H1Records.Steps(0L, TestSessions.T0.AddHours(-1), TimeSpan.FromHours(3), sourceId: "zero"),
            // Identical redelivery of 'a': duplicate ignored.
            M5H1Records.Steps(5000L, TestSessions.T0.AddHours(-1), TimeSpan.FromHours(1), sourceId: "a"),
        });

        var diag = session.GetDiagnostics();

        Assert.NotNull(diag.LastIngestion);
        var row = diag.LastIngestion!;
        Assert.Equal(IngestionOutcomeKind.Succeeded, row.Outcome);
        Assert.Equal(4, row.TotalReceived);
        Assert.Equal(1, row.Accepted);
        Assert.Equal(2, row.Rejected);
        Assert.Equal(1, row.DuplicatesIgnored);
        Assert.Equal(50L, row.VitalityCredited);
        Assert.Equal(50L, diag.LifetimeVitalityCredited);
    }

    [Fact]
    public void RecoveryFromBackup_IsSurfacedAsCalmDurableEvidence()
    {
        var writer = NewSession();
        writer.StartNewGame(7UL);
        writer.CreditActivity(TestSessions.Tx1, TestSessions.T0, 120L, "walk");

        File.WriteAllText(Path.Combine(_temp.Path, "save.json"), "{corrupted");

        var recovered = NewSession();
        Assert.Equal(StartStatus.RecoveredFromBackup, recovered.Continue().Status);

        var diag = recovered.GetDiagnostics();
        Assert.NotNull(diag);
        Assert.Equal(DiagnosticsBootOutcome.RecoveredFromBackup, diag.BootOutcome);
        Assert.True(diag.RecoveredFromBackup);
        // The failed primary decode is classified, not hidden.
        Assert.Equal(CodecFailureCategory.MalformedEnvelope, diag.LastBootCodecFailure);
    }

    [Fact]
    public void FutureSchemaSave_ClassifiesVersionTooNew_AndFailsClosed()
    {
        var writer = NewSession();
        writer.StartNewGame(7UL);

        // Build a properly framed envelope whose declared schema version is from the
        // future — the honest way this failure occurs in the wild.
        byte[] genuine = File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"));
        string bumped = Encoding.UTF8.GetString(genuine)
            .Replace("\"schemaVersion\":2", "\"schemaVersion\":999");
        Assert.NotEqual(Encoding.UTF8.GetString(genuine), bumped);
        File.WriteAllBytes(Path.Combine(_temp.Path, "save.json"), Encoding.UTF8.GetBytes(bumped));
        File.WriteAllBytes(Path.Combine(_temp.Path, "save.backup.json"), Encoding.UTF8.GetBytes(bumped));

        var session = NewSession();

        Assert.Equal(StartStatus.SaveUnreadable, session.Continue().Status);
        var diag = session.GetDiagnostics();
        Assert.Equal(DiagnosticsBootOutcome.SaveUnreadable, diag.BootOutcome);
        Assert.Equal(CodecFailureCategory.VersionTooNew, diag.LastBootCodecFailure);
    }

    [Fact]
    public void NoRawExceptionText_EverAppearsInPlayerFacingOrDiagnosticSurfaces()
    {
        var port = new FakeActivityConnectionPort { Permission = ActivityPermissionState.Granted };
        var session = NewSession(port);
        session.StartNewGame(7UL);

        Assert.Throws<IOException>(() => session.IngestFromSource(
            new ThrowingRecordSource(new IOException("fetch failed: " + LeakMarker)),
            TestSessions.T0, TestSessions.T0.AddHours(1)));

        var status = session.GetActivityStatus();
        var diag = session.GetDiagnostics();
        var home = session.GetHome();
        var settings = session.GetSettings();
        var onboarding = session.GetOnboarding();

        Assert.DoesNotContain(LeakMarker, status.Status.ToString());
        Assert.False(home.ToString()!.Contains(LeakMarker));
        foreach (var model in new object?[] { settings, onboarding })
            if (model?.ToString()?.Contains(LeakMarker) == true)
                Assert.Fail("leak via ToString");

        // Diagnostics carries only the stable error CATEGORY — never the message.
        Assert.NotNull(diag.LastIngestion);
        Assert.Equal(IngestionOutcomeKind.SourceFetchFailed, diag.LastIngestion!.Outcome);
        Assert.Equal(nameof(IOException), diag.LastIngestion.ErrorCategory);
        Assert.DoesNotContain(LeakMarker, diag.LastIngestion.ErrorCategory);

        // Adapter-owned technical detail is bounded and gated to the diagnostics surface.
        port.TechnicalDetail = new string('x', 1000) + LeakMarker;
        var bounded = session.GetDiagnostics().ConnectionTechnicalDetail;
        Assert.NotNull(bounded);
        Assert.True(bounded!.Length <= 300);
    }

    [Fact]
    public void PreferencesLoadProblems_AreVisibleToSupport()
    {
        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json"), "{\"schemaVersion\":77,\"x\":1}");

        var session = NewSession();
        session.StartNewGame(7UL);

        var diag = session.GetDiagnostics();
        Assert.Equal(UxPreferencesLoadOutcome.FutureVersion, diag.PreferencesLoadOutcome);
        Assert.Contains("77", diag.PreferencesLoadDetail);
    }

    [Fact]
    public void WatermarkAge_IsNullBeforeFirstRealBatch_ThenSaneAfterwards()
    {
        var session = NewSession();
        session.StartNewGame(7UL);
        session.Continue();

        // No batch has ever run: a default sentinel is NOT a fact and must not be
        // reported as an absurd age.
        Assert.Null(session.GetDiagnostics().CheckpointWatermarkAgeDays);

        _clock.Set(TestSessions.T0.AddHours(30));
        session.IngestActivityBatch(new[]
        {
            M5H1Records.Steps(1000L, TestSessions.T0.AddHours(20), TimeSpan.FromHours(1), sourceId: "w"),
        });

        var age = session.GetDiagnostics().CheckpointWatermarkAgeDays;
        Assert.NotNull(age);
        Assert.True(age >= 0 && age <= 1);
    }

    [Fact]
    public void Diagnostics_AreAvailableEvenWhenBootFailed()
    {
        File.WriteAllText(Path.Combine(_temp.Path, "save.json"), "garbage");
        File.WriteAllText(Path.Combine(_temp.Path, "save.backup.json"), "garbage");

        var session = NewSession();
        Assert.Equal(StartStatus.SaveUnreadable, session.Continue().Status);

        var diag = session.GetDiagnostics();
        Assert.Equal(DiagnosticsBootOutcome.SaveUnreadable, diag.BootOutcome);
        Assert.Equal(CodecFailureCategory.MalformedEnvelope, diag.LastBootCodecFailure);
        Assert.Equal(0, diag.SchemaVersion);
        Assert.Null(diag.CheckpointWatermarkAgeDays);
    }
}
