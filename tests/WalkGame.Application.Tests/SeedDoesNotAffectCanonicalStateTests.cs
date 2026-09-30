using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-057: an HONESTY test for the seed parameter.
///
/// <c>StartNewGame(ulong seed)</c> seeds <c>GameState.Rng</c>, which the save codec
/// round-trips field by field and <c>dump</c> prints. Nothing in the product reads it:
/// every consumer of the generator is a test or a diagnostic. So the seed is written to
/// the save and read back, and that is all — two profiles run with different seeds reach
/// IDENTICAL canonical state.
///
/// That is not a bug to be quietly tolerated and not a defect to paper over. It is the
/// current, honest property of the system, and this test pins it so that the FIRST
/// canonical consumer has to revisit seed semantics deliberately, and to notice this test
/// failing, rather than silently changing what a seed means.
///
/// Both runs are driven through the REAL trust pipeline and compared deeply, so the claim
/// is about canonical state and not about one incidental field.
/// </summary>
public sealed class SeedDoesNotAffectCanonicalStateTests : IDisposable
{
    private const ulong SeedA = 7UL;
    private const ulong SeedB = 99_991UL;

    private readonly TempDirectory _tempA = new();
    private readonly TempDirectory _tempB = new();

    public void Dispose()
    {
        _tempA.Dispose();
        _tempB.Dispose();
    }

    [Fact]
    public void TwoProfilesWithDifferentSeeds_ReachIdenticalCanonicalState()
    {
        var stateA = RunProfile(_tempA, SeedA);
        var stateB = RunProfile(_tempB, SeedB);

        // The seeds really are different, and really are persisted differently.
        Assert.NotEqual(SeedA, SeedB);
        Assert.NotEqual(stateA.Rng.S0, stateB.Rng.S0);

        // ...and that is the ONLY difference. Everything the player can observe is equal.
        Assert.Equal(
            CanonicalJson(stateA), CanonicalJson(stateB));

        // Spelled out on the fields a player would actually notice.
        Assert.Equal(stateA.Resources.Get(WalkGame.Domain.Economy.ResourceType.Vitality),
            stateB.Resources.Get(WalkGame.Domain.Economy.ResourceType.Vitality));
        Assert.Equal(stateA.Ledger.TotalVitalityCredited, stateB.Ledger.TotalVitalityCredited);
        Assert.Equal(stateA.Region.IsCompleted, stateB.Region.IsCompleted);
        Assert.Equal(
            stateA.Region.Projects.Values.Count(p => p.Status == ProjectStatus.Completed),
            stateB.Region.Projects.Values.Count(p => p.Status == ProjectStatus.Completed));
        Assert.Equal(stateA.Queue.QueuedProjectIds, stateB.Queue.QueuedProjectIds);
        Assert.Equal(stateA.ProcessedRecords.Count, stateB.ProcessedRecords.Count);
    }

    /// <summary>
    /// A fixed 24-day profile through the real ingestion pipeline, with queue choices
    /// driven deterministically by the day number. Uses no wall clock and no ordering
    /// dependency, so the test is itself deterministic.
    /// </summary>
    private static GameState RunProfile(TempDirectory dir, ulong seed)
    {
        var content = Region1Catalog.Create();
        var clock = new ManualClock(TestSessions.T0);

        var bootstrap = TestSessions.Create(dir.Path, clock);
        Assert.Equal(StartStatus.NewGameCreated, bootstrap.StartNewGame(seed).Status);

        for (int day = 1; day <= 24; day++)
        {
            var at = TestSessions.T0.AddDays(day);
            var session = TestSessions.Create(dir.Path, new ManualClock(at));
            Assert.Equal(StartStatus.Loaded, session.Continue().Status);

            var record = new NormalizedActivityRecord(
                "seed.probe", "day-" + day, ActivityCategory.Walking,
                ActivityUnits.Steps, 10_000L,
                at.AddHours(-8), at);
            session.IngestActivityBatch(new[] { record });

            // Deterministic auto-player: queue the first available project when idle.
            var home = session.GetHome();
            if (home.ActiveProjectId == null && home.Queued.Count == 0)
            {
                foreach (var definition in content.Projects)
                {
                    if (session.EnqueueProject(definition.Id.Value).IsSuccess)
                        break;
                }
            }
        }

        var final = TestSessions.Create(dir.Path, new ManualClock(TestSessions.T0.AddDays(24)));
        Assert.Equal(StartStatus.Loaded, final.Continue().Status);
        Assert.Empty(GameStateValidator.Validate(Decode(dir.Path), content));

        return Decode(dir.Path);
    }

    /// <summary>
    /// Canonical JSON with the RNG state REMOVED, so the comparison is over everything a
    /// player can observe and not over the one field we already know differs.
    /// </summary>
    private static string CanonicalJson(GameState state)
    {
        var decoded = TestSessions.NewCodec()
            .Decode(TestSessions.NewCodec().Encode(state, TestSessions.T0.AddDays(24)));
        Assert.Equal(CodecStatus.Ok, decoded.Status);

        var node = JsonNode.Parse(JsonSerializer.Serialize(decoded.State!,
            new JsonSerializerOptions { WriteIndented = false }))!.AsObject();
        node.Remove("rng");

        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static GameState Decode(string directory)
    {
        var decoded = TestSessions.NewCodec()
            .Decode(File.ReadAllBytes(Path.Combine(directory, "save.json")));
        Assert.Equal(CodecStatus.Ok, decoded.Status);
        return decoded.State!;
    }
}
