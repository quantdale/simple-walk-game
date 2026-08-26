using System.Globalization;
using WalkGame.Domain.Regions;
using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Screens
{
    public static class HomeScreen
    {
        public static VisualElement Build(AppShell shell)
        {
            var host = AppHost.Instance;
            var session = host.Graph.Session;
            var container = Ui.Column(10);

            if (!session.HasLoadedState)
            {
                container.Add(Ui.Muted("No restoration in progress yet."));
                return container;
            }

            var pending = session.GetPendingReturnSummary();
            if (pending != null && pending.HasMeaningfulChange)
            {
                var card = Ui.Card();
                card.Add(Ui.SectionHeader("While you were away"));
                card.Add(Ui.Muted("Your restorations continued. A short summary is waiting."));
                card.Add(Ui.PrimaryButton("See what changed", () => shell.ShowReturnSummary()));
                container.Add(card);
            }

            var home = session.GetHome();

            var focusCard = Ui.Card();
            focusCard.Add(Ui.SectionHeader("Current focus"));
            if (home.ActiveProjectId != null)
            {
                focusCard.Add(Ui.Body(CopyTable.Text(home.ActiveProjectTitleKey ?? home.ActiveProjectId)));
                float frac = home.ActiveProjectCost > 0
                    ? (float)home.ActiveProjectInvested / home.ActiveProjectCost
                    : 0f;
                focusCard.Add(Ui.ProgressBar(frac, out _));
                focusCard.Add(Ui.Muted(FormatUnits(home.ActiveProjectInvested) + " of " +
                                       FormatUnits(home.ActiveProjectCost) + " vitality invested"));
            }
            else
            {
                focusCard.Add(Ui.Muted("Nothing is being restored right now."));
            }

            if (!string.IsNullOrEmpty(home.PrimaryNextAction))
                focusCard.Add(Ui.StatusLine(CopyTable.Text(home.PrimaryNextAction), Ui.Accent));

            container.Add(focusCard);

            var activityCard = Ui.Card();
            activityCard.Add(Ui.SectionHeader("Latest changes"));
            if (host.LastIngestResult != null)
            {
                var r = host.LastIngestResult;
                activityCard.Add(Ui.Body(
                    "+" + FormatUnits(r.VitalityCredited) + " vitality from your last activity"));
                activityCard.Add(Ui.Muted(
                    r.Accepted + " records accepted, " + r.DuplicatesIgnored + " duplicates ignored, " +
                    r.Rejected + " rejected"));
            }
            else
            {
                activityCard.Add(Ui.Muted("No new activity processed yet on this device."));
            }
            activityCard.Add(Ui.Muted("Last checked: " + FormatUtc(host.LastReconcileUtc)));
            container.Add(activityCard);

            var regionCard = Ui.Card();
            regionCard.Add(Ui.SectionHeader(CopyTable.Text(home.RegionTitleKey)));
            regionCard.Add(Ui.KeyValueRow("Projects restored",
                home.CompletedProjects + " of " + home.TotalProjects));
            int restoredLandmarks = 0;
            foreach (var lm in home.Landmarks)
                if (lm.Stage >= RestorationStage.Restored)
                    restoredLandmarks++;
            regionCard.Add(Ui.KeyValueRow("Landmarks thriving",
                restoredLandmarks + " of " + home.Landmarks.Count));
            regionCard.Add(Ui.GhostButton("Region overview", () => shell.NavigateTo(ScreenId.Region)));
            container.Add(regionCard);

            return container;
        }

        internal static string FormatUnits(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        internal static string FormatUtc(DateTimeOffset utc)
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        }
    }
}
