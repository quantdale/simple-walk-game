using System;
using System.IO;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>D-103: days walked through the real ingestion pipeline and save.</summary>
public sealed class DaysWalkedSessionTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);
    private const int Manila = 8 * 3600;
    private readonly TempDirectory _temp = new();
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FreshGame_HasZeroExactDays()
    {
        var s = Start(T0);
        var d = s.GetDaysWalked();
        Assert.Equal(0, d.Days);
        Assert.False(d.IsLowerBound);
    }

    [Fact]
    public void DatedWalks_CountLocalDays_AndSurviveReload()
    {
        var s = Start(T0);
        // 15:30 UTC and 17:00 UTC on Mar 9 are Mar 9 23:30 and Mar 10 01:00 in Manila: two local days.
        s.IngestActivityBatch(new[]
        {
            Rec("a", 3000, T0.AddHours(-16.5), Manila),
            Rec("b", 3000, T0.AddHours(-15), Manila),
            Rec("c", 3000, T0.AddHours(-1), Manila), // Mar 10 15:00 Manila: same day as "b"
        });
        Assert.Equal(2, s.GetDaysWalked().Days);
        Assert.False(s.GetDaysWalked().IsLowerBound);

        var reloaded = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.Loaded, reloaded.Continue().Status);
        Assert.Equal(2, reloaded.GetDaysWalked().Days);
        Assert.Empty(GameStateValidator.Validate(State(), Region1Catalog.Create()));
    }

    [Fact]
    public void ProviderDeletion_UncountsTheDay()
    {
        var s = Start(T0);
        s.IngestActivityBatch(new[] { Rec("a", 3000, T0.AddHours(-30), Manila), Rec("b", 3000, T0.AddHours(-1), Manila) });
        Assert.Equal(2, s.GetDaysWalked().Days);
        s.IngestActivityBatch(new[] { Rec("a", 0, T0.AddHours(-30), Manila, revision: 2, deletion: true) });
        Assert.Equal(1, s.GetDaysWalked().Days);
        Assert.Empty(GameStateValidator.Validate(State(), Region1Catalog.Create()));
    }

    [Fact]
    public void WalkWithoutOffset_IsShownAsALowerBound()
    {
        var s = Start(T0);
        s.IngestActivityBatch(new[] { Rec("a", 3000, T0.AddHours(-1), null) });
        var d = s.GetDaysWalked();
        Assert.Equal(0, d.Days);
        Assert.True(d.IsLowerBound);
    }

    [Fact]
    public void Validator_RejectsATamperedDaySet()
    {
        Start(T0).IngestActivityBatch(new[] { Rec("a", 3000, T0.AddHours(-1), Manila) });
        var state = State();
        state.ProcessedRecords.WalkedLocalDays.Clear();
        Assert.Contains(GameStateValidator.Validate(state, Region1Catalog.Create()), v => v.Contains("days-walked"));
        state.ProcessedRecords.WalkedLocalDays.AddRange(new[] { 5, 5 });
        Assert.Contains(GameStateValidator.Validate(state, Region1Catalog.Create()), v => v.Contains("strictly ascending"));
    }

    private static NormalizedActivityRecord Rec(string id, long qty, DateTimeOffset end, int? offset, int revision = 1, bool deletion = false) =>
        new("p", id, ActivityCategory.Walking, ActivityUnits.Steps, qty, end.AddMinutes(-20), end, revision, deletion, offset);

    private GameSession Start(DateTimeOffset now)
    {
        var s = TestSessions.Create(_temp, new ManualClock(now));
        Assert.Equal(StartStatus.NewGameCreated, s.StartNewGame(seed: 7UL).Status);
        return s;
    }

    private GameState State() =>
        TestSessions.NewCodec().Decode(File.ReadAllBytes(Path.Combine(_temp.Path, "save.json"))).State!;
}
