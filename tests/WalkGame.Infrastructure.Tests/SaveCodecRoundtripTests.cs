using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalkGame.Application.Persistence;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Infrastructure.Persistence;
using LandmarkId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.LandmarkIdKind>;
using ProducerId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProducerIdKind>;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;
using RegionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RegionIdKind>;
using RewardTransactionId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.RewardTransactionIdKind>;

namespace WalkGame.Infrastructure.Tests;

public sealed class SaveCodecRoundtripTests
{
    private static readonly DateTimeOffset T0 =
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid Tx1 = new Guid("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Tx2 = new Guid("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void Encode_Decode_PreservesEveryField()
    {
        var codec = NewCodec();
        var expected = MutatedNewGame();

        var encoded = codec.Encode(expected, T0.AddHours(2));
        var result = codec.Decode(encoded);

        Assert.Equal(CodecStatus.Ok, result.Status);
        Assert.NotNull(result.State);
        Assert.Equal(2, result.SourceSchemaVersion);
        Assert.Empty(result.AppliedMigrations);
        AssertEquivalentState(expected, result.State!);
    }

    [Fact]
    public void Decode_FlippedPayloadCharacter_ReportsChecksumMismatch()
    {
        var codec = NewCodec();
        var envelope = EnvelopeOf(codec.Encode(MutatedNewGame(), T0));

        var payloadBase64 = envelope["payloadBase64"]!.GetValue<string>();
        int index = payloadBase64.Length / 2;
        while (payloadBase64[index] == '=')
            index--;
        char replacement = payloadBase64[index] == 'A' ? 'B' : 'A';
        envelope["payloadBase64"] =
            payloadBase64[..index] + replacement + payloadBase64[(index + 1)..];

        var result = codec.Decode(Serialize(envelope));

        Assert.Equal(CodecStatus.ChecksumMismatch, result.Status);
        Assert.Null(result.State);
        Assert.Equal(2, result.SourceSchemaVersion);
    }

    [Fact]
    public void Decode_SchemaVersionFromTheFuture_ReportsVersionTooNew()
    {
        var codec = NewCodec();
        var envelope = EnvelopeOf(codec.Encode(MutatedNewGame(), T0));
        envelope["schemaVersion"] = 99;

        var result = codec.Decode(Serialize(envelope));

        Assert.Equal(CodecStatus.VersionTooNew, result.Status);
        Assert.Null(result.State);
        Assert.Equal(99, result.SourceSchemaVersion);
    }

    [Fact]
    public void Decode_SchemaVersionBelowMinimum_ReportsVersionTooOld()
    {
        var codec = NewCodec();
        var envelope = EnvelopeOf(codec.Encode(MutatedNewGame(), T0));
        envelope["schemaVersion"] = 0;

        var result = codec.Decode(Serialize(envelope));

        Assert.Equal(CodecStatus.VersionTooOld, result.Status);
        Assert.Null(result.State);
        Assert.Equal(0, result.SourceSchemaVersion);
    }

    [Theory]
    [InlineData("{this is not json")]
    [InlineData("")]
    public void Decode_GarbageBytes_ReportsMalformedEnvelope(string garbage)
    {
        var codec = NewCodec();

        var result = codec.Decode(Encoding.UTF8.GetBytes(garbage));

        Assert.Equal(CodecStatus.MalformedEnvelope, result.Status);
        Assert.Null(result.State);
    }

    private static SaveCodec NewCodec() =>
        new(new MigrationRunner(DefaultMigrations.All));

    private static RegionDefinition BuildRegion()
    {
        var projects = new List<ProjectDefinition>
        {
            new ProjectDefinition(new ProjectId("proj.clear-trailhead"), "Clear the old trailhead", 300L),
            new ProjectDefinition(new ProjectId("proj.build-workshop"), "Rebuild the settlement workshop", 1500L,
                prerequisites: new[] { new ProjectId("proj.clear-trailhead") }),
        };
        var landmarks = new List<LandmarkDefinition>
        {
            new LandmarkDefinition(new LandmarkId("lm.trailhead"), "Old Trailhead",
                stages: new[] { new LandmarkStageDefinition(RestorationStage.Stabilized, "proj.clear-trailhead") }),
        };
        var producers = new List<ProducerDefinition>
        {
            new ProducerDefinition(new ProducerId("prd.workshop-salvage"), "Workshop Salvage Crew",
                ResourceType.Materials, 2500L, 500L, "proj.build-workshop"),
        };
        return new RegionDefinition(new RegionId("region.millbrook-valley"), "Millbrook Valley",
            projects, landmarks, producers);
    }

    private static GameState MutatedNewGame()
    {
        var state = GameFactory.NewGame(BuildRegion(), T0, seed: 42UL);

        state.Ledger.Apply(
            new RewardTransaction(RewardTransactionId.FromGuid(Tx1), T0, 120L, "walk-1"), state.Resources);
        state.Ledger.Apply(
            new RewardTransaction(RewardTransactionId.FromGuid(Tx2), T0.AddMinutes(30), 80L, "walk-2"), state.Resources);

        string identityA = ActivityIdentity.Compute(new NormalizedActivityRecord(
            "fixture", "rec-a", ActivityCategory.Walking, ActivityUnits.Steps, 6400L,
            T0.AddMinutes(-60), T0.AddMinutes(-15)));
        string identityB = ActivityIdentity.Compute(new NormalizedActivityRecord(
            "fixture", null, ActivityCategory.Walking, ActivityUnits.Steps, 12345L,
            T0.AddMinutes(-50), T0.AddMinutes(-20)));
        state.ProcessedRecords.Record(new ProcessedRecordEntry(identityA, StepConversionRuleV1.RuleVersion, 6400L, 64L, T0));
        state.ProcessedRecords.Record(new ProcessedRecordEntry(identityB, StepConversionRuleV1.RuleVersion, 12345L, 123L, T0));
        state.IngestionCheckpointUtc = T0.AddMinutes(-15);

        state.Resources.SetCap(ResourceType.Materials, 500L);
        state.Resources.Add(ResourceType.Materials, 10L);

        foreach (var projectId in new[] { "proj.clear-trailhead", "proj.build-workshop" })
        {
            state.Region.FindProject(projectId)!.Status = ProjectStatus.Queued;
            state.Queue.QueuedProjectIds.Add(projectId);
        }

        var producer = state.Region.FindProducer("prd.workshop-salvage")!;
        producer.Unlocked = true;
        producer.StoredMilliUnits = 1234L;
        producer.TotalProducedMilliUnits = 5678L;
        producer.LastTickUtc = T0.AddHours(1);

        state.PendingReturnSummary = new WalkGame.Domain.Summaries.PendingReturnSummaryState
        {
            PrimaryNextAction = "Queue the next restoration project.",
            GeneratedAtUtc = T0.AddMinutes(45),
        };
        state.PendingReturnSummary.Items.Add(new WalkGame.Domain.Summaries.PendingSummaryItemState
        {
            Kind = WalkGame.Domain.Summaries.SummaryItemKind.Transformation,
            Text = "Old Trailhead has reached Stabilized.",
        });
        state.PendingReturnSummary.Items.Add(new WalkGame.Domain.Summaries.PendingSummaryItemState
        {
            Kind = WalkGame.Domain.Summaries.SummaryItemKind.Aggregate,
            Text = "Vitality credited: 200.",
        });

        return state;
    }

    private static void AssertEquivalentState(GameState expected, GameState actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.LastAdvancedUtc, actual.LastAdvancedUtc);

        Assert.Equal(expected.Rng.S0, actual.Rng.S0);
        Assert.Equal(expected.Rng.S1, actual.Rng.S1);
        Assert.Equal(expected.Rng.S2, actual.Rng.S2);
        Assert.Equal(expected.Rng.S3, actual.Rng.S3);

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            Assert.Equal(expected.Resources.Get(type), actual.Resources.Get(type));
        Assert.Equal(expected.Resources.Amounts.Count, actual.Resources.Amounts.Count);
        Assert.Equal(expected.Resources.Caps.Count, actual.Resources.Caps.Count);
        foreach (var pair in expected.Resources.Caps)
            Assert.Equal(pair.Value, actual.Resources.GetCap(pair.Key));

        Assert.Equal(expected.Ledger.Records.Count, actual.Ledger.Records.Count);
        for (int i = 0; i < expected.Ledger.Records.Count; i++)
        {
            var e = expected.Ledger.Records[i];
            var a = actual.Ledger.Records[i];
            Assert.Equal(e.TransactionId, a.TransactionId);
            Assert.Equal(e.OccurredAtUtc, a.OccurredAtUtc);
            Assert.Equal(e.VitalityAmount, a.VitalityAmount);
            Assert.Equal(e.Reason, a.Reason);
        }
        Assert.Equal(expected.Ledger.TotalVitalityCredited, actual.Ledger.TotalVitalityCredited);

        Assert.Equal(expected.IngestionCheckpointUtc, actual.IngestionCheckpointUtc);
        Assert.Equal(expected.ProcessedRecords.Count, actual.ProcessedRecords.Count);
        foreach (var pair in expected.ProcessedRecords.Entries)
        {
            Assert.True(actual.ProcessedRecords.Entries.TryGetValue(pair.Key, out var a));
            Assert.Equal(pair.Value.IdentityKey, a!.IdentityKey);
            Assert.Equal(pair.Value.ConversionRuleVersion, a.ConversionRuleVersion);
            Assert.Equal(pair.Value.EligibleSteps, a.EligibleSteps);
            Assert.Equal(pair.Value.VitalityCredited, a.VitalityCredited);
            Assert.Equal(pair.Value.ProcessedAtUtc, a.ProcessedAtUtc);
        }

        Assert.Equal(expected.Queue.ActiveProjectId, actual.Queue.ActiveProjectId);
        Assert.Equal(expected.Queue.AutoAdvance, actual.Queue.AutoAdvance);
        Assert.Equal(expected.Queue.QueuedProjectIds, actual.Queue.QueuedProjectIds);

        Assert.Equal(expected.Region.RegionId, actual.Region.RegionId);
        Assert.Equal(expected.Region.Projects.Count, actual.Region.Projects.Count);
        foreach (var pair in expected.Region.Projects)
        {
            var e = pair.Value;
            var a = actual.Region.FindProject(pair.Key);
            Assert.NotNull(a);
            Assert.Equal(e.ProjectId, a!.ProjectId);
            Assert.Equal(e.Status, a.Status);
            Assert.Equal(e.VitalityInvested, a.VitalityInvested);
            Assert.Equal(e.CompletedAtUtc, a.CompletedAtUtc);
        }

        Assert.Equal(expected.Region.LandmarkStages.Count, actual.Region.LandmarkStages.Count);
        foreach (var pair in expected.Region.LandmarkStages)
            Assert.Equal(pair.Value, actual.Region.LandmarkStages[pair.Key]);

        Assert.Equal(expected.Region.Producers.Count, actual.Region.Producers.Count);
        for (int i = 0; i < expected.Region.Producers.Count; i++)
        {
            var e = expected.Region.Producers[i];
            var a = actual.Region.Producers[i];
            Assert.Equal(e.ProducerId, a.ProducerId);
            Assert.Equal(e.Unlocked, a.Unlocked);
            Assert.Equal(e.StoredMilliUnits, a.StoredMilliUnits);
            Assert.Equal(e.TotalProducedMilliUnits, a.TotalProducedMilliUnits);
            Assert.Equal(e.LastTickUtc, a.LastTickUtc);
        }

        if (expected.PendingReturnSummary == null)
        {
            Assert.Null(actual.PendingReturnSummary);
        }
        else
        {
            Assert.NotNull(actual.PendingReturnSummary);
            Assert.Equal(expected.PendingReturnSummary.PrimaryNextAction, actual.PendingReturnSummary!.PrimaryNextAction);
            Assert.Equal(expected.PendingReturnSummary.GeneratedAtUtc, actual.PendingReturnSummary.GeneratedAtUtc);
            Assert.Equal(expected.PendingReturnSummary.Items.Count, actual.PendingReturnSummary.Items.Count);
            for (int i = 0; i < expected.PendingReturnSummary.Items.Count; i++)
            {
                Assert.Equal(expected.PendingReturnSummary.Items[i].Kind, actual.PendingReturnSummary.Items[i].Kind);
                Assert.Equal(expected.PendingReturnSummary.Items[i].Text, actual.PendingReturnSummary.Items[i].Text);
            }
        }
    }

    private static JsonObject EnvelopeOf(byte[] encoded) => JsonNode.Parse(encoded)!.AsObject();

    private static byte[] Serialize(JsonObject envelope) => JsonSerializer.SerializeToUtf8Bytes(envelope);
}
