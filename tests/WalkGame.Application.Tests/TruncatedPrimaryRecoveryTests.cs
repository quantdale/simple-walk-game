using System;
using System.IO;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-197 hardening: process death while the OS had not finished flushing the primary
/// (a file system without rename-after-flush ordering) can leave a primary that is a
/// PREFIX of the bytes we wrote. Every prefix must boot the backup generation, never a
/// fresh world, never a partial one, and the next save must heal the primary.
/// </summary>
public sealed class TruncatedPrimaryRecoveryTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(2)]
    [InlineData(-1)]
    public void TruncatedPrimary_BootsTheBackupGeneration_AndTheNextSaveHeals(int divisor)
    {
        var clock = new ManualClock(TestSessions.T0);
        var a = TestSessions.Create(_temp, clock);
        a.StartNewGame(7UL);
        a.EnqueueProject(TestSessions.EntryProjectId);
        a.ActivateQueuedProject(TestSessions.EntryProjectId);
        a.CreditActivity(TestSessions.Tx1, TestSessions.T0, 500L, "walk");
        long backupLifetime = a.GetHome().Vitality;
        a.CreditActivity(TestSessions.Tx2, TestSessions.T0.AddHours(1), 300L, "walk 2");
        long primaryLifetime = a.GetHome().Vitality;
        Assert.NotEqual(backupLifetime, primaryLifetime);

        string primary = _temp.FilePath("save.json");
        byte[] full = File.ReadAllBytes(primary);
        int keep = divisor == -1 ? full.Length - 1 : divisor == 1 ? 1 : full.Length / divisor;
        File.WriteAllBytes(primary, full.AsSpan(0, keep).ToArray());

        var boot = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.RecoveredFromBackup, boot.Continue().Status);
        Assert.Equal(backupLifetime, boot.GetHome().Vitality);

        boot.CreditActivity(TestSessions.Tx2, TestSessions.T0.AddHours(1), 300L, "walk 2 again");
        var again = TestSessions.Create(_temp, clock);
        Assert.Equal(StartStatus.Loaded, again.Continue().Status);
        Assert.Equal(primaryLifetime, again.GetHome().Vitality);
    }
}
