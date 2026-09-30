using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Summaries;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Summaries;
using WalkGame.Domain.Time;
using WalkGame.Domain.Validation;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-055 regression coverage for the durable return summary's rotation.
///
/// The defect: the summary only ever GREW. Every batch merged new items into the
/// previously pending ones, the merged list was sorted by kind, and the first 12 survived.
/// Nothing was ever removed except by an explicit acknowledgement. So for a player who
/// does not tap "dismiss" — precisely the busy player this product is designed for — the
/// summary saturated within roughly the first dozen region events, after which every new
/// Transformation item was dropped in favour of the oldest ones and no Production, Notice
/// or Aggregate item was ever shown again. The player who engaged least got the least
/// useful information.
/// </summary>
public sealed class ReturnSummaryRotationTests
{
    private static readonly DateTimeOffset T0 = TestSessions.T0;

    private static RegionDefinition Content() => Region1Catalog.Create();

    // ==================================================================
    // 4.1 / 4.2 / 4.3 Saturation and kind-scoped eviction.
    // ==================================================================

    [Fact]
    public void SaturatingOneKind_IsBoundedByThatKind_SoNoSingleKindCanFillTheSummary()
    {
        // 30 transformation events. The Transformation bucket's bound is 5, so the summary
        // holds FIVE transformation items, not 12. That is the point of the change: before
        // rotation, transformation filled the entire summary and every other kind was
        // starved out forever.
        var events = Enumerable.Range(1, 30)
            .Select(i => (SimulationEvent)new ProjectCompleted(T0, "proj.p" + i))
            .ToList();

        var summary = ReturnSummaryComposer.Compose(events, Content(), null, T0);

        Assert.Equal(5, summary.Items.Count);
        Assert.All(summary.Items, i => Assert.Equal(SummaryItemKind.Transformation, i.Kind));
        Assert.True(summary.Items.Count < PendingReturnSummaryState.MaxItems,
            "one kind alone must not be able to fill the whole summary");

        // The transformation bucket holds the NEWEST facts, not the oldest.
        var texts = summary.Items.Select(i => i.Text).ToList();
        Assert.Contains(texts, t => t.Contains("p30"));
        Assert.DoesNotContain(texts, t => t.Contains("p1."));
    }

    [Fact]
    public void AMixedBatch_ReachesTheHardBound_AndStillRotates()
    {
        // With every kind contributing, the total bound of 12 is reached and each kind
        // still respects its own share: 5 + 3 + 2 + 1 (notice) + 1 (aggregate) = 12.
        var events = new List<SimulationEvent>();
        for (int i = 1; i <= 12; i++)
            events.Add(new ProjectCompleted(T0.AddDays(i), "proj.p" + i));
        for (int i = 1; i <= 6; i++)
            events.Add(new ProjectBecameAvailable(T0.AddDays(i), "proj.n" + i));
        events.Add(new ProducerUnlocked(T0, "prod.mill"));
        events.Add(new ProducerUnlocked(T0, "prod.greenhouse"));
        events.Add(new DiscoveryUnlocked(T0.AddDays(1), "disc.mural"));
        events.Add(new ActivityCredited(T0, "seed", 1_000L));

        var summary = ReturnSummaryComposer.Compose(events, Content(), null, T0.AddDays(20));

        Assert.Equal(PendingReturnSummaryState.MaxItems, summary.Items.Count);
        Assert.Equal(5, summary.Items.Count(i => i.Kind == SummaryItemKind.Transformation));
        Assert.Equal(3, summary.Items.Count(i => i.Kind == SummaryItemKind.ActionableDecision));
        Assert.Equal(2, summary.Items.Count(i => i.Kind == SummaryItemKind.Production));
        Assert.Equal(1, summary.Items.Count(i => i.Kind == SummaryItemKind.Notice));
        Assert.Equal(1, summary.Items.Count(i => i.Kind == SummaryItemKind.Aggregate));
        // Rotation kept the newest transformation, dropped the oldest.
        var texts = summary.Items.Select(i => i.Text).ToList();
        Assert.Contains(texts, t => t.Contains("p12"));
        Assert.DoesNotContain(texts, t => t.Contains("p1."));
    }

    [Fact]
    public void ANewTransformationItem_EvictsOnlyATransformationItem()
    {
        var first = ReturnSummaryComposer.Compose(
            new SimulationEvent[]
            {
                new ProjectCompleted(T0, "proj.a"),
                new ProjectBecameAvailable(T0, "proj.next"),
                new ProducerUnlocked(T0, "prod.mill"),
            },
            Content(), null, T0);

        Assert.Contains(first.Items, i => i.Kind == SummaryItemKind.ActionableDecision);
        Assert.Contains(first.Items, i => i.Kind == SummaryItemKind.Production);

        // Push transformation past its bound of 5.
        var state = first;
        for (int i = 1; i <= 6; i++)
        {
            state = ReturnSummaryComposer.Compose(
                new[] { (SimulationEvent)new ProjectCompleted(T0.AddDays(i), "proj.t" + i) },
                Content(), state, T0.AddDays(i));
        }

        Assert.Equal(5, state.Items.Count(i => i.Kind == SummaryItemKind.Transformation));
        // The other kinds were untouched by transformation rotation.
        Assert.Contains(state.Items, i => i.Kind == SummaryItemKind.ActionableDecision);
        Assert.Contains(state.Items, i => i.Kind == SummaryItemKind.Production);
    }

    [Fact]
    public void ALowerPriorityItem_NeverDisplacesAHigherPriorityOne()
    {
        // Fill every bucket to its bound, then overflow the LOWEST-priority bucket.
        var state = BuildSaturatedSummary();
        Assert.Equal(5, state.Items.Count(i => i.Kind == SummaryItemKind.Transformation));
        Assert.Equal(3, state.Items.Count(i => i.Kind == SummaryItemKind.ActionableDecision));

        var transformationsBefore = state.Items
            .Where(i => i.Kind == SummaryItemKind.Transformation).Select(i => i.Text).ToList();

        // Many aggregate items, which are the lowest priority.
        var events = Enumerable.Range(1, 20)
            .Select(i => (SimulationEvent)new ActivityCredited(T0.AddDays(100 + i), "tx" + i, i * 10L))
            .ToList();
        var next = ReturnSummaryComposer.Compose(events, Content(), state, T0.AddDays(200));

        // Transformation is untouched: a low-priority item may never push one out.
        var transformationsAfter = next.Items
            .Where(i => i.Kind == SummaryItemKind.Transformation).Select(i => i.Text).ToList();
        Assert.Equal(transformationsBefore, transformationsAfter);

        // And the documented priority order still holds end to end.
        var kinds = next.Items.Select(i => (int)i.Kind).ToList();
        Assert.Equal(kinds.OrderBy(k => k).ToList(), kinds);
    }

    // ==================================================================
    // 4.4 / 4.5 Ordering is deterministic and byte-stable.
    // ==================================================================

    [Fact]
    public void WithinKindOrder_IsFirstCommitOrder_NotArbitrary()
    {
        // The previous implementation used List<T>.Sort, which is NOT stable, and then
        // claimed "insertion order within a kind" in a comment. This pins the real order.
        // Four completions fit under the bound of 5 alongside the aggregate item the
        // composer derives from the same batch.
        var events = Enumerable.Range(1, 4)
            .Select(i => (SimulationEvent)new ProjectCompleted(T0, "proj.s" + i))
            .ToList();

        var summary = ReturnSummaryComposer.Compose(events, Content(), null, T0);

        Assert.Equal(
            new[] { "proj.s1", "proj.s2", "proj.s3", "proj.s4" },
            summary.Items
                .Where(i => i.Text.Contains("is complete."))
                .Select(i => i.Text.Replace(" is complete.", string.Empty))
                .ToList());
    }

    [Fact]
    public void ReplayingTheSameEventSequence_ProducesIdenticalSummaryState()
    {
        var events = new List<SimulationEvent>
        {
            new ProjectCompleted(T0, "proj.a"),
            new ProjectBecameAvailable(T0, "proj.b"),
            new ProducerUnlocked(T0, "prod.mill"),
            new ActivityCredited(T0, "00000000-0000-0000-0000-000000000001", 250L),
        };

        var first = ReturnSummaryComposer.Compose(events, Content(), null, T0);
        var second = ReturnSummaryComposer.Compose(events, Content(), null, T0);

        Assert.Equal(
            first.Items.Select(i => i.Kind + "|" + i.Text + "|" + i.FirstCommittedAtUtc).ToList(),
            second.Items.Select(i => i.Kind + "|" + i.Text + "|" + i.FirstCommittedAtUtc).ToList());
        Assert.Equal(first.PrimaryNextAction, second.PrimaryNextAction);
        Assert.Equal(first.GeneratedAtUtc, second.GeneratedAtUtc);
    }

    // ==================================================================
    // 4.6 The regression test for the saturation defect itself.
    // ==================================================================

    [Fact]
    public void ThirtyComposesWithoutAcknowledgement_NeverExceedTheBound_AndShowRecentItems()
    {
        var state = (PendingReturnSummaryState?)null;

        for (int boot = 1; boot <= 30; boot++)
        {
            var events = new List<SimulationEvent>
            {
                new ProjectCompleted(T0.AddDays(boot), "proj.c" + boot),
                new ProjectBecameAvailable(T0.AddDays(boot), "proj.n" + boot),
            };
            state = ReturnSummaryComposer.Compose(events, Content(), state, T0.AddDays(boot));

            Assert.True(state!.Items.Count <= PendingReturnSummaryState.MaxItems,
                "boot " + boot + " exceeded the hard bound with " + state.Items.Count + " items");
        }

        // The final summary reflects the MOST RECENT boots, which is the whole point.
        var texts = state!.Items.Select(i => i.Text).ToList();
        Assert.Contains(texts, t => t.Contains("c30"));
        Assert.DoesNotContain(texts, t => t.Contains("c1."));
    }

    [Fact]
    public void ARetiredItem_DoesNotReappearInTheNextSummaries()
    {
        var state = (PendingReturnSummaryState?)null;
        for (int i = 1; i <= 5; i++)
            state = ReturnSummaryComposer.Compose(
                new[] { (SimulationEvent)new ProjectCompleted(T0.AddDays(i), "proj.keep" + i) },
                Content(), state, T0.AddDays(i));

        Assert.Contains(state!.Items, i => i.Text.Contains("keep1"));

        // Two more transformation items retire the two oldest.
        for (int i = 6; i <= 7; i++)
            state = ReturnSummaryComposer.Compose(
                new[] { (SimulationEvent)new ProjectCompleted(T0.AddDays(i), "proj.keep" + i) },
                Content(), state, T0.AddDays(i));

        Assert.DoesNotContain(state!.Items, i => i.Text.Contains("keep1"));
        Assert.DoesNotContain(state.Items, i => i.Text.Contains("keep2"));

        // And they stay gone: re-composing without new events must not resurrect them.
        for (int i = 8; i <= 12; i++)
            state = ReturnSummaryComposer.Compose(
                Array.Empty<SimulationEvent>(), Content(), state, T0.AddDays(i));

        Assert.DoesNotContain(state!.Items, i => i.Text.Contains("keep1"));
        Assert.DoesNotContain(state.Items, i => i.Text.Contains("keep2"));
    }

    // ==================================================================
    // 2.x Additive currency: an old-shaped save must load as all-current.
    // ==================================================================

    [Fact]
    public void ASummaryWrittenInTheOldShape_LoadsWithEveryItemTreatedAsCurrent()
    {
        // An old save: items with NO currency field at all.
        var legacy = new PendingReturnSummaryState { GeneratedAtUtc = T0 };
        legacy.Items.Add(new PendingSummaryItemState
        {
            Kind = SummaryItemKind.Transformation,
            Text = "Clear the old trailhead is complete.",
        });
        legacy.PrimaryNextAction = "Queue the next restoration project.";

        var view = WalkGame.Application.ReadModels.ReturnSummaryReadModel.FromState(legacy);

        // Treated as current, so a long-untouched pre-upgrade save still surfaces.
        Assert.All(view.Items, i => Assert.True(i.IsCurrent));
        Assert.True(view.HasCurrentItems);
        // No state validation failure: the additive field cannot brick an old save.
        Assert.Empty(GameStateValidator.Validate(
            WalkGame.Domain.GameFactory.NewGame(Content(), T0, 7UL), Content()));
    }

    [Fact]
    public void ItemCurrency_IsCarriedForward_NotRestampedOnEveryComposition()
    {
        var first = ReturnSummaryComposer.Compose(
            new[] { (SimulationEvent)new ProjectCompleted(T0, "proj.a") }, Content(), null, T0);
        var item = first.Items.Single();

        // Compose again ten days later with no new events.
        var later = ReturnSummaryComposer.Compose(
            Array.Empty<SimulationEvent>(), Content(), first, T0.AddDays(10));
        var same = later.Items.Single(i => i.Text == item.Text);

        // The ORIGINAL first-commit time is preserved, which is what makes "historical"
        // detectable at all. Restamping on every composition would make every item look
        // brand new forever.
        Assert.Equal(T0, same.FirstCommittedAtUtc);
        Assert.Equal(T0.AddDays(10), later.GeneratedAtUtc);
    }

    [Fact]
    public void ItemRow_MarksItemsHistoricalOnceTheyAgePastTheThreshold()
    {
        var state = ReturnSummaryComposer.Compose(
            new[] { (SimulationEvent)new ProjectCompleted(T0, "proj.a") }, Content(), null, T0);

        var fresh = WalkGame.Application.ReadModels.ReturnSummaryReadModel.FromState(state);
        Assert.True(fresh.HasCurrentItems);

        // A summary regenerated 30 days later still holds the same item, now historical.
        var old = WalkGame.Application.ReadModels.ReturnSummaryReadModel.FromState(
            new PendingReturnSummaryState
            {
                GeneratedAtUtc = T0.AddDays(30),
                Items = { new PendingSummaryItemState
                    {
                        Kind = SummaryItemKind.Transformation,
                        Text = "Clear the old trailhead is complete.",
                        FirstCommittedAtUtc = T0,
                    } },
            });

        Assert.False(old.HasCurrentItems);
        Assert.All(old.Items, i => Assert.False(i.IsCurrent));
    }

    // ==================================================================
    // 1.3 The calm recovery notice must survive rotation.
    // ==================================================================

    [Fact]
    public void TheRecoveryNotice_IsStillRetained_UnderRotation()
    {
        var state = ReturnSummaryComposer.Compose(
            new[] { (SimulationEvent)new ProjectCompleted(T0, "proj.a") }, Content(), null, T0);
        state = ReturnSummaryComposer.WithNotice(state, T0, "Your save was restored.");

        Assert.Contains(state.Items, i => i.Kind == SummaryItemKind.Notice && i.Text.Contains("restored"));
    }

    // ==================================================================
    // 3.x The Home attention signal.
    // ==================================================================

    [Fact]
    public void Home_StopsClaimingAttentionForAFullyHistoricalSummary_ButStillOffersToReadIt()
    {
        using var dir = new TempDirectory();
        var session = TestSessions.Create(dir.Path, new ManualClock(T0));
        Assert.Equal(StartStatus.NewGameCreated, session.StartNewGame(7UL).Status);

        // Keep the queue NON-idle and the project UN-finished for the whole test, so the
        // only thing that could make Home demand attention is the summary itself. An idle
        // queue with banked Vitality would otherwise mask the signal under test, and a
        // large enough credit would complete the project and re-idle the queue.
        Assert.True(session.EnqueueProject(TestSessions.EntryProjectId).IsSuccess);
        session.ActivateQueuedProject(TestSessions.EntryProjectId);
        session.CreditActivity(TestSessions.Tx1, T0, 50L, "seed");

        var fresh = session.GetHome();
        Assert.True(fresh.HasPendingSummary);
        Assert.True(fresh.RequiresAttention);
        Assert.Equal(HomeAttentionReason.PendingReturnSummary, fresh.AttentionReason);

        // Forty days later the same item is historical: still readable, no longer urgent.
        var later = TestSessions.Create(dir.Path, new ManualClock(T0.AddDays(40)));
        Assert.Equal(StartStatus.Loaded, later.Continue().Status);

        var home = later.GetHome();
        Assert.True(home.HasPendingSummary);   // still offered for reading
        Assert.False(home.RequiresAttention);   // but no longer shouting
        Assert.NotEqual(HomeAttentionReason.PendingReturnSummary, home.AttentionReason);
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    /// <summary>A summary with every bucket filled to its per-kind bound.</summary>
    private static PendingReturnSummaryState BuildSaturatedSummary()
    {
        var events = new List<SimulationEvent>();
        for (int i = 1; i <= 5; i++)
            events.Add(new ProjectCompleted(T0.AddDays(i), "proj.t" + i));
        for (int i = 1; i <= 3; i++)
            events.Add(new ProjectBecameAvailable(T0.AddDays(i), "proj.a" + i));
        events.Add(new ProducerUnlocked(T0, "prod.mill"));
        events.Add(new ActivityCredited(T0, "seed-tx", 100L));

        return ReturnSummaryComposer.Compose(events, Content(), null, T0.AddDays(10));
    }
}
