using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Screens
{
    public static class UnrecoverableScreen
    {
        private static bool _confirmingReset;

        public static VisualElement Build(AppShell shell)
        {
            var host = AppHost.Instance;
            var container = Ui.Column(12);

            var card = Ui.Card();
            card.Add(Ui.Title("Your save could not be opened"));
            card.Add(Ui.Body(
                "The restoration data on this device is damaged or unreadable. " +
                "Nothing has been deleted. You can try again, or start a brand-new valley."));
            if (!string.IsNullOrEmpty(host.BootDetail))
                card.Add(Ui.Muted("Technical detail: " + host.BootDetail));

            card.Add(Ui.PrimaryButton("Try again", () =>
            {
                _confirmingReset = false;
                host.RunBoot();
                shell.Refresh();
            }));

            if (!_confirmingReset)
            {
                card.Add(Ui.GhostButton("Start over from scratch", () => _confirmingReset = true));
            }
            else
            {
                card.Add(Ui.StatusLine(
                    "Starting over erases the damaged data and begins a new valley. This cannot be undone.",
                    Ui.Danger));
                card.Add(Ui.DangerButton("Yes - start over", () =>
                {
                    _confirmingReset = false;
                    var result = host.StartNewGame(0);
                    shell.ShowFeedback(result.Status == WalkGame.Application.StartStatus.NewGameCreated
                        ? Domain.Common.DomainResult.Ok()
                        : Domain.Common.DomainResult.Fail("startover.failed", result.Detail ?? "Could not start a new game."));
                    shell.Refresh();
                }));
                card.Add(Ui.GhostButton("No - go back", () => _confirmingReset = false));
            }

            container.Add(card);
            return container;
        }
    }
}
