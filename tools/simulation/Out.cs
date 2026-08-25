using System;
using System.IO;
using System.Linq;
using WalkGame.Application.ReadModels;
using WalkGame.Domain;
using WalkGame.Domain.Activity;
using WalkGame.Domain.Economy;

internal static class Out
{
    public static string HomeLine(HomeReadModel home) =>
        $"home: region={home.RegionTitleKey} vitality={home.Vitality} materials={home.Materials} knowledge={home.Knowledge}"
        + $" completed={home.CompletedProjects}/{home.TotalProjects} active={ActiveText(home)} queued={QueueText(home)}";

    public static void HomeBlock(HomeReadModel home)
    {
        Console.WriteLine("region=" + home.RegionTitleKey);
        Console.WriteLine($"vitality={home.Vitality} materials={home.Materials} knowledge={home.Knowledge}");
        Console.WriteLine(home.ActiveProjectId == null
            ? "active=none"
            : $"active={home.ActiveProjectId} invested={home.ActiveProjectInvested}/{home.ActiveProjectCost}");
        Console.WriteLine("queued=" + QueueText(home));
        Console.WriteLine($"completed={home.CompletedProjects}/{home.TotalProjects}");
        Console.WriteLine("landmarks=" + string.Join(",", home.Landmarks.Select(l => $"{l.LandmarkId}={l.Stage}")));
    }

    public static void Dump(GameState state)
    {
        Field("schema", state.SchemaVersion.ToString());
        Field("created", state.CreatedAtUtc.ToString("O"));
        Field("lastAdvanced", state.LastAdvancedUtc.ToString("O"));
        Console.WriteLine(
            $"balances: vitality={state.Resources.Get(ResourceType.Vitality)}"
            + $" materials={state.Resources.Get(ResourceType.Materials)}"
            + $" knowledge={state.Resources.Get(ResourceType.Knowledge)}");

        var records = state.Ledger.Records;
        Console.WriteLine($"ledger: count={records.Count} totalCredited={state.Ledger.TotalVitalityCredited}");
        if (records.Count > 0)
            Record("  first", records[0]);
        if (records.Count > 1)
            Record("  last ", records[^1]);

        Console.WriteLine("projects:");
        foreach (var pair in state.Region.Projects.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var project = pair.Value;
            Console.WriteLine(
                $"  {pair.Key.PadRight(24)} {project.Status.ToString().PadRight(10)}"
                + $" invested={project.VitalityInvested}"
                + $" completedAt={(project.CompletedAtUtc?.ToString("O") ?? "-")}");
        }

        string queuedIds = state.Queue.QueuedProjectIds.Count == 0
            ? "-"
            : string.Join(",", state.Queue.QueuedProjectIds);
        Console.WriteLine($"queue: active={(state.Queue.ActiveProjectId ?? "-")} queued=[{queuedIds}] autoAdvance={state.Queue.AutoAdvance}");

        Console.WriteLine("landmarks:");
        foreach (var pair in state.Region.LandmarkStages.OrderBy(p => p.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {pair.Key.PadRight(24)} {pair.Value}");

        Console.WriteLine("producers:");
        foreach (var producer in state.Region.Producers)
            Console.WriteLine(
                $"  {producer.ProducerId.PadRight(24)}"
                + $" unlocked={producer.Unlocked}"
                + $" totalMilli={producer.TotalProducedMilliUnits}"
                + $" carry={producer.CarryMilliUnits}");

        Console.WriteLine(
            $"rng: S0={state.Rng.S0:X16} S1={state.Rng.S1:X16} S2={state.Rng.S2:X16} S3={state.Rng.S3:X16}");
    }

    private static string ActiveText(HomeReadModel home) =>
        home.ActiveProjectId == null
            ? "none"
            : $"{home.ActiveProjectId}({home.ActiveProjectInvested}/{home.ActiveProjectCost})";

    private static string QueueText(HomeReadModel home) =>
        home.Queued.Count == 0 ? "none" : string.Join(",", home.Queued.Select(q => q.ProjectId));

    private static void Field(string label, string value) =>
        Console.WriteLine((label + ":").PadRight(14) + value);

    private static void Record(string label, LedgerRecord record) =>
        Console.WriteLine($"{label}: {record.TransactionId} at={record.OccurredAtUtc:O} amount={record.VitalityAmount} reason=\"{record.Reason}\"");
}
