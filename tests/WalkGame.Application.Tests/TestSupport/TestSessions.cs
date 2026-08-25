using System;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;

namespace WalkGame.Application.Tests.TestSupport
{
    internal static class TestSessions
    {
        public const string EntryProjectId = "proj.clear-trailhead";

        public static readonly DateTimeOffset T0 =
            new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);

        public static readonly Guid Tx1 = new Guid("00000000-0000-0000-0000-000000000001");
        public static readonly Guid Tx2 = new Guid("00000000-0000-0000-0000-000000000002");

        public static GameSession Create(TempDirectory directory, IClock clock) =>
            Create(directory.Path, clock);

        public static GameSession Create(string directory, IClock clock) =>
            Create(directory, clock, Region1Catalog.Create());

        public static GameSession Create(string directory, IClock clock, RegionDefinition content) =>
            new GameSession(
                new AtomicFileSaveStore(directory),
                NewCodec(),
                clock,
                content);

        public static SaveCodec NewCodec() =>
            new SaveCodec(new MigrationRunner(DefaultMigrations.All));
    }
}
