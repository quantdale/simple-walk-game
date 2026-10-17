using System;
using System.Collections.Generic;
using WalkGame.Application.Activity;
using WalkGame.Application.Development;
using WalkGame.Domain.Activity;


/// <summary>
/// D-190: provider noise that a correct trust pipeline must turn into exactly the clean credit.
/// For one simulated day it delivers, in a deliberately scrambled order:
/// <list type="bullet">
/// <item>a deletion for a record the game has never seen (D-183 tombstone, zero credit);</item>
/// <item>today's record, then an equal-quantity revision 2 of it (a no-op correction, D-178/D-181);</item>
/// <item>yesterday's record again, exactly as first delivered (a re-delivered duplicate);</item>
/// <item>today's revision-1 record a second time, after its revision 2 (a stale revision).</item>
/// </list>
/// Identities and quantities match <see cref="SyntheticWalkingSource"/>, so the clean and noisy
/// runs differ only in noise. Development tooling only; never shipped.
/// </summary>
internal sealed class NoisyReplaySource : IActivityRecordSource
{
    private readonly long _today;
    private readonly long _yesterday;

    public NoisyReplaySource(long todaySteps, long yesterdaySteps)
    {
        _today = Math.Max(0L, todaySteps);
        _yesterday = Math.Max(0L, yesterdaySteps);
    }

    public string ProviderNamespace => SyntheticWalkingSource.DevProviderNamespace;

    /// <summary>The window must be [today - 1 day, today end): one UTC day of history plus today.</summary>
    public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc)
    {
        var todayStart = windowEndUtc.ToUniversalTime().AddDays(-1);
        var yesterdayStart = todayStart.AddDays(-1);
        bool hasYesterday = windowStartUtc.ToUniversalTime() <= yesterdayStart;
        var records = new List<NormalizedActivityRecord>
        {
            new(ProviderNamespace, "ghost." + todayStart.UtcDateTime.ToString("yyyyMMdd"), ActivityCategory.Walking,
                ActivityUnits.Steps, 0L, todayStart, todayStart.AddHours(1), Revision: 3, IsDeletion: true),
        };
        if (_today > 0L)
        {
            records.Add(Day(todayStart, _today, 1));
            records.Add(Day(todayStart, _today, 2));
        }
        if (hasYesterday && _yesterday > 0L)
            records.Add(Day(yesterdayStart, _yesterday, 1));
        if (_today > 0L)
            records.Add(Day(todayStart, _today, 1));
        return records;
    }

    private NormalizedActivityRecord Day(DateTimeOffset dayStart, long steps, int revision) =>
        new(ProviderNamespace, SyntheticWalkingSource.SourceRecordIdFor(dayStart), ActivityCategory.Walking,
            ActivityUnits.Steps, steps, dayStart, dayStart.AddDays(1), Revision: revision);
}
