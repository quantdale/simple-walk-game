using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Summaries;
using WalkGame.Application.Tests.TestSupport;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Summaries;

namespace WalkGame.Application.Tests;

/// <summary>
/// D-056: an ENUMERATION GUARD against the class of gap this repository has actually
/// hit twice.
///
/// <c>GameSession</c> is the entire application contract the future shell will call. Two
/// of its public mutating operations — <c>ReorderQueue</c> and <c>DequeueProject</c> —
/// had zero test references anywhere in the repository, and they are exactly the two a
/// drag-to-reorder queue UI and an "un-queue" button would wire up first. Writing tests
/// for the two known gaps fixes those two; this guard is what stops the third from
/// shipping. A gap nobody looks for is a gap that grows.
///
/// The guard never INVOKES an operation to check it — it only reads names and source
/// text — so it cannot mutate state or throw for the wrong reason.
///
/// PROOF (task 5.5, "do not ship an unproven guard"): a public parameterless, non-void
/// use case whose name appears nowhere in the test sources was temporarily added to
/// <c>GameSession</c>. The guard failed, naming that member and the directory expected to
/// contain its test; the method was then removed and the guard returned to green. The
/// transcript is in <c>docs/DECISIONS.md</c> D-056 — deliberately NOT here: naming the
/// probe in THIS file would make the guard find it in the very sources it scans and pass
/// vacuously. That self-reference was the guard's first observed failure mode, and it is
/// why the proof lives in the decision log rather than beside the code.
/// </summary>
public sealed class UseCaseCoverageGuardTests
{
    /// <summary>
    /// Members the guard deliberately does not require a test for, each with the reason.
    /// An entry here is a reviewable act, not a silent exemption.
    /// </summary>
    private static readonly Dictionary<string, string> AllowList = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Pure projections of an already-loaded state; every one of these IS asserted
        // somewhere, but the guard cannot tell a Get* that is a projection from one that
        // is a use case, and listing them individually would be noise that rots.
        // Their behaviour is covered by the read-model and acceptance suites.
    };

    /// <summary>
    /// Members that MUTATE durable state, or that drive a boot/ingest, and so must never
    /// be invoked by a guard. They are covered by behaviour tests that set up real state.
    /// </summary>
    private static bool IsMutatingOrBoot(string name) =>
        name.StartsWith("Set", StringComparison.Ordinal)
        || name.StartsWith("Enqueue", StringComparison.Ordinal)
        || name.StartsWith("Dequeue", StringComparison.Ordinal)
        || name.StartsWith("Activate", StringComparison.Ordinal)
        || name.StartsWith("Reorder", StringComparison.Ordinal)
        || name.StartsWith("Mark", StringComparison.Ordinal)
        || name.StartsWith("Advance", StringComparison.Ordinal)
        || name.StartsWith("Acknowledge", StringComparison.Ordinal)
        || name == "StartNewGame"
        || name == "Continue"
        || name == "CreditActivity"
        || name == "IngestActivityBatch"
        || name == "IngestFromSource";

    [Fact]
    public void EveryPublicSideEffectFreeUseCase_IsReferencedSomewhereInTheTestSources()
    {
        var sources = ReadTestSources();
        Assert.NotEmpty(sources);

        var uncovered = new List<string>();

        foreach (var method in typeof(GameSession)
                     .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName) continue;                 // property/event accessors
            if (method.DeclaringType != typeof(GameSession)) continue;
            if (method.IsGenericMethodDefinition) continue;
            if (method.ReturnType == typeof(void)) continue;     // a void method mutates

            string name = method.Name;
            if (IsMutatingOrBoot(name)) continue;
            if (AllowList.ContainsKey(name)) continue;

            if (!sources.Any(path => FileContains(path, name)))
                uncovered.Add(name + " (" + Describe(method) + ") — expected a test in tests/WalkGame.Application.Tests/");
        }

        Assert.True(
            uncovered.Count == 0,
            "public use cases with no test reference anywhere in the test sources:\n  - "
            + string.Join("\n  - ", uncovered));
    }

    [Fact]
    public void TheTwoHistoricallyUntestedUseCases_AreNowReferenced()
    {
        // The specific gap this change closes, asserted directly so a future refactor that
        // drops the coverage is caught by name rather than by accident.
        var sources = ReadTestSources();

        Assert.True(sources.Any(path => FileContains(path, "ReorderQueue")),
            "ReorderQueue must be referenced by a test");
        Assert.True(sources.Any(path => FileContains(path, "DequeueProject")),
            "DequeueProject must be referenced by a test");
    }

    // ==================================================================
    // D-056 §3: ReturnSummaryComposer.WithNotice had no DIRECT test. It was
    // reached only indirectly through backup recovery, where its output was
    // asserted as a COUNT rather than as content.
    // ==================================================================

    private static readonly DateTimeOffset T = TestSessions.T0;

    [Fact]
    public void WithNotice_OnNullState_ProducesAValidSingleItemSummary()
    {
        var summary = ReturnSummaryComposer.WithNotice(null, T, "Your save was restored.");

        Assert.Single(summary.Items);
        Assert.Equal(SummaryItemKind.Notice, summary.Items[0].Kind);
        Assert.Equal("Your save was restored.", summary.Items[0].Text);
        Assert.Equal(T, summary.GeneratedAtUtc);
        Assert.Null(summary.PrimaryNextAction);
    }

    [Fact]
    public void WithNotice_KeepsExistingItems_AndPlacesTheNoticeAtItsDocumentedPriority()
    {
        var seeded = ReturnSummaryComposer.Compose(
            new Domain.Simulation.SimulationEvent[]
            {
                new Domain.Simulation.ProjectCompleted(T, "proj.a"),
                new Domain.Simulation.ProjectBecameAvailable(T, "proj.next"),
            },
            Region1Catalog.Create(), null, T);

        Assert.Contains(seeded.Items, i => i.Kind == SummaryItemKind.Transformation);
        Assert.Contains(seeded.Items, i => i.Kind == SummaryItemKind.ActionableDecision);

        var withNotice = ReturnSummaryComposer.WithNotice(seeded, T.AddHours(1), "Backup restored.");

        // Everything that was there is still there.
        foreach (var original in seeded.Items)
            Assert.Contains(withNotice.Items, i => i.Text == original.Text);

        // The notice is present, and the documented priority order is preserved:
        // Transformation, then ActionableDecision, then Notice.
        Assert.Contains(withNotice.Items, i => i.Kind == SummaryItemKind.Notice);
        var kinds = withNotice.Items.Select(i => (int)i.Kind).ToList();
        Assert.Equal(kinds.OrderBy(k => k).ToList(), kinds);
        Assert.True(withNotice.Items.Count <= PendingReturnSummaryState.MaxItems);
    }

    [Fact]
    public void WithNotice_IsDedupedAgainstAnIdenticalExistingNotice()
    {
        var once = ReturnSummaryComposer.WithNotice(null, T, "Backup restored.");
        var twice = ReturnSummaryComposer.WithNotice(once, T.AddHours(1), "Backup restored.");

        Assert.Equal(
            once.Items.Count(i => i.Text == "Backup restored."),
            twice.Items.Count(i => i.Text == "Backup restored."));
    }

    [Fact]
    public void TheRecoveryNoticeText_IsCalmNonTechnical_AndSaysProgressIsIntact()
    {
        // The player-facing copy is asserted as CONTENT here, not as a count: a notice that
        // leaked a path, an error code or jargon would be a support problem, and the
        // existing suite only checked how many items existed.
        var notice = "Your latest save was damaged; the most recent healthy backup was restored.";

        var summary = ReturnSummaryComposer.WithNotice(null, T, notice);
        var text = summary.Items.Single().Text;

        Assert.Equal(notice, text);
        Assert.DoesNotContain("\\", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("save.", text, StringComparison.OrdinalIgnoreCase);
        // It states both what happened and that nothing was lost.
        Assert.Contains("backup", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("restored", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheMissingPrimaryNotice_IsAlsoCalm_AndDoesNotClaimTheFileWasDamaged()
    {
        // D-052 added a cause-specific notice. "Damaged" would be untrue when the file was
        // simply missing, and inaccurate copy on a recovery is how a player concludes their
        // world was corrupted and starts over.
        var notice = "Your most recent save could not be found; the last healthy backup was restored.";
        var summary = ReturnSummaryComposer.WithNotice(null, T, notice);
        var text = summary.Items.Single().Text;

        Assert.DoesNotContain("damaged", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("could not be found", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("last healthy backup", text, StringComparison.OrdinalIgnoreCase);
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static string Describe(MethodInfo method) =>
        method.ReturnType.Name + " " + method.Name + "("
        + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name)) + ")";

    private static bool FileContains(string path, string needle)
    {
        foreach (var line in File.ReadLines(path))
            if (line.Contains(needle, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>All C# sources under tests/, located by walking up to the repository root.</summary>
    private static List<string> ReadTestSources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "tests")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        var testRoot = Path.Combine(directory!.FullName, "tests");
        return Directory
            .GetFiles(testRoot, "*.cs", SearchOption.AllDirectories)
            // Exclude generated output so a stale copy cannot satisfy the guard.
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }
}
