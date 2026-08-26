using UnityEngine;
using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Screens
{
    public static class OnboardingScreen
    {
        public static VisualElement Build(AppShell shell)
        {
            var host = AppHost.Instance;
            var container = Ui.Column(12);

            container.Add(Ui.Title("Millbrook Valley needs you back"));
            container.Add(Ui.Body(
                "Every walk you take becomes vitality. While you are away, that vitality keeps restoring " +
                "a forgotten valley: trails, water, wetlands, forest and the old settlement."));

            var howCard = Ui.Card();
            howCard.Add(Ui.SectionHeader("How it works"));
            howCard.Add(Ui.Body("Walk - the valley restores while you are gone."));
            howCard.Add(Ui.Body("Return briefly - see what changed."));
            howCard.Add(Ui.Body("Choose priorities - pick what gets restored next."));
            howCard.Add(Ui.Body("Leave - nothing bad happens if you stay away."));
            container.Add(howCard);

            var startCard = Ui.Card();
            startCard.Add(Ui.PrimaryButton("Begin the restoration", () =>
            {
                ulong seed = DeriveSeed();
                host.StartNewGame(seed);
                shell.Refresh();
            }));
            startCard.Add(Ui.Muted("Your progress is saved on this device and survives restarts."));
            container.Add(startCard);

            return container;
        }

        private static ulong DeriveSeed()
        {
            long ticks = DateTime.UtcNow.Ticks;
            return (ulong)ticks ^ ((ulong)Environment.ProcessId << 32) ^ (ulong)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }
    }
}
