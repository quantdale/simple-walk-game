using System;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Persistence;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Time;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Platform;

namespace WalkGame.UnityShell.Composition
{
    public sealed class SaveLocationInfo
    {
        public string Directory { get; }
        public string PrimaryPath { get; }
        public string BackupPath { get; }

        public SaveLocationInfo(string directory, string primaryPath, string backupPath)
        {
            Directory = directory;
            PrimaryPath = primaryPath;
            BackupPath = backupPath;
        }
    }

    public sealed class AppGraph
    {
        public GameSession Session { get; }
        public RegionDefinition Content { get; }
        public SaveLocationInfo SaveLocation { get; }

        public AppGraph(GameSession session, RegionDefinition content, SaveLocationInfo saveLocation)
        {
            Session = session;
            Content = content;
            SaveLocation = saveLocation;
        }
    }

    public static class AppComposer
    {
        public static AppGraph Compose(string saveDirectory, IClock clock)
        {
            if (string.IsNullOrWhiteSpace(saveDirectory))
                throw new ArgumentException("Save directory is required.", nameof(saveDirectory));

            var store = new AtomicFileSaveStore(saveDirectory);
            var codec = new SaveCodec(new MigrationRunner(DefaultMigrations.All));
            var content = Region1Catalog.Create();
            var session = new GameSession(store, codec, clock, content);

            return new AppGraph(
                session,
                content,
                new SaveLocationInfo(
                    saveDirectory,
                    System.IO.Path.Combine(saveDirectory, "save.json"),
                    System.IO.Path.Combine(saveDirectory, "save.backup.json")));
        }

        public static AppGraph Compose(string saveDirectory)
        {
            return Compose(saveDirectory, new SystemClock());
        }
    }
}
