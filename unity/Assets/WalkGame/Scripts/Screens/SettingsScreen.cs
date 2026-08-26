using UnityEngine.UIElements;
using WalkGame.UnityShell.Composition;
using WalkGame.UnityShell.Development;

namespace WalkGame.UnityShell.Screens
{
    public static class SettingsScreen
    {
        public static VisualElement Build(AppShell shell)
        {
            var container = Ui.Column(10);

            var motionCard = Ui.Card();
            motionCard.Add(Ui.SectionHeader("Motion"));
            bool reducedMotion = AppSettings.GetBool(AppPreference.ReducedMotion, fallback: false);
            motionCard.Add(Ui.Body("Reduced motion keeps the interface calm and still."));
            motionCard.Add(Ui.ToggleButton(
                reducedMotion ? "Reduced motion: On" : "Reduced motion: Off",
                reducedMotion,
                () =>
                {
                    AppSettings.SetBool(AppPreference.ReducedMotion, !reducedMotion);
                    shell.ApplyMotionPreference();
                    shell.Refresh();
                }));
            container.Add(motionCard);

            var aboutCard = Ui.Card();
            aboutCard.Add(Ui.SectionHeader("About"));
            aboutCard.Add(Ui.KeyValueRow("Version", Application.version));
            aboutCard.Add(Ui.Body("Walk Game is a low-attention restoration game. " +
                                  "Your walking restores a valley while the app is closed; check in briefly, " +
                                  "choose what to restore next, and get on with your day."));
            container.Add(aboutCard);

            if (DevActivityGate.Enabled)
                container.Add(DevToolsSectionHost.Build(shell));

            return container;
        }
    }
}
