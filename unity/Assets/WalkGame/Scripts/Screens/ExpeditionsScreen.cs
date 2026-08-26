using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Screens
{
    public static class ExpeditionsScreen
    {
        public static VisualElement Build(AppShell shell)
        {
            var session = AppHost.Instance.Graph.Session;
            var container = Ui.Column(10);
            if (!session.HasLoadedState)
                return container;

            var model = session.GetExpeditions();

            var header = Ui.Card();
            header.Add(Ui.Title("Expeditions"));
            header.Add(Ui.Muted(model.CompletedCount + " of " + model.TotalExpeditions +
                                " routes completed, " + model.AvailableCount + " ready"));
            container.Add(header);

            foreach (var e in model.Expeditions)
                container.Add(ExpeditionCard(e));

            return container;
        }

        private static VisualElement ExpeditionCard(ExpeditionsReadModel.ExpeditionRow e)
        {
            var card = Ui.Card();
            card.Add(Ui.Body(CopyTable.Text(e.TitleKey)));

            var requirements = new List<string>();
            foreach (var pid in e.RequiredProjectIds)
                requirements.Add(CopyTable.Text(pid));
            foreach (var stage in e.RequiredStageKeys)
                requirements.Add(CopyTable.Text(stage));

            if (requirements.Count > 0)
                card.Add(Ui.Muted("Requires: " + string.Join(", ", requirements)));

            switch (e.Status)
            {
                case ExpeditionsReadModel.ExpeditionStatus.Locked:
                    card.Add(Ui.StatusLine("Route not open yet", Ui.Warn));
                    break;
                case ExpeditionsReadModel.ExpeditionStatus.Available:
                    card.Add(Ui.StatusLine("Ready — resolves while the app is closed", Ui.Accent));
                    break;
                case ExpeditionsReadModel.ExpeditionStatus.Completed:
                    card.Add(Ui.StatusLine(
                        "Completed" + (e.CompletedAtUtc.HasValue
                            ? " " + HomeScreen.FormatUtc(e.CompletedAtUtc.Value)
                            : string.Empty),
                        Ui.TextMuted));
                    break;
            }

            if (e.RewardType.HasValue && e.Status != ExpeditionsReadModel.ExpeditionStatus.Completed)
                card.Add(Ui.Muted("Reward: " + e.RewardUnits + " " + RewardName(e.RewardType.Value)));

            return card;
        }

        private static string RewardName(WalkGame.Domain.Economy.ResourceType type)
        {
            return type switch
            {
                WalkGame.Domain.Economy.ResourceType.Vitality => "vitality",
                WalkGame.Domain.Economy.ResourceType.Materials => "materials",
                WalkGame.Domain.Economy.ResourceType.Knowledge => "knowledge",
                _ => type.ToString(),
            };
        }
    }
}
