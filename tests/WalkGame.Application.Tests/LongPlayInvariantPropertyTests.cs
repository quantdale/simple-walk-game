using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-135: seeded property tests over long random play sequences (ingest, replay, clock
/// jumps forward and backward, project queueing, reloads, summary acknowledgements and
/// failed saves that are later retried). After every step:
///   * no double credit: replaying any already-delivered record credits 0;
///   * accomplishments are never lost: completed projects and unlocked chapters only grow;
///   * progression is monotonic: lifetime credited Vitality never decreases;
///   * a reload of a fully committed world reproduces the same progression.
/// </summary>
public sealed class LongPlayInvariantPropertyTests
{
    public static IEnumerable<object[]> Seeds() =>
        Enumerable.Range(1, 12).Select(i => new object[] { i * 7919 });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void LongRandomPlay_KeepsCreditExactlyOnce_AccomplishmentsAndProgressionMonotonic(int seed)
    {
        using var temp = new TempDirectory();
        var rng = new Random(seed);
        var clock = new ManualClock(TestSessions.T0);
        var store = new FlakyStore(temp.Path);
        GameSession Open() => new GameSession(store, TestSessions.NewCodec(), clock, Region1Catalog.Create());
        var s = Open();
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame((ulong)seed).Status);

        var history = new List<NormalizedActivityRecord>();
        long lastCredited = 0;
        var completed = new HashSet<string>();
        int lastChapters = 0;
        int next = 0;

        for (int step = 0; step < 300; step++)
        {
            int op = rng.Next(100);
            store.FailNext = rng.Next(10) == 0; // ~10% of steps hit one failing commit
            try
            {
                if (op < 35)
                {
                    var batch = new List<NormalizedActivityRecord>();
                    int n = rng.Next(1, 4);
                    for (int i = 0; i < n; i++)
                        batch.Add(M5H1Records.Steps(rng.Next(100, 12_000), clock.UtcNow.AddHours(-3).AddMinutes(i * 20),
                            TimeSpan.FromMinutes(15), sourceId: "p" + seed + "-" + next++));
                    s.IngestActivityBatch(batch);
                    history.AddRange(batch);
                }
                else if (op < 50 && history.Count > 0)
                {
                    var replay = Enumerable.Range(0, rng.Next(1, 5)).Select(_ => history[rng.Next(history.Count)]).ToList();
                    var r = s.IngestActivityBatch(replay);
                    Assert.Equal(0L, r.VitalityCredited);
                }
                else if (op < 70)
                {
                    var forward = rng.Next(8) == 0 ? -TimeSpan.FromHours(rng.Next(1, 48)) : TimeSpan.FromHours(rng.Next(0, 72));
                    clock.Set(clock.UtcNow + forward);
                    s.CatchUpWithoutReload(); // D-134 in-memory catch-up (commits when healthy)
                }
                else if (op < 82)
                {
                    var rows = s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Available).ToList();
                    if (rows.Count > 0) s.EnqueueProject(rows[rng.Next(rows.Count)].ProjectId);
                }
                else if (op < 90)
                {
                    s.AcknowledgeReturnSummary();
                }
                else
                {
                    store.FailNext = false;
                    Assert.True(s.RetryPendingSave());
                    s = Open();
                    var st = s.Continue().Status;
                    Assert.True(st == StartStatus.Loaded || st == StartStatus.RecoveredFromBackup, st.ToString());
                }
            }
            catch (IOException) { /* injected commit failure: state is kept in memory, retried below */ }

            store.FailNext = false;
            Assert.True(s.RetryPendingSave(), "storage is healthy again, the retry must commit");
            Assert.False(s.HasUnsavedChanges);

            long credited = s.GetDiagnostics().LifetimeVitalityCredited;
            Assert.True(credited >= lastCredited, $"seed {seed} step {step}: credit regressed {lastCredited} -> {credited}");
            lastCredited = credited;

            var nowCompleted = s.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Completed).Select(p => p.ProjectId).ToHashSet();
            Assert.True(completed.IsSubsetOf(nowCompleted), $"seed {seed} step {step}: a completed project was lost");
            completed = nowCompleted;

            int chapters = s.GetStory().UnlockedCount;
            Assert.True(chapters >= lastChapters, $"seed {seed} step {step}: a chapter was lost");
            lastChapters = chapters;
        }

        Assert.True(lastCredited > 0, "the sequence must actually credit activity");
        Assert.NotEmpty(completed); // long play always finishes at least one project

        // Full-history replay after a reload credits nothing and changes nothing.
        var reloaded = Open();
        reloaded.Continue();
        Assert.Equal(lastCredited, reloaded.GetDiagnostics().LifetimeVitalityCredited);
        foreach (var chunk in history.Chunk(25))
            Assert.Equal(0L, reloaded.IngestActivityBatch(chunk).VitalityCredited);
        Assert.Equal(lastCredited, reloaded.GetDiagnostics().LifetimeVitalityCredited);
        Assert.True(completed.SetEquals(reloaded.GetProjects().Projects.Where(p => p.Status == ProjectStatus.Completed).Select(p => p.ProjectId)));
    }

    private sealed class FlakyStore : ISaveStore
    {
        private readonly AtomicFileSaveStore _inner;
        public FlakyStore(string dir) => _inner = new AtomicFileSaveStore(dir);
        public bool FailNext { get; set; }
        private void Gate() { if (FailNext) { FailNext = false; throw new IOException("Injected."); } }
        public void WriteAtomic(byte[] b) { Gate(); _inner.WriteAtomic(b); }
        public void WriteAtomicPreservingBackup(byte[] b) { Gate(); _inner.WriteAtomicPreservingBackup(b); }
        public SaveReadResult ReadPrimary() => _inner.ReadPrimary();
        public SaveReadResult ReadBackup() => _inner.ReadBackup();
    }
}
