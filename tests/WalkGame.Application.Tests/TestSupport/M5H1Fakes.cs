using System;
using System.Collections.Generic;
using WalkGame.Application.Activity;
using WalkGame.Application.Ux;
using WalkGame.Domain.Activity;

namespace WalkGame.Application.Tests.TestSupport
{
    /// <summary>Mutable test double behind the same connection port native adapters will implement.</summary>
    internal sealed class FakeActivityConnectionPort : IActivityConnectionPort
    {
        public ActivityPermissionState Permission { get; set; } = ActivityPermissionState.NotRequested;

        public ActivitySourceAvailability Availability { get; set; } = ActivitySourceAvailability.Available;

        public DateTimeOffset? LastSuccessfulRefreshUtc { get; set; }

        public DateTimeOffset? LastAttemptUtc { get; set; }

        public string? TechnicalDetail { get; set; }

        public ActivityConnectionSnapshot SnapshotConnection() => new ActivityConnectionSnapshot(
            Permission, Availability, LastSuccessfulRefreshUtc, LastAttemptUtc, TechnicalDetail);
    }

    /// <summary>Deterministic record source returning a fixed batch.</summary>
    internal sealed class StaticRecordSource : WalkGame.Application.Activity.IActivityRecordSource
    {
        private readonly IReadOnlyList<NormalizedActivityRecord> _records;

        public StaticRecordSource(params NormalizedActivityRecord[] records) => _records = records;

        public StaticRecordSource(IReadOnlyList<NormalizedActivityRecord> records) => _records = records;

        public string ProviderNamespace => "test.static";

        public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc) =>
            _records;
    }

    /// <summary>Source whose fetch always throws the given exception (transient failure double).</summary>
    internal sealed class ThrowingRecordSource : WalkGame.Application.Activity.IActivityRecordSource
    {
        private readonly Exception _exception;

        public ThrowingRecordSource(Exception exception) => _exception = exception;

        public string ProviderNamespace => "test.throwing";

        public IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc) =>
            throw _exception;
    }

    internal static class M5H1Records
    {
        public static NormalizedActivityRecord Steps(
            long steps, DateTimeOffset startUtc, TimeSpan duration, int revision = 1, string? sourceId = null) =>
            new NormalizedActivityRecord(
                ProviderNamespace: "test.m5h1",
                SourceRecordId: sourceId,
                Category: ActivityCategory.Walking,
                Unit: ActivityUnits.Steps,
                Quantity: steps,
                StartUtc: startUtc,
                EndUtc: startUtc.Add(duration),
                Revision: revision);
    }
}
