using System;
using System.IO;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Application.Ux;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using Xunit;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-123: seeded property tests for the reminder time setting and the restart path (a new
/// session over the same directory, as after an app or phone restart). Fixed seeds reproduce.
/// </summary>
public sealed class ReminderPropertyTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    private GameSession Session() =>
        TestSessions.Create(_temp.Path, new ManualClock(T0), new LocalPreferencesStore(_temp.Path));

    private static int Draw(Random r) => r.Next(5) switch
    {
        0 => r.Next(UxPreferencesState.ReminderMinutesMin, UxPreferencesState.ReminderMinutesMax + 1),
        1 => r.Next(-3, 3),
        2 => r.Next(24 * 60 - 3, 24 * 60 + 3),
        3 => r.Next(int.MinValue, int.MaxValue),
        _ => r.Next(2) == 0 ? int.MinValue : int.MaxValue,
    };

    [Fact]
    public void SetDailyReminder_AcceptsExactlyTheDay_AndAFailedSetChangesNothing_AcrossRestarts()
    {
        var rnd = new Random(20261017);
        var s = Session();
        s.StartNewGame(7UL);
        bool enabled = false; int minutes = UxPreferencesState.DefaultReminderMinutesOfDay;
        for (int i = 0; i < 400; i++)
        {
            bool want = rnd.Next(2) == 0;
            int m = Draw(rnd);
            bool valid = m >= 0 && m < 24 * 60;
            var r = s.SetDailyReminder(want, m);
            Assert.Equal(valid, r.IsSuccess);
            if (valid) { enabled = want; minutes = m; }
            else Assert.Equal(UxErrorCodes.InvalidReminderTime, r.Error!.Code);
            if (i % 20 == 0)
            {
                // Restart path: a fresh session reads back exactly what was last accepted.
                s = Session();
                Assert.Equal(StartStatus.Loaded, s.Continue().Status);
            }
            var settings = s.GetSettings();
            Assert.Equal(minutes, settings.DailyReminderMinutesOfDay);
            Assert.Equal(enabled, settings.DailyReminderEnabled);
        }
    }

    [Fact]
    public void ADamagedStoredTime_FallsBackToTheDefault_SoTurningOnIsNeverADeadEnd()
    {
        var rnd = new Random(7);
        for (int i = 0; i < 200; i++)
        {
            int m = Draw(rnd);
            File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json"),
                "{\"schemaVersion\":" + UxPreferencesState.CurrentVersion + ",\"dailyReminderEnabled\":false,\"dailyReminderMinutesOfDay\":" + m + "}");
            var loaded = new LocalPreferencesStore(_temp.Path).Load();
            Assert.Equal(UxPreferencesLoadOutcome.Success, loaded.Outcome);
            int got = loaded.State!.DailyReminderMinutesOfDay;
            Assert.InRange(got, UxPreferencesState.ReminderMinutesMin, UxPreferencesState.ReminderMinutesMax);
            Assert.Equal(m >= 0 && m < 24 * 60 ? m : UxPreferencesState.DefaultReminderMinutesOfDay, got);
        }

        File.WriteAllText(Path.Combine(_temp.Path, "ux-preferences.json"),
            "{\"schemaVersion\":" + UxPreferencesState.CurrentVersion + ",\"dailyReminderMinutesOfDay\":99999}");
        var s = Session();
        s.StartNewGame(7UL);
        var settings = s.GetSettings();
        Assert.True(s.SetDailyReminder(true, settings.DailyReminderMinutesOfDay).IsSuccess,
            "the shell turns the reminder on with the stored time; it must validate");
    }
}
