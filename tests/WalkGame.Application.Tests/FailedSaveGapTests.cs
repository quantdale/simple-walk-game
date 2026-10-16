using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-131 (D-128 residual): after a failed commit the in-memory state is ahead of disk. The
/// session marks it unsaved, a retry commits the whole state once, and no credit is ever
/// lost or doubled whichever way the gap is closed (retry, later commit, or reload).
/// </summary>
public sealed class FailedSaveGapTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static readonly List<NormalizedActivityRecord> Batch = new()
    {
        Rec("gap-a", 5_000L),
        Rec("gap-b", 7_500L),
    };

    private (GameSession session, FlakyStore store) Started()
    {
        var store = new FlakyStore(_temp.Path);
        var s = new GameSession(store, TestSessions.NewCodec(), new ManualClock(TestSessions.T0), Region1Catalog.Create());
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(7UL).Status);
        return (s, store);
    }

    private long DiskVitality()
    {
        var s = TestSessions.Create(_temp, new ManualClock(TestSessions.T0));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        return s.GetHome().Vitality;
    }

    [Fact]
    public void FreshSession_HasNoUnsavedChanges_AndRetryIsANoOp()
    {
        var (s, store) = Started();
        Assert.False(s.HasUnsavedChanges);
        int writes = store.Writes;
        Assert.True(s.RetryPendingSave());
        Assert.Equal(writes, store.Writes);
    }

    [Fact]
    public void FailedCommit_MarksUnsaved_RetryCommitsExactlyOnce()
    {
        var (s, store) = Started();
        store.FailNextWrites = 1;
        Assert.Throws<IOException>(() => s.IngestActivityBatch(Batch));
        Assert.True(s.HasUnsavedChanges);
        Assert.Equal(0L, DiskVitality());

        Assert.True(s.RetryPendingSave());
        Assert.False(s.HasUnsavedChanges);
        Assert.Equal(125L, DiskVitality());

        var replay = TestSessions.Create(_temp, new ManualClock(TestSessions.T0));
        replay.Continue();
        var r = replay.IngestActivityBatch(Batch);
        Assert.Equal(0L, r.VitalityCredited);
        Assert.Equal(2, r.DuplicatesIgnored);
        Assert.Equal(125L, replay.GetHome().Vitality);
    }

    [Fact]
    public void RetryWhileStorageStillFails_ReturnsFalse_NeverThrows_DiskUntouched()
    {
        var (s, store) = Started();
        store.FailNextWrites = 3;
        Assert.Throws<IOException>(() => s.IngestActivityBatch(Batch));
        var before = File.ReadAllBytes(_temp.FilePath("save.json"));
        Assert.False(s.RetryPendingSave());
        Assert.False(s.RetryPendingSave());
        Assert.True(s.HasUnsavedChanges);
        Assert.True(before.SequenceEqual(File.ReadAllBytes(_temp.FilePath("save.json"))));
        Assert.True(s.RetryPendingSave());
        Assert.Equal(125L, DiskVitality());
    }

    [Fact]
    public void ReplayingTheSameBatchInMemory_DuringTheGap_DoesNotDoubleCredit()
    {
        var (s, store) = Started();
        store.FailNextWrites = 1;
        Assert.Throws<IOException>(() => s.IngestActivityBatch(Batch));
        var again = s.IngestActivityBatch(Batch); // commits now
        Assert.Equal(0L, again.VitalityCredited);
        Assert.True(again.Saved);
        Assert.False(s.HasUnsavedChanges);
        Assert.Equal(125L, s.GetHome().Vitality);
        Assert.Equal(125L, DiskVitality());
    }

    [Fact]
    public void ReloadDuringTheGap_DropsUncommittedState_AndProviderReReadCreditsOnce()
    {
        var (s, store) = Started();
        store.FailNextWrites = 1;
        Assert.Throws<IOException>(() => s.IngestActivityBatch(Batch));
        Assert.Equal(StartStatus.Loaded, s.Continue().Status);
        Assert.False(s.HasUnsavedChanges);
        Assert.Equal(0L, s.GetHome().Vitality);
        var r = s.IngestActivityBatch(Batch);
        Assert.Equal(125L, r.VitalityCredited);
        Assert.Equal(125L, DiskVitality());
    }

    [Fact]
    public void AccessDenied_IsTreatedAsAFailedCommit_Too()
    {
        var (s, store) = Started();
        store.FailNextWrites = 1;
        store.FailWithAccessDenied = true;
        Assert.Throws<IOException>(() => s.IngestActivityBatch(Batch));
        Assert.True(s.HasUnsavedChanges);
        Assert.True(s.RetryPendingSave());
        Assert.Equal(125L, DiskVitality());
    }

    private static NormalizedActivityRecord Rec(string id, long steps) =>
        new NormalizedActivityRecord(
            ProviderNamespace: "test.gap",
            SourceRecordId: id,
            Category: ActivityCategory.Walking,
            Unit: ActivityUnits.Steps,
            Quantity: steps,
            StartUtc: TestSessions.T0.AddMinutes(-50),
            EndUtc: TestSessions.T0.AddMinutes(-20));

    private sealed class FlakyStore : ISaveStore
    {
        private readonly AtomicFileSaveStore _inner;
        public FlakyStore(string dir) => _inner = new AtomicFileSaveStore(dir);
        public int FailNextWrites { get; set; }
        public bool FailWithAccessDenied { get; set; }
        public int Writes { get; private set; }

        public void WriteAtomic(byte[] envelopeBytes)
        {
            if (FailNextWrites > 0)
            {
                FailNextWrites--;
                if (FailWithAccessDenied) throw new UnauthorizedAccessException("Injected denial.");
                throw new IOException("Injected storage failure.");
            }
            Writes++;
            _inner.WriteAtomic(envelopeBytes);
        }

        public void WriteAtomicPreservingBackup(byte[] envelopeBytes) => _inner.WriteAtomicPreservingBackup(envelopeBytes);
        public SaveReadResult ReadPrimary() => _inner.ReadPrimary();
        public SaveReadResult ReadBackup() => _inner.ReadBackup();
    }
}
