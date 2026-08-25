using System;
using System.Collections.Generic;
using WalkGame.Domain.Activity;

namespace WalkGame.Application.Activity
{
    /// <summary>
    /// Platform-neutral activity source port (TECHNICAL_ARCHITECTURE §3.2/§19): the narrow
    /// seam every provider passes through — fixture reader today, Health Connect/HealthKit
    /// adapters later. Implementations fetch normalized records only; they never compute
    /// Vitality, touch balances or write state. The trust pipeline downstream of this port
    /// is identical for development fixtures and production platforms.
    /// </summary>
    public interface IActivityRecordSource
    {
        /// <summary>Stable provider namespace stamped onto every record from this source.</summary>
        string ProviderNamespace { get; }

        /// <summary>
        /// Returns normalized records whose intervals lie within [windowStartUtc, windowEndUtc).
        /// Must be deterministic per source window so replays produce identical records.
        /// </summary>
        IReadOnlyList<NormalizedActivityRecord> FetchRecords(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc);
    }
}
