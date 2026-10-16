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
                case Domain.Projects.ProjectStatus.Available: return PlayerStrings.T("project.status.available");
                case Domain.Projects.ProjectStatus.Queued: return PlayerStrings.F("project.status.queued", Ordinal((queuedPosition ?? 0) + 1));
                case Domain.Projects.ProjectStatus.Active: return PlayerStrings.T("project.status.active");
                case Domain.Projects.ProjectStatus.Completed: return PlayerStrings.T("project.status.completed");
                default: return PlayerStrings.T("project.status.locked");
            }
        }

        public static string ActivityStatus(ActivityPlayerStatus status)
        {
            switch (status)
            {
                case ActivityPlayerStatus.ConnectedCurrent: return PlayerStrings.T("activity.connected");
                case ActivityPlayerStatus.PermissionNeeded: return PlayerStrings.T("activity.permissionNeeded");
                case ActivityPlayerStatus.PermissionDenied: return PlayerStrings.T("activity.permissionDenied");
                case ActivityPlayerStatus.SourceUnavailable: return PlayerStrings.T("activity.sourceUnavailable");
                case ActivityPlayerStatus.WaitingForFirstData: return PlayerStrings.T("activity.waiting");
                case ActivityPlayerStatus.RefreshTemporarilyFailed: return PlayerStrings.T("activity.refreshFailed");
                case ActivityPlayerStatus.ProviderUpdateRequired: return PlayerStrings.T("activity.providerUpdate");
                default: return PlayerStrings.T("activity.unknown");
            }
        }

        /// <summary>Null when nothing needs doing.</summary>
        public static string ActivityAction(ActivityRecommendedAction action)
        {
            switch (action)
            {
                case ActivityRecommendedAction.Connect: return PlayerStrings.T("activity.action.connect");
                case ActivityRecommendedAction.OpenSettings: return PlayerStrings.T("activity.action.openSettings");
                case ActivityRecommendedAction.RetryLater: return PlayerStrings.T("activity.action.retryLater");
                case ActivityRecommendedAction.UpdateProvider: return PlayerStrings.T("activity.action.updateProvider");
                default: return null;
            }
        }

        /// <summary>Closed table for discovery kinds; unknown kinds read as a plain "find".</summary>
        public static string DiscoveryCategory(string category)
        {
            switch (category)
            {
                case "artifact": return "artifact";
                case "infrastructure-history": return PlayerStrings.T("discovery.kind.infrastructure");
                case "settlement-story": return PlayerStrings.T("discovery.kind.settlement");
                case "archive-fragment": return PlayerStrings.T("discovery.kind.archive");
                case "flora": return PlayerStrings.T("discovery.kind.flora");
                case "wildlife": return "wildlife";
                case "science-record": return PlayerStrings.T("discovery.kind.science");
                default: return "find";
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
