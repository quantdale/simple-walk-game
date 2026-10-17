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
/// D-139 hardening: many sync cycles while storage fails EVERY write (days of redelivered, overlapping
/// and new batches plus catch-up), then storage recovers. The in-memory world never double-credits,
/// never loses a credit, and the first successful save boots to exactly what a healthy twin computed.
/// </summary>
public sealed class PersistentSaveFailureSyncTests : IDisposable
{
    private readonly TempDirectory _flakyDir = new();
    private readonly TempDirectory _healthyDir = new();
    public void Dispose() { _flakyDir.Dispose(); _healthyDir.Dispose(); }

    private static string Snap(GameSession s) =>
        JsonSerializer.Serialize(new { home = s.GetHome(), credited = s.GetDiagnostics().LifetimeVitalityCredited });

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99)]
    public void ManySyncs_WhileEveryWriteFails_MatchAHealthyTwin_AndTheFirstGoodSaveBootsIt(int seed)
    {
        var rng = new Random(seed);
        var fc = new ManualClock(TestSessions.T0);
        var hc = new ManualClock(TestSessions.T0);
        var store = new AlwaysFailingStore(_flakyDir.Path);
        var f = new GameSession(store, TestSessions.NewCodec(), fc, Region1Catalog.Create());
        var h = TestSessions.Create(_healthyDir, hc);
        f.StartNewGame(7UL); h.StartNewGame(7UL);
        f.EnqueueProject(TestSessions.EntryProjectId); h.EnqueueProject(TestSessions.EntryProjectId);

        store.Failing = true;
        var delivered = new List<NormalizedActivityRecord>();
        long lastCredited = f.GetDiagnostics().LifetimeVitalityCredited;
        for (int cycle = 0; cycle < 40; cycle++)
        {
            var now = TestSessions.T0.AddHours(6 * (cycle + 1));
            fc.Set(now); hc.Set(now);
            var batch = new List<NormalizedActivityRecord>();
            // A fresh record, plus a random redelivery of something already sent (providers repeat themselves).
            batch.Add(M5H1Records.Steps(500 + rng.Next(4000), now.AddHours(-2), TimeSpan.FromHours(1), sourceId: "p-" + cycle));
            if (delivered.Count > 0) batch.Add(delivered[rng.Next(delivered.Count)]);
            delivered.Add(batch[0]);

            var fail = Record.Exception(() => f.IngestActivityBatch(batch));
            Assert.True(fail is IOException, "cycle " + cycle + ": the failing store surfaces as IOException, got " + fail);
            h.IngestActivityBatch(batch);
            if (rng.Next(3) == 0) { Assert.False(f.RetryPendingSave()); f.CatchUpWithoutReload(); h.Continue(); }

            Assert.True(f.HasUnsavedChanges);
            long credited = f.GetDiagnostics().LifetimeVitalityCredited;
            Assert.True(credited >= lastCredited, "credit never goes backwards");
            lastCredited = credited;
        }

        h = TestSessions.Create(_healthyDir, hc); h.Continue();
        Assert.Equal(Snap(h), Snap(f));

        store.Failing = false;
        Assert.True(f.RetryPendingSave());
        Assert.False(f.HasUnsavedChanges);
        var rebooted = new GameSession(store, TestSessions.NewCodec(), fc, Region1Catalog.Create());
        rebooted.Continue();
        Assert.Equal(Snap(h), Snap(rebooted));

        // Redelivering everything after recovery credits nothing more.
        rebooted.IngestActivityBatch(delivered);
        Assert.Equal(Snap(h), Snap(rebooted));
    }

    private sealed class AlwaysFailingStore : ISaveStore
    {
        private readonly AtomicFileSaveStore _inner;
        public AlwaysFailingStore(string dir) => _inner = new AtomicFileSaveStore(dir);
        public bool Failing { get; set; }
        private void Gate() { if (Failing) throw new IOException("Injected persistent storage failure."); }
        public void WriteAtomic(byte[] b) { Gate(); _inner.WriteAtomic(b); }
        public void WriteAtomicPreservingBackup(byte[] b) { Gate(); _inner.WriteAtomicPreservingBackup(b); }
        public SaveReadResult ReadPrimary() => _inner.ReadPrimary();
        public SaveReadResult ReadBackup() => _inner.ReadBackup();
    }
}
