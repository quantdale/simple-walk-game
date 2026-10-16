using System;
using System.IO;
using System.Linq;
using System.Text;
using WalkGame.Application.Development;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Time;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-091: adverse byte-level damage to the PRIMARY save at session level (truncation, a single
/// flipped byte, oversized garbage, a re-encoded file). Each must recover from the backup without
/// throwing, the recovered game must keep playing and persist (a later boot loads normally), and
/// when BOTH generations carry the damage the session fails closed and a no-overwrite start
/// (D-085) still refuses, leaving the bytes for support/recovery.
/// </summary>
public sealed class SaveRecoveryAdverseTests : IDisposable
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private static byte[] Damage(string kind, byte[] b) => kind switch
    {
        "truncated-half" => b.Take(b.Length / 2).ToArray(),
        "truncated-one-byte" => b.Take(b.Length - 1).ToArray(),
        "flipped-middle-byte" => b.Select((x, i) => i == b.Length / 2 ? (byte)(x ^ 0x01) : x).ToArray(),
        "oversized-garbage" => Enumerable.Range(0, 2_000_000).Select(i => (byte)(i * 31 + 7)).ToArray(),
        "utf16-reencoded" => Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Encoding.UTF8.GetString(b))).ToArray(),
        "nul-filled" => new byte[b.Length],
        _ => throw new ArgumentException(kind),
    };

    private long BuildGame()
    {
        Assert.Equal(StartStatus.NewGameCreated, TestSessions.Create(_temp.Path, new ManualClock(T0)).StartNewGame(42UL).Status);
        for (int d = 1; d <= 3; d++)
        {
            var s = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(d)));
            Assert.Equal(StartStatus.Loaded, s.Continue().Status);
            s.IngestFromSource(new SyntheticWalkingSource(9_000), T0.AddDays(d - 1), T0.AddDays(d));
        }
        Assert.True(File.Exists(_temp.FilePath("save.backup.json")), "fixture needs a backup generation");
        var check = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(3)));
        Assert.Equal(StartStatus.Loaded, check.Continue().Status);
        return check.GetHome().Vitality;
    }

    [Theory]
    [InlineData("truncated-half")]
    [InlineData("truncated-one-byte")]
    [InlineData("flipped-middle-byte")]
    [InlineData("oversized-garbage")]
    [InlineData("utf16-reencoded")]
    [InlineData("nul-filled")]
    public void Damaged_primary_recovers_keeps_playing_and_the_next_boot_loads(string kind)
    {
        BuildGame();
        var primary = _temp.FilePath("save.json");
        File.WriteAllBytes(primary, Damage(kind, File.ReadAllBytes(primary)));

        var recovered = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(4)));
        Assert.Equal(StartStatus.RecoveredFromBackup, recovered.Continue().Status);
        long vitality = recovered.GetHome().Vitality;
        recovered.IngestFromSource(new SyntheticWalkingSource(9_000), T0.AddDays(3), T0.AddDays(4));

        var next = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(5)));
        Assert.Equal(StartStatus.Loaded, next.Continue().Status);
        Assert.True(next.GetHome().Vitality >= vitality, "progress after recovery was not persisted");
    }

    [Theory]
    [InlineData("truncated-half")]
    [InlineData("flipped-middle-byte")]
    [InlineData("oversized-garbage")]
    [InlineData("utf16-reencoded")]
    [InlineData("nul-filled")]
    public void Both_generations_damaged_fail_closed_and_a_safe_start_refuses_to_overwrite(string kind)
    {
        BuildGame();
        foreach (var f in new[] { "save.json", "save.backup.json" })
            File.WriteAllBytes(_temp.FilePath(f), Damage(kind, File.ReadAllBytes(_temp.FilePath(f))));
        var primaryBefore = File.ReadAllBytes(_temp.FilePath("save.json"));
        var backupBefore = File.ReadAllBytes(_temp.FilePath("save.backup.json"));

        var s = TestSessions.Create(_temp.Path, new ManualClock(T0.AddDays(4)));
        var status = s.Continue().Status;
        Assert.Contains(status, new[] { StartStatus.SaveUnreadable, StartStatus.StateInvalid });
        Assert.False(s.HasLoadedState);

        Assert.Equal(StartStatus.ExistingSaveRefused, s.StartNewGame(99UL, overwriteExisting: false).Status);
        Assert.Equal(primaryBefore, File.ReadAllBytes(_temp.FilePath("save.json")));
        Assert.Equal(backupBefore, File.ReadAllBytes(_temp.FilePath("save.backup.json")));
    }
}
