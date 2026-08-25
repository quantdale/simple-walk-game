using System;
using System.IO;
using WalkGame.Domain.Activity;
using WalkGame.Infrastructure.Fixtures;

namespace WalkGame.Application.Tests.TestSupport
{
    /// <summary>Loads the checked-in fixture corpus from tests/fixtures/activity.</summary>
    internal static class ActivityFixtures
    {
        public static System.Collections.Generic.List<NormalizedActivityRecord> LoadBatch(string fileName) =>
            FixtureActivityFileReader.LoadBatch(Path.Combine(FixtureRoot(), fileName));

        private static string FixtureRoot()
        {
            DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null && !File.Exists(System.IO.Path.Combine(current.FullName, "SimpleWalkGame.sln")))
                current = current.Parent;

            if (current == null)
                throw new InvalidOperationException("Could not locate repository root from test base directory.");

            return System.IO.Path.Combine(current.FullName, "tests", "fixtures", "activity");
        }
    }
}
