using UnityEngine.UIElements;
using WalkGame.UnityShell.Development;

namespace WalkGame.UnityShell.Screens
{
    public static class DevToolsSectionHost
    {
        public static VisualElement Build(AppShell shell)
        {
            var host = AppHost.Instance;
            var container = Ui.Column(8);

            var card = Ui.Card();
            card.Add(Ui.SectionHeader("Development tools"));
            card.Add(Ui.Muted("Feeds synthetic walking through the real ingestion pipeline. " +
                              "Replaying the same days credits nothing."));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            for (int days = 1; days <= 3; days++)
            {
                int d = days;
                var b = Ui.GhostButton("+" + d + " day" + (d > 1 ? "s" : ""), () =>
                {
                    host.InjectDevActivity(d, DevActivityGate.DefaultStepsPerDay);
                    shell.Refresh();
                });
                b.style.marginRight = 6;
                row.Add(b);
            }
            card.Add(row);

            if (host.LastIngestResult != null)
            {
                var r = host.LastIngestResult;
                card.Add(Ui.Muted("Last ingest: " + r.Accepted + " accepted, " +
                                  r.DuplicatesIgnored + " duplicates ignored, " +
                                  r.VitalityCredited + " vitality credited"));
            }

            container.Add(card);
            return container;
        }
    }
}
