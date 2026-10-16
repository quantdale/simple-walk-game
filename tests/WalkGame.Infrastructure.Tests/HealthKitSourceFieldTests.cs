using System;
using System.Linq;
using WalkGame.Infrastructure.Platform.HealthKit;
using Xunit;

namespace WalkGame.Infrastructure.Tests;

/// <summary>
/// D-114: the HealthKit bridge's optional sixth field (source kind) and the adapter's use of
/// <see cref="HealthKitSourceDedup"/>. Headless with a fake bridge; the adapter is not composed.
/// </summary>
public sealed class HealthKitSourceFieldTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

    private sealed class FakeBridge : IHealthKitBridge
    {
        public string Response = "ok";
        public string GetAvailability() => "available";
        public string GetRequestStatus() => "unnecessary";
        public string ReadSteps(long s, long e) => Response;
    }

    private static string Line(string id, DateTimeOffset s, DateTimeOffset e, long count, string? source) =>
        $"{id}\t{s.ToUnixTimeMilliseconds()}\t{e.ToUnixTimeMilliseconds()}\t{count}\t" + (source == null ? "" : "\t" + source);

    private static System.Collections.Generic.IReadOnlyList<WalkGame.Domain.Activity.NormalizedActivityRecord> Fetch(params string[] lines) =>
        new HealthKitActivityAdapter(new FakeBridge { Response = string.Join("\n", new[] { "ok" }.Concat(lines)) }, () => T0.AddDays(2))
            .FetchRecords(T0, T0.AddDays(1));

    [Theory]
    [InlineData("watch", HealthKitSourceKind.Watch)]
    [InlineData("phone", HealthKitSourceKind.Phone)]
    [InlineData("other", HealthKitSourceKind.Other)]
    [InlineData("", HealthKitSourceKind.Other)]
    public void Source_tokens_parse(string token, HealthKitSourceKind expected)
    {
        Assert.True(HealthKitActivityAdapter.TryParseSourceKind(token, out var k));
        Assert.Equal(expected, k);
    }

    [Theory]
    [InlineData("Watch")]
    [InlineData("ipad")]
    [InlineData(" watch")]
    [InlineData("5")]
    public void Unknown_source_token_fails_the_whole_fetch(string token)
    {
        Assert.Throws<InvalidOperationException>(() => Fetch(Line("a", T0, T0.AddHours(1), 10, token)));
    }

    [Fact]
    public void Five_field_lines_are_unchanged_by_D114()
    {
        var r = Fetch(Line("a", T0, T0.AddHours(1), 100, null), Line("b", T0, T0.AddHours(1), 80, null));
        Assert.Equal(new long[] { 100, 80 }, r.OrderBy(x => x.SourceRecordId).Select(x => x.Quantity));
    }

    [Fact]
    public void Watch_wins_a_full_overlap_and_the_covered_phone_sample_is_omitted()
    {
        var r = Fetch(Line("phone1", T0, T0.AddHours(1), 900, "phone"), Line("watch1", T0, T0.AddHours(1), 1000, "watch"));
        var only = Assert.Single(r);
        Assert.Equal("watch1", only.SourceRecordId);
        Assert.Equal(1000, only.Quantity);
    }

    [Fact]
    public void Partial_overlap_keeps_the_prorated_floored_share()
    {
        // Phone 0–60 min (600 steps), Watch 30–90 min: phone keeps the uncovered half → 300.
        var r = Fetch(Line("p", T0, T0.AddMinutes(60), 600, "phone"), Line("w", T0.AddMinutes(30), T0.AddMinutes(90), 500, "watch"));
        Assert.Equal(300, r.Single(x => x.SourceRecordId == "p").Quantity);
        Assert.Equal(500, r.Single(x => x.SourceRecordId == "w").Quantity);
    }

    [Fact]
    public void Deletions_pass_through_and_do_not_cover_credits()
    {
        var del = $"del\tw0\t{T0.ToUnixTimeMilliseconds()}\t{T0.AddHours(1).ToUnixTimeMilliseconds()}";
        var r = Fetch(Line("p", T0, T0.AddHours(1), 700, "phone"), del);
        Assert.Equal(700, r.Single(x => !x.IsDeletion).Quantity);
        Assert.Single(r, x => x.IsDeletion);
    }

    [Fact]
    public void Non_positive_counts_are_left_for_the_trust_pipeline()
    {
        var r = Fetch(Line("z", T0, T0.AddHours(1), 0, "phone"), Line("w", T0, T0.AddHours(1), 50, "watch"));
        Assert.Equal(0, r.Single(x => x.SourceRecordId == "z").Quantity);
    }

    [Fact]
    public void Randomised_responses_never_credit_more_than_the_bridge_reported_and_are_order_independent()
    {
        var rng = new Random(114);
        string[] kinds = { "watch", "phone", "other", "" };
        for (int round = 0; round < 200; round++)
        {
            var lines = Enumerable.Range(0, rng.Next(1, 8)).Select(i =>
            {
                var s = T0.AddMinutes(rng.Next(0, 600));
                return (Id: "s" + i, Line: Line("s" + i, s, s.AddMinutes(rng.Next(1, 120)), rng.Next(1, 3000), kinds[rng.Next(4)]), Count: 0L);
            }).ToList();
            var a = Fetch(lines.Select(l => l.Line).ToArray());
            var b = Fetch(lines.Select(l => l.Line).Reverse().ToArray());
            Assert.Equal(a.Select(x => (x.SourceRecordId, x.Quantity)), b.Select(x => (x.SourceRecordId, x.Quantity)));
            long inTotal = lines.Sum(l => long.Parse(l.Line.Split('\t')[3]));
            Assert.True(a.Sum(x => x.Quantity) <= inTotal);
            Assert.All(a, x => Assert.True(x.Quantity > 0));
        }
    }
}
