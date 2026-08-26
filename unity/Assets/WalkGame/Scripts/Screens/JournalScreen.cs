using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Screens
{
    public static class JournalScreen
    {
        public static VisualElement Build(AppShell shell)
        {
            var session = AppHost.Instance.Graph.Session;
            var container = Ui.Column(10);
            if (!session.HasLoadedState)
                return container;

            var model = session.GetDiscoveries();

            var header = Ui.Card();
            header.Add(Ui.Title("Discoveries"));
            header.Add(Ui.Muted(model.UnlockedCount + " of " + model.TotalDiscoveries +
                                " recorded, " + model.UnreviewedCount + " new"));
            container.Add(header);

            if (model.TotalDiscoveries == 0)
            {
                container.Add(Ui.Muted("No discoveries exist in this region yet."));
                return container;
            }

            foreach (var d in model.Discoveries)
            {
                var card = Ui.Card();
                if (!d.Unlocked)
                {
                    card.Add(Ui.Body("Undiscovered"));
                    card.Add(Ui.Muted("Keep restoring — finds surface as places come back to life."));
                    container.Add(card);
                    continue;
                }

                card.Add(Ui.Body(d.TitleKey));
                if (!string.IsNullOrEmpty(d.Category))
                    card.Add(Ui.Muted(d.Category));
                if (!d.Reviewed)
                {
                    card.Add(Ui.StatusLine("New — not yet reviewed", Ui.Accent));
                    var id = d.DiscoveryId;
                    card.Add(Ui.GhostButton("Mark as reviewed", () =>
                    {
                        shell.ShowFeedback(session.MarkDiscoveryReviewed(id));
                        shell.Refresh();
                    }));
                }
                else
                {
                    card.Add(Ui.StatusLine("Reviewed", Ui.TextMuted));
                }
                container.Add(card);
            }

            return container;
        }
    }
}
