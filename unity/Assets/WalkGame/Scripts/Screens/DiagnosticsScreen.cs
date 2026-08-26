using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Screens
{
    public static class DiagnosticsScreen
    {
        public static VisualElement Build(AppShell shell)
        {
            var host = AppHost.Instance;
            var graph = host.Graph;
            var session = graph.Session;
            var container = Ui.Column(10);

            var buildCard = Ui.Card();
            buildCard.Add(Ui.SectionHeader("Build & runtime"));
            buildCard.Add(Ui.KeyValueRow("App version", Application.version));
            buildCard.Add(Ui.KeyValueRow("Unity", Application.unityVersion));
            buildCard.Add(Ui.KeyValueRow("Platform", Application.platform.ToString()));
            if (!string.IsNullOrEmpty(SystemInfo.deviceModel))
                buildCard.Add(Ui.KeyValueRow("Device", SystemInfo.deviceModel));
            container.Add(buildCard);

            var bootCard = Ui.Card();
            bootCard.Add(Ui.SectionHeader("Session"));
            bootCard.Add(Ui.KeyValueRow("Boot phase", host.Phase.ToString()));
            bootCard.Add(Ui.KeyValueRow("Last boot duration",
                host.LastBootDuration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms"));
            bootCard.Add(Ui.KeyValueRow("State loaded", session.HasLoadedState ? "Yes" : "No"));
            if (!string.IsNullOrEmpty(host.BootDetail))
                bootCard.Add(Ui.Muted(host.BootDetail!));
            bootCard.Add(Ui.KeyValueRow("Last reconcile", HomeScreen.FormatUtc(host.LastReconcileUtc)));
            container.Add(bootCard);

            var saveCard = Ui.Card();
            saveCard.Add(Ui.SectionHeader("Persistence"));
            saveCard.Add(Ui.Muted(graph.SaveLocation.Directory));
            AddFileRow(saveCard, "Primary save", graph.SaveLocation.PrimaryPath);
            AddFileRow(saveCard, "Backup save", graph.SaveLocation.BackupPath);
            saveCard.Add(Ui.KeyValueRow("Schema version",
                Infrastructure.Persistence.SaveCodec.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)));
            container.Add(saveCard);

            var ingestCard = Ui.Card();
            ingestCard.Add(Ui.SectionHeader("Activity pipeline"));
            ingestCard.Add(Ui.KeyValueRow("Dev source available",
                Development.DevActivityGate.Enabled ? "Yes (dev build)" : "No"));
            if (host.LastIngestResult != null)
            {
                var r = host.LastIngestResult;
                ingestCard.Add(Ui.KeyValueRow("Received", r.TotalReceived.ToString(CultureInfo.InvariantCulture)));
                ingestCard.Add(Ui.KeyValueRow("Accepted", r.Accepted.ToString(CultureInfo.InvariantCulture)));
                ingestCard.Add(Ui.KeyValueRow("Rejected", r.Rejected.ToString(CultureInfo.InvariantCulture)));
                ingestCard.Add(Ui.KeyValueRow("Duplicates ignored", r.DuplicatesIgnored.ToString(CultureInfo.InvariantCulture)));
                ingestCard.Add(Ui.KeyValueRow("Corrections applied", r.CorrectionsApplied.ToString(CultureInfo.InvariantCulture)));
                ingestCard.Add(Ui.KeyValueRow("Deletions applied", r.DeletionsApplied.ToString(CultureInfo.InvariantCulture)));
                ingestCard.Add(Ui.KeyValueRow("Vitality credited", r.VitalityCredited.ToString(CultureInfo.InvariantCulture)));
                ingestCard.Add(Ui.KeyValueRow("Unapplied reversals", r.UnappliedReversalVitality.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                ingestCard.Add(Ui.Muted("No activity processed in this session."));
            }
            container.Add(ingestCard);

            return container;
        }

        private static void AddFileRow(VisualElement card, string label, string path)
        {
            try
            {
                var info = new System.IO.FileInfo(path);
                if (info.Exists)
                    card.Add(Ui.KeyValueRow(label,
                        (info.Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB, " +
                        info.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")));
                else
                    card.Add(Ui.KeyValueRow(label, "Not present"));
            }
            catch (System.Exception ex)
            {
                card.Add(Ui.KeyValueRow(label, "Unavailable: " + ex.Message));
            }
        }
    }
}
