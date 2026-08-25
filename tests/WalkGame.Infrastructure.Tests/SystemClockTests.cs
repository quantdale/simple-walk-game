using System;
using WalkGame.Infrastructure.Platform;
using Xunit;

namespace WalkGame.Infrastructure.Tests;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_TracksWallClock_WithZeroOffset()
    {
        var clock = new SystemClock();

        var now = clock.UtcNow;

        Assert.Equal(TimeSpan.Zero, now.Offset);
        Assert.True(Math.Abs((now - DateTimeOffset.UtcNow).TotalSeconds) < 5.0);
    }
}
