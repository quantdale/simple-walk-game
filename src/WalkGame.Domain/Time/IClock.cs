using System;

namespace WalkGame.Domain.Time
{
    /// <summary>
    /// Injected time abstraction. Domain code must never call DateTime.UtcNow directly;
    /// all offline systems advance from explicit timestamps/checkpoints.
    /// </summary>
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    /// <summary>A manually controlled UTC clock for tests and headless simulation.</summary>
    public sealed class ManualClock : IClock
    {
        private DateTimeOffset _now;

        public ManualClock(DateTimeOffset startUtc)
        {
            _now = startUtc.ToUniversalTime();
        }

        public static ManualClock At(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
            new ManualClock(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));

        public DateTimeOffset UtcNow => _now;

        public void Set(DateTimeOffset utc) => _now = utc.ToUniversalTime();

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
