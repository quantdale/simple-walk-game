using System;
using System.Collections.Generic;
using WalkGame.Domain.Activity;

namespace WalkGame.Application.Development
{
    /// <summary>
    /// DEVELOPMENT-ONLY deterministic walking-activity generator. Exists to prove the
    /// ambient loop ("app closed while synthetic activity accumulates → reopen →
    /// reconcile through the production trust pipeline") without platform health APIs.
    ///
    /// This is not a production health provider and must never ship wired into a
    /// production composition root: production builds exclude it by simply never
    /// constructing it (the composition root chooses its own IActivityRecordSource).
    /// Records carry stable per-day source IDs, so replaying a window is always an
    /// exactly-once no-op.
    /// </summary>
    public sealed class SyntheticWalkingSource : WalkGame.Application.Activity.IActivityRecordSource
    {
        public const string DevProviderNamespace = "dev.synthetic-walking";

        private readonly long _stepsPerDay;

        public SyntheticWalkingSource(long stepsPerDay)
        {
            if (stepsPerDay <= 0L)
                throw new ArgumentOutOfRangeException(nameof(stepsPerDay), "Steps per day must be positive.");
            _stepsPerDay = stepsPerDay;
        }

        public string ProviderNamespace => DevProviderNamespace;

        public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc)
        {
            var records = new List<NormalizedActivityRecord>();
            DateTimeOffset start = windowStartUtc.ToUniversalTime();
            DateTimeOffset end = windowEndUtc.ToUniversalTime();

            // One full-UTC-day record per day fully contained in the requested window.
            for (DateTimeOffset day = start; day.AddDays(1) <= end; day = day.AddDays(1))
            {
                records.Add(new NormalizedActivityRecord(
                    ProviderNamespace,
                    SourceRecordIdFor(day),
                    ActivityCategory.Walking,
                    ActivityUnits.Steps,
                    _stepsPerDay,
                    day,
                    day.AddDays(1)));
            }

            return records;
        }

        /// <summary>Stable logical identity: one walking record per UTC calendar day.</summary>
        public static string SourceRecordIdFor(DateTimeOffset dayStartUtc) =>
            "walk." + dayStartUtc.UtcDateTime.ToString("yyyyMMdd");
    }
}
