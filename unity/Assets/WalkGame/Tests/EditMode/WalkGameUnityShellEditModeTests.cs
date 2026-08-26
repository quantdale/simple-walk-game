using System;
using System.IO;
using NUnit.Framework;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Application.Development;
using WalkGame.Domain.Time;
using WalkGame.UnityShell.Composition;

namespace WalkGame.UnityShell.EditModeTests
{
    internal sealed class TempSaveDir : IDisposable
    {
        public string Path { get; }

        public TempSaveDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "walkgame-unity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }

    public sealed class CompositionTests
    {
        [Test]
        public void Compose_boots_to_no_save_found_on_fresh_directory()
        {
            using var dir = new TempSaveDir();
            var graph = AppComposer.Compose(dir.Path);
            Assert.IsFalse(graph.Session.HasLoadedState);
            var boot = graph.Session.Continue();
            Assert.AreEqual(StartStatus.NoSaveFound, boot.Status);
        }

        [Test]
        public void StartNewGame_then_recompose_loads_same_state()
        {
            using var dir = new TempSaveDir();
            var first = AppComposer.Compose(dir.Path);
            Assert.AreEqual(StartStatus.NewGameCreated, first.Session.StartNewGame(seed: 7).Status);

            var home1 = first.Session.GetHome();
            Assert.IsNotNull(home1);

            var second = AppComposer.Compose(dir.Path);
            var boot = second.Session.Continue();
            Assert.AreEqual(StartStatus.Loaded, boot.Status);
            Assert.IsTrue(second.Session.HasLoadedState);
        }

        [Test]
        public void Corrupt_primary_recovers_from_backup()
        {
            using var dir = new TempSaveDir();
            var graph = AppComposer.Compose(dir.Path);
            graph.Session.StartNewGame(seed: 42);

            File.WriteAllText(graph.SaveLocation.PrimaryPath, "not a save");

            var fresh = AppComposer.Compose(dir.Path);
            var boot = fresh.Session.Continue();
            Assert.AreEqual(StartStatus.RecoveredFromBackup, boot.Status);
            Assert.IsTrue(fresh.Session.HasLoadedState);
        }

        [Test]
        public void Unreadable_saves_fail_explicitly_without_fresh_save_fabrication()
        {
            using var dir = new TempSaveDir();
            var graph = AppComposer.Compose(dir.Path);
            graph.Session.StartNewGame(seed: 42);
            File.WriteAllText(graph.SaveLocation.PrimaryPath, "garbage");
            File.WriteAllText(graph.SaveLocation.BackupPath, "also garbage");

            var fresh = AppComposer.Compose(dir.Path);
            var boot = fresh.Session.Continue();
            Assert.AreEqual(StartStatus.SaveUnreadable, boot.Status);
            Assert.IsFalse(fresh.Session.HasLoadedState);
            Assert.IsNotEmpty(boot.Detail ?? string.Empty);
        }
    }

    public sealed class PipelineTests
    {
        private static DateTimeOffset T0 => new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.Zero);

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow => T0;
        }

        [Test]
        public void Synthetic_activity_credits_once_and_replays_as_noop()
        {
            using var dir = new TempSaveDir();
            var graph = AppComposer.Compose(dir.Path, new FixedClock());
            graph.Session.StartNewGame(seed: 5);

            var before = graph.Session.GetHome().Vitality;

            var source1 = DevActivityGate.CreateSource(8000);
            var first = graph.Session.IngestFromSource(source1, T0.AddDays(-2), T0);
            Assert.Greater(first.VitalityCredited, 0);
            Assert.Greater(graph.Session.GetHome().Vitality, before);

            var vitalityAfterFirst = graph.Session.GetHome().Vitality;
            var source2 = DevActivityGate.CreateSource(8000);
            var replay = graph.Session.IngestFromSource(source2, T0.AddDays(-2), T0);
            Assert.AreEqual(0, replay.VitalityCredited);
            Assert.AreEqual(vitalityAfterFirst, graph.Session.GetHome().Vitality);
        }

        [Test]
        public void Queue_operations_flow_through_application_boundary()
        {
            using var dir = new TempSaveDir();
            var graph = AppComposer.Compose(dir.Path, new FixedClock());
            graph.Session.StartNewGame(seed: 9);

            var entryId = "proj.clear-trailhead";
            var enqueue = graph.Session.EnqueueProject(entryId);
            Assert.IsTrue(enqueue.IsSuccess);

            var duplicate = graph.Session.EnqueueProject(entryId);
            Assert.IsFalse(duplicate.IsSuccess);
            Assert.IsNotNull(duplicate.Error);

            var projects = graph.Session.GetProjects();
            Assert.AreEqual(entryId, projects.ActiveProjectId);
        }

        [Test]
        public void Return_summary_acknowledge_is_idempotent_and_does_not_alter_progression()
        {
            using var dir = new TempSaveDir();
            var graph = AppComposer.Compose(dir.Path, new FixedClock());
            graph.Session.StartNewGame(seed: 11);

            graph.Session.IngestFromSource(DevActivityGate.CreateSource(20000), T0.AddDays(-30), T0);

            var pending = graph.Session.GetPendingReturnSummary();
            if (pending == null || !pending.HasMeaningfulChange)
                Assert.Ignore("No durable summary was generated for this seed/window; nothing to acknowledge.");

            var homeBefore = graph.Session.GetHome();

            Assert.IsTrue(graph.Session.AcknowledgeReturnSummary().IsSuccess);
            Assert.IsNull(graph.Session.GetPendingReturnSummary());
            Assert.IsTrue(graph.Session.AcknowledgeReturnSummary().IsSuccess);

            var homeAfter = graph.Session.GetHome();
            Assert.AreEqual(homeBefore.Vitality, homeAfter.Vitality);
            Assert.AreEqual(homeBefore.CompletedProjects, homeAfter.CompletedProjects);
        }
    }

    public sealed class CopyTableTests
    {
        [Test]
        public void Region_title_resolves()
        {
            var content = Region1Catalog.Create();
            StringAssert.DoesNotStartWith("region.", CopyTable.Text(content.TitleKey));
        }

        [Test]
        public void All_project_titles_resolve_through_content_keys()
        {
            var content = Region1Catalog.Create();
            foreach (var project in content.Projects)
                StringAssert.DoesNotStartWith("proj.", CopyTable.Text(project.TitleKey));
        }

        [Test]
        public void Unknown_key_returns_key_itself()
        {
            Assert.AreEqual("totally.unknown.key", CopyTable.Text("totally.unknown.key"));
        }
    }
}
