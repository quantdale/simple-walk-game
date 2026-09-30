using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Expeditions;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;
using DiscoveryId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.DiscoveryIdKind>;
using ExpeditionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ExpeditionIdKind>;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-059: a save must be GATED against the authored content version it was written for.
///
/// <c>RegionDefinition.ContentVersion</c> existed, was set to 2, and was read by nothing.
/// The consequence is a specific, unrecoverable failure: canonical state fails CLOSED on
/// unknown content — which is correct, and is exactly what makes a content rename
/// dangerous. Rename one project, or drop a discovery an existing save unlocked, and
/// every existing save fails validation on boot; both generations are unreadable and the
/// one-generation backup holds the same invalid content, so it fails too.
///
/// These tests cover the three things that prevent it: recording the version, gating on
/// it at boot, and resolving retired identifiers to authored tombstones instead of letting
/// them become unresolvable references.
/// </summary>
public sealed class ContentVersionGateTests : IDisposable
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // ==================================================================
    // 1. The version is recorded at CREATION and never rewritten.
    // ==================================================================

    [Fact]
    public void ANewGame_RecordsTheContentVersionItWasCreatedAgainst()
    {
        var content = Region1Catalog.Create();
        var game = GameFactory.NewGame(content, T0, seed: 7UL);

        Assert.Equal(content.ContentVersion, game.AuthoredContentVersion);
    }

    [Fact]
    public void TheRecordedVersionIsNotRewrittenByLaterCommits()
    {
        var content = Region1Catalog.Create();
        var session = TestSessions.Create(_temp, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);

        long recorded = Decode().AuthoredContentVersion!.Value;
        Assert.Equal(content.ContentVersion, recorded);

        // Many commits later, the profile must still say which content it was MADE with.
        for (int i = 1; i <= 5; i++)
        {
            var later = TestSessions.Create(_temp, new ManualClock(T0.AddDays(i)));
            later.Continue();
            later.CreditActivity(Guid.NewGuid(), T0.AddDays(i), 5_000L, "work");
        }

        Assert.Equal(recorded, Decode().AuthoredContentVersion!.Value);
    }

    [Fact]
    public void NoSchemaBumpWasNeeded_TheFieldIsAdditive()
    {
        // Absence must decode to null (unknown), NOT fail. That is what makes the field
        // additive: no ISaveMigration, no SchemaVersions bump.
        var content = Region1Catalog.Create();
        var game = GameFactory.NewGame(content, T0, seed: 7UL);

        var payload = PayloadSurgery.OpenPayload(
            TestSessions.NewCodec().Encode(game, T0));
        payload.AsObject().Remove("authoredContentVersion");

        var stripped = TestSessions.NewCodec()
            .Decode(PayloadSurgery.WrapPayload(payload, SchemaVersions.Current, T0));

        Assert.Equal(CodecStatus.Ok, stripped.Status);
        Assert.Null(stripped.State!.AuthoredContentVersion);
    }

    // ==================================================================
    // 4. Boot classification.
    // ==================================================================

    [Fact]
    public void Boot_ReportsExactMatch_ForASaveCreatedByThisBuild()
    {
        Bootstrap();
        var session = Session();
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);

        var diag = session.GetDiagnostics();
        Assert.Equal(ContentCompatibility.ExactMatch, diag.ContentCompatibility);
        Assert.Equal(Region1Catalog.Create().ContentVersion, diag.SaveContentVersion);
        Assert.Equal(Region1Catalog.Create().ContentVersion, diag.BuildContentVersion);
    }

    [Fact]
    public void Boot_ReportsCompatibleOlder_ForASaveFromAnEarlierContentVersion()
    {
        Bootstrap();
        // Rewrite only the recorded version, as an older build would have written it.
        var game = Decode();
        game.AuthoredContentVersion = 1;
        Persist(game);

        var session = Session();
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);

        var diag = session.GetDiagnostics();
        Assert.Equal(ContentCompatibility.CompatibleOlder, diag.ContentCompatibility);
        Assert.Equal(1, diag.SaveContentVersion);
    }

    [Fact]
    public void Boot_FailsClosed_ForASaveFromANewerContentVersion()
    {
        Bootstrap();
        var game = Decode();
        game.AuthoredContentVersion = Region1Catalog.Create().ContentVersion + 5;
        Persist(game);

        byte[] before = File.ReadAllBytes(_temp.FilePath("save.json"));
        var session = Session();
        var result = session.Continue();

        Assert.Equal(StartStatus.ContentIncompatible, result.Status);
        Assert.Contains("content version", result.Detail!, StringComparison.OrdinalIgnoreCase);

        var diag = session.GetDiagnostics();
        Assert.Equal(DiagnosticsBootOutcome.ContentIncompatible, diag.BootOutcome);
        Assert.Equal(ContentCompatibility.NewerThanBuild, diag.ContentCompatibility);

        // The save is NOT rewritten: failing closed must not "fix" the profile.
        Assert.True(before.AsSpan().SequenceEqual(File.ReadAllBytes(_temp.FilePath("save.json"))),
            "a content-incompatible save must never be rewritten");
    }

    [Fact]
    public void AnUnrecordedVersion_ClassifiesAsExactMatch_NotAsNewer()
    {
        Bootstrap();
        var game = Decode();
        game.AuthoredContentVersion = null; // a pre-D-059 save
        Persist(game);

        var session = Session();
        Assert.Equal(StartStatus.Loaded, session.Continue().Status);

        // Absent means "unknown", and the safe reading of unknown is the OLDEST version,
        // never a false "newer than this build".
        Assert.Equal(ContentCompatibility.ExactMatch, session.GetDiagnostics().ContentCompatibility);
    }

    [Fact]
    public void TheTwoVersionAxesAreReportedIndependently()
    {
        Bootstrap();
        // A save on the CURRENT payload schema but authored against a NEWER content
        // version. The codec is entirely happy; only the content relationship is wrong.
        // If the two axes were conflated, this would be misreported as a schema problem
        // and the corrective action ("migrate the payload") would be the wrong one.
        var game = Decode();
        game.AuthoredContentVersion = Region1Catalog.Create().ContentVersion + 1;
        Persist(game);

        var session = Session();
        Assert.Equal(StartStatus.ContentIncompatible, session.Continue().Status);

        var diag = session.GetDiagnostics();
        // Content axis: rejected, and named.
        Assert.Equal(ContentCompatibility.NewerThanBuild, diag.ContentCompatibility);
        // Payload axis: no codec failure whatsoever — the envelope decoded perfectly.
        Assert.Equal(CodecFailureCategory.None, diag.LastBootCodecFailure);
        Assert.Empty(diag.AppliedMigrationsAtBoot);
        // And it is NOT reported as any of the schema/version-decode classes.
        Assert.NotEqual(StartStatus.SaveUnreadable, StartStatus.ContentIncompatible);
        Assert.Equal(DiagnosticsBootOutcome.ContentIncompatible, diag.BootOutcome);
    }

    // ==================================================================
    // 2. Authored tombstones resolve retired references.
    // ==================================================================

    [Fact]
    public void ShippedRegion1_AuthorsNoTombstones_NothingIsRetired()
    {
        Assert.Empty(Region1Catalog.Create().Tombstones);
    }

    [Fact]
    public void ATombstonedProject_ResolvesInsteadOfFailingClosed()
    {
        // The scenario this mechanism exists for: content was RENAMED, and an existing
        // save still references the old identifier.
        var content = TombstonedRegion(new ContentTombstone("proj.old-name", "Renamed to proj.clear-trailhead", 3));
        var game = GameFactory.NewGame(Region1Catalog.Create(), T0, seed: 7UL);
        game.Region.Projects["proj.old-name"] = new ProjectState
        {
            ProjectId = "proj.old-name",
            Status = ProjectStatus.Completed,
            VitalityInvested = 300L,
            CompletedAtUtc = T0,
        };

        var violations = GameStateValidator.Validate(game, content);

        Assert.DoesNotContain(violations, v => v.Contains("unknown to content"));
        Assert.Empty(violations);
    }

    [Fact]
    public void AnIdentifierThatIsNeitherLiveNorTombstoned_StillFailsClosed()
    {
        // Tombstones must not become a general escape hatch: an identifier nobody declared
        // is still unknown, and still fails closed.
        var content = TombstonedRegion();
        var game = GameFactory.NewGame(Region1Catalog.Create(), T0, seed: 7UL);
        game.Region.Projects["proj.never-existed"] = new ProjectState
        {
            ProjectId = "proj.never-existed",
            Status = ProjectStatus.Available,
        };

        var violations = GameStateValidator.Validate(game, content);

        Assert.Contains(violations, v => v.Contains("proj.never-existed") && v.Contains("unknown"));
    }

    [Fact]
    public void ATombstonedProject_GrantsNoFurtherProgression()
    {
        // "Resolves" must not mean "still works". A retired project must not sit Active
        // (which is how progress is granted).
        var content = TombstonedRegion(new ContentTombstone("proj.old-name", "Renamed", 3));
        var game = GameFactory.NewGame(Region1Catalog.Create(), T0, seed: 7UL);
        game.Region.Projects["proj.old-name"] = new ProjectState
        {
            ProjectId = "proj.old-name",
            Status = ProjectStatus.Active,
            VitalityInvested = 0L,
            CompletedAtUtc = null,
        };

        var violations = GameStateValidator.Validate(game, content);

        Assert.Contains(violations, v => v.Contains("proj.old-name") && v.Contains("tombstoned"));
    }

    [Fact]
    public void ATombstonedProducer_MayNotUnlockOrHoldOutput()
    {
        var content = TombstonedRegion(
            new ContentTombstone("prod.retired", "Retired in content v3", 3));

        var game = GameFactory.NewGame(Region1Catalog.Create(), T0, seed: 7UL);
        game.Region.Producers.Add(new ProducerRuntimeState
        {
            ProducerId = "prod.retired",
            Unlocked = true,
            StoredMilliUnits = 5_000L,
            TotalProducedMilliUnits = 5_000L,
            LastTickUtc = T0,
        });

        var violations = GameStateValidator.Validate(game, content);

        Assert.Contains(violations, v => v.Contains("prod.retired") && v.Contains("unlocked"));
    }

    [Fact]
    public void ATombstonedExpedition_MayNotBeRecordedAsCompleted()
    {
        // Completion is what pays an expedition's reward, so a retired expedition recorded
        // as completed is exactly the "grants something" case.
        var content = TombstonedRegion(
            new ContentTombstone("exp.retired", "Route removed", 3));

        var game = GameFactory.NewGame(Region1Catalog.Create(), T0, seed: 7UL);
        game.Region.Expeditions["exp.retired"] = new ExpeditionRuntimeState
        {
            ExpeditionId = "exp.retired",
            AvailableAtUtc = T0,
            CompletedAtUtc = T0.AddDays(1),
        };

        var violations = GameStateValidator.Validate(game, content);

        Assert.Contains(violations, v => v.Contains("exp.retired") && v.Contains("completed"));
    }

    // ==================================================================
    // 3. Content validation rules for tombstones.
    // ==================================================================

    [Fact]
    public void ContentValidator_AcceptsAValidTombstoneSet()
    {
        Assert.Empty(ContentValidator.Validate(TombstonedRegion(
            new ContentTombstone("proj.retired-cleanly", "Renamed to proj.a", 3))));
    }

    [Fact]
    public void ContentValidator_RejectsAnIdentifierThatIsBothLiveAndTombstoned()
    {
        // Use a REAL live Region 1 project id: a tombstone that also names live content is
        // dead weight, and worse, it would let a reference silently resolve to "retired" for
        // content that is supposed to be playable.
        const string liveProject = "proj.clear-trailhead";
        Assert.NotNull(Region1Catalog.Create().FindProject(liveProject));

        var violations = ContentValidator.Validate(TombstonedRegion(
            new ContentTombstone(liveProject, "Also still live", 3)));

        Assert.Contains(violations, v =>
            v.Contains(liveProject) && v.Contains("both a live definition and a content tombstone"));
    }

    [Fact]
    public void ContentValidator_RejectsALiveProjectDependingOnATombstonedPrerequisite()
    {
        var content = new RegionDefinition(
            new RegionId("region.dep"), "Dep",
            new[]
            {
                new ProjectDefinition(new ProjectId("proj.retired-dep"), "Retired", 100L),
                new ProjectDefinition(new ProjectId("proj.live"), "Live", 100L,
                    prerequisites: new[] { new ProjectId("proj.retired-dep") }),
            },
            Array.Empty<LandmarkDefinition>(), Array.Empty<ProducerDefinition>(),
            tombstones: new[] { new ContentTombstone("proj.retired-dep", "Removed", 3) });

        var violations = ContentValidator.Validate(content);

        Assert.Contains(violations, v =>
            v.Contains("proj.live") && v.Contains("tombstoned prerequisite"));
    }

    [Fact]
    public void ATombstoneWithoutAReason_IsRejectedAtConstruction()
    {
        // An unattributed silent removal is the exact class of change the mechanism exists
        // to make reviewable, so it cannot be constructed in the first place.
        Assert.Throws<ArgumentException>(() => new ContentTombstone("proj.x", ""));
        Assert.Throws<ArgumentException>(() => new ContentTombstone("", "reason"));
    }

    [Fact]
    public void ShippedRegion1_StillValidatesWithZeroViolations()
    {
        Assert.Empty(ContentValidator.Validate(Region1Catalog.Create()));
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    /// <summary>Region 1's real content, with an extra tombstone set layered on.</summary>
    private static RegionDefinition TombstonedRegion(params ContentTombstone[] extra) =>
        Region1Catalog.Create(extra);

    private void Bootstrap() =>
        Assert.Equal(StartStatus.NewGameCreated,
            TestSessions.Create(_temp, new ManualClock(T0)).StartNewGame(7UL).Status);

    private GameSession Session() =>
        TestSessions.Create(_temp, new ManualClock(T0.AddDays(2)));

    private GameState Decode()
    {
        var decoded = TestSessions.NewCodec()
            .Decode(File.ReadAllBytes(_temp.FilePath("save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }

    private void Persist(GameState state) =>
        new WalkGame.Infrastructure.Persistence.AtomicFileSaveStore(_temp.Path)
            .WriteAtomic(TestSessions.NewCodec().Encode(state, T0));
}
