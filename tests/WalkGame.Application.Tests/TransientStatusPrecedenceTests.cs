using WalkGame.Application.Activity;
using WalkGame.Domain.Activity;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-192 (KI-030; amends D-144): when the provider's status is unreadable (TemporarilyUnavailable)
/// both adapters report the permission as NotRequested on purpose: they never claim a grant they
/// cannot see. If the last permission the adapter DID read was a grant, that is a connected player's
/// passing glitch and projects to RefreshTemporarilyFailed / RetryLater, not a "connect" prompt.
/// A never-granted player still sees "not connected"; a revoked or denied one still sees the
/// Settings guidance; neither ever shows as a refresh failure.
/// </summary>
public sealed class TransientStatusPrecedenceTests
{
    private static ActivityStatusReadModel P(ActivityPermissionState perm, ActivitySourceAvailability avail, bool processed,
        ActivityPermissionState? lastKnown, IngestionOutcomeKind last = IngestionOutcomeKind.Succeeded) =>
        ActivityStatusProjector.Project(new ActivityConnectionSnapshot(perm, avail, lastKnownPermission: lastKnown), processed, last, 0L, null);

    private const ActivityPermissionState NotRequested = ActivityPermissionState.NotRequested;
    private const ActivitySourceAvailability Unreadable = ActivitySourceAvailability.TemporarilyUnavailable;

    [Theory]
    [InlineData(true, IngestionOutcomeKind.Succeeded)]
    [InlineData(false, IngestionOutcomeKind.NeverRun)]
    [InlineData(true, IngestionOutcomeKind.SourceFetchFailed)]
    public void Unreadable_after_a_known_grant_is_a_temporary_refresh_failure(bool processed, IngestionOutcomeKind last)
    {
        var m = P(NotRequested, Unreadable, processed, ActivityPermissionState.Granted, last);
        Assert.Equal(ActivityPlayerStatus.RefreshTemporarilyFailed, m.Status);
        Assert.Equal(ActivityRecommendedAction.RetryLater, m.RecommendedAction);
        Assert.False(m.PermissionGranted, "the unseen grant is never claimed, so nothing reads");
    }

    [Fact]
    public void Unreadable_after_a_known_partial_grant_is_also_a_temporary_refresh_failure() =>
        Assert.Equal(ActivityPlayerStatus.RefreshTemporarilyFailed,
            P(NotRequested, Unreadable, true, ActivityPermissionState.PartiallyGranted).Status);

    [Theory]
    [InlineData(null)]
    [InlineData(ActivityPermissionState.NotRequested)]
    public void Never_granted_and_unreadable_still_reads_as_not_connected(ActivityPermissionState? lastKnown)
    {
        var m = P(NotRequested, Unreadable, false, lastKnown, IngestionOutcomeKind.NeverRun);
        Assert.Equal(ActivityPlayerStatus.PermissionNeeded, m.Status);
        Assert.Equal(ActivityRecommendedAction.Connect, m.RecommendedAction);
    }

    [Theory]
    [InlineData(ActivityPermissionState.Revoked)]
    [InlineData(ActivityPermissionState.Denied)]
    public void Revoked_or_denied_then_unreadable_still_points_to_settings_never_a_refresh_failure(ActivityPermissionState lastKnown)
    {
        var m = P(NotRequested, Unreadable, true, lastKnown);
        Assert.Equal(ActivityPlayerStatus.PermissionDenied, m.Status);
        Assert.Equal(ActivityRecommendedAction.OpenSettings, m.RecommendedAction);
    }

    [Theory]
    [InlineData(ActivityPermissionState.Denied)]
    [InlineData(ActivityPermissionState.Revoked)]
    public void A_current_refusal_outranks_a_transient_failure_whatever_was_known(ActivityPermissionState perm) =>
        Assert.Equal(ActivityPlayerStatus.PermissionDenied, P(perm, Unreadable, true, ActivityPermissionState.Granted).Status);

    [Fact]
    public void A_known_grant_does_not_change_an_available_not_requested_state()
    {
        // Readable status says NotRequested now: the player (or OS) reset it; asking again is right.
        var m = P(NotRequested, ActivitySourceAvailability.Available, true, ActivityPermissionState.Granted);
        Assert.Equal(ActivityPlayerStatus.PermissionNeeded, m.Status);
    }

    [Theory]
    [InlineData(ActivitySourceAvailability.Unsupported, ActivityPlayerStatus.SourceUnavailable)]
    [InlineData(ActivitySourceAvailability.ProviderUpdateRequired, ActivityPlayerStatus.ProviderUpdateRequired)]
    public void Missing_or_outdated_providers_keep_their_precedence(ActivitySourceAvailability avail, ActivityPlayerStatus expected) =>
        Assert.Equal(expected, P(NotRequested, avail, true, ActivityPermissionState.Granted).Status);
}
