using System;

namespace WalkGame.Application.ReadModels
{
    /// <summary>D-198: how long the player was away, as a welcome-line tier. Never a penalty.</summary>
    public enum AbsenceTier
    {
        None = 0,
        FewDays = 1,
        Long = 2,
    }

    /// <summary>
    /// D-198: pure classification of the gap between the last offline advance and this boot or
    /// resume. Session-only (not saved). A backward clock is never a "long absence": it yields
    /// <see cref="AbsenceTier.None"/>, so a clock jump can never produce a welcome-back line.
    /// </summary>
    public static class AbsenceWelcome
    {
        public static readonly TimeSpan FewDaysThreshold = TimeSpan.FromDays(2);
        public static readonly TimeSpan LongThreshold = TimeSpan.FromDays(14);

        public static AbsenceTier Classify(DateTimeOffset lastAdvancedUtc, DateTimeOffset nowUtc)
        {
            if (nowUtc <= lastAdvancedUtc) return AbsenceTier.None;
            var away = nowUtc - lastAdvancedUtc;
            if (away >= LongThreshold) return AbsenceTier.Long;
            if (away >= FewDaysThreshold) return AbsenceTier.FewDays;
            return AbsenceTier.None;
        }
    }
}
