using System.Globalization;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Regions;
using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Screens
{
    public static class RegionScreen
    {
        public static VisualElement Build(AppShell shell)
        {
            var session = AppHost.Instance.Graph.Session;
            var container = Ui.Column(10);
            if (!session.HasLoadedState)
                return container;

            var model = session.GetRegion();

            var overview = Ui.Card();
            overview.Add(Ui.Title(CopyTable.Text(model.RegionTitleKey)));
            overview.Add(Ui.ProgressBar(
                model.TotalProjects > 0 ? (float)model.CompletedProjects / model.TotalProjects : 0f,
                out _));
            overview.Add(Ui.Muted(model.CompletedProjects + " of " + model.TotalProjects +
                                  " restoration projects complete"));
            overview.Add(Ui.KeyValueRow("Ecology", StageWord(model.EcologyStage)));
            overview.Add(Ui.KeyValueRow("Settlement", StageWord(model.SettlementStage)));
            if (model.RegionCompleted)
            {
                overview.Add(Ui.Banner(
                    "Region restored" + (model.RegionCompletedAtUtc.HasValue
                        ? " on " + HomeScreen.FormatUtc(model.RegionCompletedAtUtc.Value)
                        : string.Empty),
                    Ui.Accent));
            }
            container.Add(overview);

            container.Add(Ui.SectionHeader("Landmarks"));
            foreach (var lm in model.Landmarks)
            {
                var card = Ui.Card();
                card.Add(Ui.Body(CopyTable.Text(lm.TitleKey)));
                card.Add(Ui.StatusLine(StageWord((int)lm.Stage), lm.Stage >= RestorationStage.Restored ? Ui.Accent : Ui.Warn));
                container.Add(card);
            }

            container.Add(Ui.SectionHeader("Producers"));
            foreach (var p in model.Producers)
            {
                var card = Ui.Card();
                card.Add(Ui.Body(CopyTable.Text(p.TitleKey)));
                if (!p.Unlocked)
                {
                    card.Add(Ui.StatusLine("Not yet operating", Ui.TextMuted));
                    continue;
                }
                card.Add(Ui.KeyValueRow("Output", OutputName(p.Output)));
                card.Add(Ui.KeyValueRow("Rate",
                    (p.MilliUnitsPerDay / 1000.0).ToString("0.##", CultureInfo.InvariantCulture) + " / day"));
                card.Add(Ui.KeyValueRow("Waiting",
                    (p.StoredMilliUnits / 1000.0).ToString("0.##", CultureInfo.InvariantCulture) +
                    " of " + p.CapacityUnits.ToString(CultureInfo.InvariantCulture)));
                container.Add(card);
            }

            return container;
        }

        private static string StageWord(int stage)
        {
            return stage switch
            {
                (int)RestorationStage.Ruined => "Ruined",
                (int)RestorationStage.Stabilized => "Stabilized",
                (int)RestorationStage.Functional => "Functional",
                (int)RestorationStage.Restored => "Restored",
                (int)RestorationStage.Flourishing => "Flourishing",
                _ => "Unknown",
            };
        }

        private static string OutputName(ResourceType type)
        {
            return type switch
            {
                ResourceType.Vitality => "Vitality",
                ResourceType.Materials => "Materials",
                ResourceType.Knowledge => "Knowledge",
                _ => type.ToString(),
            };
        }
    }
}
