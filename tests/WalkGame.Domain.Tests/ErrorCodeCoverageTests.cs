using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WalkGame.Domain.Common;

namespace WalkGame.Domain.Tests;

/// <summary>
/// D-057: a guard against `ErrorCodes` silently accumulating conditions the system does
/// not actually distinguish.
///
/// Three codes were removed by this change — <c>NotAvailable</c>, <c>QueueEmpty</c> and
/// <c>InvalidArgument</c> — each declared and returned by nothing. They were not
/// harmless: a reader of `Results.cs` reasonably concluded the system distinguished
/// "not available", "queue empty" and "invalid argument" somewhere, and nothing in C#
/// warns about a constant nobody reads. This is the same shape of lie the repository has
/// already been bitten by twice: a mechanism that reads as live and is not.
///
/// The guard reports ALL unused codes in one message, not just the first, so a single run
/// tells the author everything to fix.
/// </summary>
public sealed class ErrorCodeCoverageTests
{
    /// <summary>
    /// Codes the guard does not require a production consumer for, each with the reason.
    /// Platform adapters can declare a code ahead of the operation that returns it without
    /// the guard blocking legitimate in-flight work; doing so is a reviewable edit, not a
    /// silent exemption.
    /// </summary>
    private static readonly Dictionary<string, string> ReservedAllowList =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Intentionally empty. Every code currently declared is returned by a real
        // operation. If an adapter needs to land a code before its operation, add it
        // here WITH its reason in the same commit.
    };

    [Fact]
    public void EveryDeclaredErrorCode_IsReturnedBySomeProductionPath()
    {
        var sources = ReadProductionSources();
        Assert.NotEmpty(sources);

        var unused = new List<string>();

        foreach (var field in typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.IsLiteral || field.FieldType != typeof(string)) continue;

            string name = field.Name;
            if (ReservedAllowList.ContainsKey(name)) continue;

            // A code counts as used when it is REFERENCED as ErrorCodes.<name> in
            // production. Matching on the member name is deliberate: it means a code that
            // only ever appears in a test cannot keep itself alive.
            string qualified = "ErrorCodes." + name;
            if (!sources.Any(path => FileContains(path, qualified)))
                unused.Add(name + " (\"" + (field.GetRawConstantValue() as string) + "\")");
        }

        Assert.True(
            unused.Count == 0,
            "ErrorCodes declared but returned by no production path (add a consumer, or remove the code):\n  - "
            + string.Join("\n  - ", unused));
    }

    [Fact]
    public void TheRemovedCodesAreGenuinelyGone()
    {
        // The three removed codes must not reappear, because reintroducing them is exactly
        // the accumulation this guard exists to prevent.
        var declared = typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name)
            .ToList();

        Assert.DoesNotContain("NotAvailable", declared);
        Assert.DoesNotContain("QueueEmpty", declared);
        Assert.DoesNotContain("InvalidArgument", declared);
    }

    [Fact]
    public void TheGenericDomainResultIsGone()
    {
        // `DomainResult<T>` was removed: zero production references, two in its own unit
        // test. Asserting its absence keeps a future contributor from re-adding a result
        // shape "just in case" — which is how it arrived.
        var type = typeof(DomainResult).Assembly.GetType("WalkGame.Domain.Common.DomainResult`1");
        Assert.Null(type);
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static bool FileContains(string path, string needle)
    {
        foreach (var line in File.ReadLines(path))
            if (line.Contains(needle, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>All C# sources under src/, located by walking up to the repository root.</summary>
    private static List<string> ReadProductionSources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        var srcRoot = Path.Combine(directory!.FullName, "src");
        return Directory
            .GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }
}
