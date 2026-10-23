using System;
using WalkGame.Domain.Activity;

namespace WalkGame.Application.Activity
{
    /// <summary>
    /// D-196 (KI-031): on a platform that cannot report a refused read (HealthKit), a connected player
    /// whose walks have stopped arriving for <see cref="QuietLocalDays"/> local calendar days gets one
    /// gentle hint line. The status stays Connected; no notification, no progress effect. The hint
    /// clears as soon as a sample arrives. Pure: derived from the status read model and the clock.
    /// </summary>
    public static class ActivityQuietHint
    {
        /// <summary>Local calendar days between the last arrival and today (today − arrival day).</summary>
        public const int QuietLocalDays = 3;

        public static bool Applies(ActivityStatusReadModel status, DateTimeOffset nowUtc, int localOffsetSeconds)
        {
            if (status == null || !status.ReadDenialHidden) return false;
            if (status.Status != ActivityPlayerStatus.ConnectedCurrent || !status.PermissionGranted) return false;
            if (!(status.LastSampleArrivedUtc is DateTimeOffset last)) return false;
            if (nowUtc.UtcTicks < last.UtcTicks) return false; // clock moved back: never guess
            int? today = LocalDayCalendar.DayOf(nowUtc, localOffsetSeconds);
            int? arrived = LocalDayCalendar.DayOf(last, localOffsetSeconds);
            if (today == null || arrived == null) return false;
            return today.Value - arrived.Value >= QuietLocalDays;
        }
    }
}
