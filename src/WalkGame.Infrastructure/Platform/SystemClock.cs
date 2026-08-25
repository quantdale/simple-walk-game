using System;
using WalkGame.Domain.Time;

namespace WalkGame.Infrastructure.Platform
{
    /// <summary>Platform wall-clock adapter. The only place DateTime.UtcNow is allowed.</summary>
    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
