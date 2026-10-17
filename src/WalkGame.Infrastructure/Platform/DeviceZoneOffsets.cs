using System;
using WalkGame.Domain.Activity;

namespace WalkGame.Infrastructure.Platform
{
    /// <summary>
    /// D-157: the device's zone offset at a given instant, used ONLY as a fallback local-day label for
    /// a walk whose source supplied no zone offset. The offset is taken from the device zone's rules AT
    /// THE RECORD'S END INSTANT (so a walk before a DST change gets the pre-change offset), never the
    /// current offset. The result is presentation metadata (days walked, "Today: N Vitality"): it is
    /// outside record identity and never changes crediting, dedup or the save schema.
    ///
    /// Fails soft: any zone error, a non-whole-second offset or one outside
    /// ±<see cref="LocalDayCalendar.MaxOffsetSeconds"/> gives null, and the walk stays undated exactly
    /// as before D-157 (it then never counts toward "Today", so the figure can only understate).
    /// </summary>
    public static class DeviceZoneOffsets
    {
        /// <summary>The device zone (<see cref="TimeZoneInfo.Local"/>), the same zone "today" is read in.</summary>
        public static Func<DateTimeOffset, int?> Local => instant => OffsetSecondsAt(TimeZoneInfo.Local, instant);

        public static Func<DateTimeOffset, int?> For(TimeZoneInfo zone)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            return instant => OffsetSecondsAt(zone, instant);
        }

        public static int? OffsetSecondsAt(TimeZoneInfo zone, DateTimeOffset instant)
        {
            if (zone == null) return null;
            TimeSpan offset;
            try { offset = zone.GetUtcOffset(instant); } // a DateTimeOffset is an unambiguous instant
            catch (Exception) { return null; }
            if (offset.Ticks % TimeSpan.TicksPerSecond != 0) return null;
            long seconds = offset.Ticks / TimeSpan.TicksPerSecond;
            if (seconds > LocalDayCalendar.MaxOffsetSeconds || seconds < -LocalDayCalendar.MaxOffsetSeconds) return null;
            return (int)seconds;
        }

        /// <summary>
        /// Applies the fallback to one parsed record: a credit with no source offset gets the device offset
        /// at its end instant; a record with a source offset, a deletion, or a null fallback is unchanged.
        /// A throwing fallback leaves the record undated (never fails the fetch).
        /// </summary>
        public static NormalizedActivityRecord WithFallbackOffset(NormalizedActivityRecord record, Func<DateTimeOffset, int?>? deviceOffsetAt)
        {
            if (deviceOffsetAt == null || record.IsDeletion || record.EndZoneOffsetSeconds != null) return record;
            int? off;
            try { off = deviceOffsetAt(record.EndUtc); }
            catch (Exception) { return record; }
            if (off == null || off.Value > LocalDayCalendar.MaxOffsetSeconds || off.Value < -LocalDayCalendar.MaxOffsetSeconds) return record;
            return record with { EndZoneOffsetSeconds = off };
        }
    }
}
