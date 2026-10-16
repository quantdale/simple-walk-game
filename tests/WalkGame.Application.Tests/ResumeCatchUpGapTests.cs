using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-134 (D-131 gap): while saves keep failing, a resume keeps the in-memory world AND
/// still runs offline catch-up, exactly once, matching what a healthy boot would compute.
/// </summary>
public sealed class ResumeCatchUpGapTests : IDisposable
{
    private readonly TempDirectory _flakyDir = new();
    private readonly TempDirectory _healthyDir = new();
    public void Dispose() { _flakyDir.Dispose(); _healthyDir.Dispose(); }

    private static readonly List<NormalizedActivityRecord> Batch = new()
    {
        M5H1Records.Steps(9000L, TestSessions.T0.AddHours(-2), TimeSpan.FromHours(1), sourceId: "cu-a"),
    };

    private static string Snap(GameSession s) =>
        JsonSerializer.Serialize(new { home = s.GetHome(), credited = s.GetDiagnostics().LifetimeVitalityCredited });

    private (GameSession s, FlakyStore store, ManualClock clock) Flaky()
    {
        var clock = new ManualClock(TestSessions.T0);
        var store = new FlakyStore(_flakyDir.Path);
        var s = new GameSession(store, TestSessions.NewCodec(), clock, Region1Catalog.Create());
        s.StartNewGame(7UL);
        s.EnqueueProject(TestSessions.EntryProjectId);
        return (s, store, clock);
    }

    private (GameSession s, ManualClock clock) Healthy()
    {
        var clock = new ManualClock(TestSessions.T0);
        var s = TestSessions.Create(_healthyDir, clock);
        s.StartNewGame(7UL);
        s.EnqueueProject(TestSessions.EntryProjectId);
        return (s, clock);
    }

    [Fact]
    public void CatchUp_DuringFailingSaves_MatchesAHealthyBoot_AndKeepsUnsavedProgress()
    {
        var (f, store, fc) = Flaky();
        var (h, hc) = Healthy();
        store.FailAll = true;
        Assert.Throws<IOException>(() => f.IngestActivityBatch(Batch));
        h.IngestActivityBatch(Batch);
        Assert.True(f.HasUnsavedChanges);

        fc.Set(TestSessions.T0.AddDays(3)); hc.Set(TestSessions.T0.AddDays(3));
        Assert.False(f.RetryPendingSave());
        Assert.False(f.CatchUpWithoutReload());
        Assert.True(f.HasUnsavedChanges);
        var rebooted = TestSessions.Create(_healthyDir, hc);
        rebooted.Continue();
        Assert.Equal(Snap(rebooted), Snap(f));
        Assert.True(f.GetDiagnostics().LifetimeVitalityCredited > 0, "gap credit kept in memory");
    }

    [Fact]
    public void RepeatedCatchUp_AtTheSameClock_CreditsNothingMore()
    {
        var (f, store, fc) = Flaky();
        store.FailAll = true;
        Assert.Throws<IOException>(() => f.IngestActivityBatch(Batch));
        fc.Set(TestSessions.T0.AddDays(2));
        f.CatchUpWithoutReload();
        var once = Snap(f);
        for (int i = 0; i < 5; i++) Assert.False(f.CatchUpWithoutReload());
        Assert.Equal(once, Snap(f));
    }

    [Fact]
    public void CatchUp_InSteps_EqualsOneCatchUp_OverTheSameSpan()
    {
        var (f, store, fc) = Flaky();
        var (h, hc) = Healthy();
        store.FailAll = true;
        Assert.Throws<IOException>(() => f.IngestActivityBatch(Batch));
        h.IngestActivityBatch(Batch);
        for (int d = 1; d <= 6; d++) { fc.Set(TestSessions.T0.AddDays(d)); f.CatchUpWithoutReload(); }
        hc.Set(TestSessions.T0.AddDays(6));
        h.CatchUpWithoutReload();
        Assert.Equal(Snap(h), Snap(f));
    }

    [Fact]
    public void WhenStorageRecovers_CatchUpCommits_OnceAndReplayCreditsNothing()
    {
        var (f, store, fc) = Flaky();
        store.FailAll = true;
        Assert.Throws<IOException>(() => f.IngestActivityBatch(Batch));
        fc.Set(TestSessions.T0.AddDays(1));
        Assert.False(f.CatchUpWithoutReload());
        store.FailAll = false;
        Assert.True(f.CatchUpWithoutReload());
        Assert.False(f.HasUnsavedChanges);
        var disk = TestSessions.Create(_flakyDir, fc);
        disk.Continue();
        Assert.Equal(f.GetDiagnostics().LifetimeVitalityCredited, disk.GetDiagnostics().LifetimeVitalityCredited);
        Assert.Equal(0L, disk.IngestActivityBatch(Batch).VitalityCredited);
    }

    [Fact]
    public void BackwardClock_DuringTheGap_NeverRegressesOrCredits()
    {
        var (f, store, fc) = Flaky();
        store.FailAll = true;
        Assert.Throws<IOException>(() => f.IngestActivityBatch(Batch));
        fc.Set(TestSessions.T0.AddDays(2)); f.CatchUpWithoutReload();
        var ahead = Snap(f);
        fc.Set(TestSessions.T0.AddDays(-5)); f.CatchUpWithoutReload();
        fc.Set(TestSessions.T0.AddDays(2)); f.CatchUpWithoutReload();
        Assert.Equal(ahead, Snap(f));
    }

    [Fact]
    public void CatchUp_WithoutLoadedState_FailsClosed()
    {
        var s = TestSessions.Create(_flakyDir, new ManualClock(TestSessions.T0));
        Assert.Throws<InvalidOperationException>(() => s.CatchUpWithoutReload());
    }

    private sealed class FlakyStore : ISaveStore
    {
        private readonly AtomicFileSaveStore _inner;
        public FlakyStore(string dir) => _inner = new AtomicFileSaveStore(dir);
        public bool FailAll { get; set; }
        public void WriteAtomic(byte[] b) { if (FailAll) throw new IOException("Injected."); _inner.WriteAtomic(b); }
        public void WriteAtomicPreservingBackup(byte[] b) { if (FailAll) throw new IOException("Injected."); _inner.WriteAtomicPreservingBackup(b); }
        public SaveReadResult ReadPrimary() => _inner.ReadPrimary();
        public SaveReadResult ReadBackup() => _inner.ReadBackup();
    }
}
