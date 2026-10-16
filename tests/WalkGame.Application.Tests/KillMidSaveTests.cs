using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-132: an app pause or process kill in the middle of a save. A pause cannot interleave
/// inside the synchronous commit (D-128), so the hostile case is a kill. Each test stops
/// <see cref="WalkGame.Infrastructure.Persistence.AtomicFileSaveStore.WriteAtomic"/> after
/// one of its file-system steps by replaying exactly the steps that had completed, then
/// boots a fresh session over what is left on disk. Every cut point must load either the
/// old or the new generation (never a fabricated world, never a partial credit), and
/// replaying the same activity must land the credit exactly once.
/// </summary>
public sealed class KillMidSaveTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    // WriteAtomic steps (AtomicFileSaveStore): 1 write save.tmp, 2 copy save.json → save.backup.tmp,
    // 3 delete save.backup.json, 4 move save.backup.tmp → save.backup.json,
    // 5 delete save.json, 6 move save.tmp → save.json. Cut point k = the first k steps finished.
    // Cut point 0.5 = save.tmp only half written (torn write).
    public static IEnumerable<object[]> CutPoints() =>
        new[] { "0", "0.5", "1", "2", "3", "4", "5", "6" }.Select(k => new object[] { k });

    private static readonly List<NormalizedActivityRecord> Batch = new()
    {
        new NormalizedActivityRecord("test.kill", "kill-a", ActivityCategory.Walking, ActivityUnits.Steps, 5_000L,
            TestSessions.T0.AddMinutes(-50), TestSessions.T0.AddMinutes(-20)),
        new NormalizedActivityRecord("test.kill", "kill-b", ActivityCategory.Walking, ActivityUnits.Steps, 7_500L,
            TestSessions.T0.AddMinutes(-50), TestSessions.T0.AddMinutes(-20)),
    };

    private string P(string name) => _temp.FilePath(name);

    [Theory]
    [MemberData(nameof(CutPoints))]
    public void KillAfterEachWriteStep_BootsOldOrNew_AndCreditLandsExactlyOnce(string cut)
    {
        var clock = new ManualClock(TestSessions.T0);
        var a = TestSessions.Create(_temp, clock);
        a.StartNewGame(7UL);
        a.CreditActivity(TestSessions.Tx1, TestSessions.T0, 40L, "walk"); // a backup slot exists from here on
        long oldLifetime = a.GetDiagnostics().LifetimeVitalityCredited;
        byte[] oldPrimary = File.ReadAllBytes(P("save.json"));

        // Produce the NEW generation's bytes in a scratch directory, then restore the old disk.
        using var scratch = new TempDirectory();
        foreach (var f in Directory.GetFiles(_temp.Path)) File.Copy(f, Path.Combine(scratch.Path, Path.GetFileName(f)));
        var b = TestSessions.Create(scratch, clock);
        Assert.Equal(StartStatus.Loaded, b.Continue().Status);
        Assert.Equal(125L, b.IngestActivityBatch(Batch).VitalityCredited);
        long newLifetime = b.GetDiagnostics().LifetimeVitalityCredited;
        byte[] newPrimary = File.ReadAllBytes(scratch.FilePath("save.json"));

        SimulateKilledWrite(cut, oldPrimary, newPrimary);

        var boot = TestSessions.Create(_temp, clock);
        var status = boot.Continue().Status;
        Assert.True(status == StartStatus.Loaded || status == StartStatus.RecoveredFromBackup,
            $"cut {cut}: boot must open a real generation, got {status}");
        long seen = boot.GetDiagnostics().LifetimeVitalityCredited;
        Assert.True(seen == oldLifetime || seen == newLifetime, $"cut {cut}: partial or fabricated credit {seen}");

        var replay = boot.IngestActivityBatch(Batch);
        Assert.Equal(newLifetime, boot.GetDiagnostics().LifetimeVitalityCredited);
        Assert.Equal(seen == newLifetime ? 0L : 125L, replay.VitalityCredited);

        var again = TestSessions.Create(_temp, clock);
        again.Continue();
        Assert.Equal(0L, again.IngestActivityBatch(Batch).VitalityCredited);
        Assert.Equal(newLifetime, again.GetDiagnostics().LifetimeVitalityCredited);
    }

    private void SimulateKilledWrite(string cut, byte[] oldPrimary, byte[] newPrimary)
    {
        if (cut == "0") return;
        if (cut == "0.5") { File.WriteAllBytes(P("save.tmp"), newPrimary.Take(newPrimary.Length / 2).ToArray()); return; }
        int k = int.Parse(cut, System.Globalization.CultureInfo.InvariantCulture);
        if (k >= 1) File.WriteAllBytes(P("save.tmp"), newPrimary);
        if (k >= 2) File.WriteAllBytes(P("save.backup.tmp"), oldPrimary);
        if (k >= 3) File.Delete(P("save.backup.json"));
        if (k >= 4) File.Move(P("save.backup.tmp"), P("save.backup.json"));
        if (k >= 5) File.Delete(P("save.json"));
        if (k >= 6) File.Move(P("save.tmp"), P("save.json"));
    }

    [Fact]
    public void KillWithTornBackupTemp_AndAbsentPrimary_StillRecoversAGeneration()
    {
        var clock = new ManualClock(TestSessions.T0);
        var a = TestSessions.Create(_temp, clock);
        a.StartNewGame(7UL);
        a.CreditActivity(TestSessions.Tx1, TestSessions.T0, 40L, "walk");
        long afterFirst = a.GetHome().Vitality;
        a.CreditActivity(TestSessions.Tx2, TestSessions.T0.AddMinutes(1), 10L, "walk");
        var primary = File.ReadAllBytes(P("save.json"));
        File.WriteAllBytes(P("save.backup.tmp"), primary.Take(7).ToArray());
        File.Delete(P("save.json"));

        var boot = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.RecoveredFromBackup, boot.Continue().Status);
        Assert.Equal(afterFirst, boot.GetHome().Vitality); // the previous durable generation, not a fresh world
    }
}
