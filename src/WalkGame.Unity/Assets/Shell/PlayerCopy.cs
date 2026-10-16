using WalkGame.Application.Activity;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Projects;

namespace WalkGame.Shell
{
    /// <summary>
    /// Closed tables turning read-model enums into player copy (UX_DESIGN §5, §12).
    /// Every enum value has a sentence; enum identifiers never reach the screen.
    /// </summary>
    public static class PlayerCopy
    {
        public static string Title(string titleKey) =>
            string.IsNullOrWhiteSpace(titleKey) || titleKey.IndexOf('.') >= 0 ? GlancePhrases.UntitledProject : titleKey;

        public static string ProjectStatus(ProjectStatus status, int? queuedPosition)
        {
            switch (status)
            {
                case Domain.Projects.ProjectStatus.Available: return "ready to start";
                case Domain.Projects.ProjectStatus.Queued: return "waiting, " + Ordinal((queuedPosition ?? 0) + 1) + " in line";
                case Domain.Projects.ProjectStatus.Active: return "being restored now";
                case Domain.Projects.ProjectStatus.Completed: return "restored";
                default: return "not open yet";
            }
        }

        public static string ActivityStatus(ActivityPlayerStatus status)
        {
            switch (status)
            {
                case ActivityPlayerStatus.ConnectedCurrent: return "Connected. Your walks are being counted.";
                case ActivityPlayerStatus.PermissionNeeded: return "Walking data is not connected yet. You can still explore everything.";
                case ActivityPlayerStatus.PermissionDenied: return "Access to walking data is turned off. Your progress is safe, but new walks will not count until it is turned back on.";
                case ActivityPlayerStatus.SourceUnavailable: return "This device has no walking data source we can read. Your progress is safe.";
                case ActivityPlayerStatus.WaitingForFirstData: return "Connected. Waiting for your first walk to arrive.";
                case ActivityPlayerStatus.RefreshTemporarilyFailed: return "We could not refresh your walks just now. Nothing is lost; we will try again.";
                default: return "Walking data status is unknown right now. Your progress is safe.";
            }
        }

        /// <summary>Null when nothing needs doing.</summary>
        public static string ActivityAction(ActivityRecommendedAction action)
        {
            switch (action)
            {
                case ActivityRecommendedAction.Connect: return "Next: connect your walking data.";
                case ActivityRecommendedAction.OpenSettings: return "Next: turn access back on in your phone's settings.";
                case ActivityRecommendedAction.RetryLater: return "Next: nothing to do; it will retry on its own.";
                default: return null;
            }
        }

        private static string Ordinal(int n)
        {
            int t = n % 100;
            string suffix = t >= 11 && t <= 13 ? "th" : (n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th");
            return n + suffix;
        }
    }
}
