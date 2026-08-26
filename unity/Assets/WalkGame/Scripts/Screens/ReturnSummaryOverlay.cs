using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Screens
{
    public static class ReturnSummaryOverlay
    {
        public static VisualElement Build(AppShell shell)
        {
            var host = AppHost.Instance;
            var container = Ui.Column(10);

            var summary = host.Graph.Session.GetPendingReturnSummary();
            container.Add(Ui.Title("While you were away"));

            if (summary == null || !summary.HasMeaningfulChange)
            {
                container.Add(Ui.Body("Nothing new happened in the valley."));
                container.Add(Ui.PrimaryButton("Close", () => shell.CloseSummary(false)));
                return container;
            }

            foreach (var item in summary.Items)
                container.Add(Ui.Body(item.Text));

            container.Add(Ui.Muted(
                "Recorded " + Screens.HomeScreen.FormatUtc(summary.GeneratedAtUtc)));

            container.Add(Ui.PrimaryButton("Got it", () => shell.CloseSummary(true)));
            container.Add(Ui.GhostButton("Later", () => shell.CloseSummary(false)));
            return container;
        }
    }
}
