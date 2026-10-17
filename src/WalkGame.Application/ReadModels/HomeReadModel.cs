using System;
using System.Collections.Generic;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Explicit attention classification for the Home surface (UX_DESIGN §3): why the
    /// shell believes the player should look, or None. Derived deterministically from
    /// canonical state; never persisted.
    /// </summary>
    public enum HomeAttentionReason
    {
        /// <summary>Nothing needs attention.</summary>
        None = 0,

        /// <summary>A durable return summary awaits acknowledgement.</summary>
        PendingReturnSummary = 1,

        /// <summary>No active/queued project while unallocated Vitality sits banked.</summary>
        QueueEmptyWithBankedVitality = 2,

        /// <summary>Fresh profile: no project has ever been started (informational).</summary>
        NoProjectStartedYet = 3,

        /// <summary>
        /// The region's restoration is complete. This is a CONCLUSION, not a to-do:
        /// <see cref="HomeReadModel.RequiresAttention"/> is false for it, and the reason value
        /// is what lets a shell tell "finished" apart from "nothing has happened yet" — which
        /// <see cref="None"/> alone cannot express (D-061).
        /// </summary>
        RegionComplete = 4,
    }

    /// <summary>
    /// Purpose-built read model for the Home screen. Presentation receives immutable
    /// snapshots like this instead of mutable domain graphs.
    /// </summary>
    public sealed class HomeReadModel
    {
        public string RegionTitleKey { get; }
        public long Vitality { get; }
        public long Materials { get; }
        public long Knowledge { get; }

        public string? ActiveProjectTitleKey { get; }
        public string? ActiveProjectId { get; }
        public long ActiveProjectInvested { get; }
        public long ActiveProjectCost { get; }

        /// <summary>D-145: authored visible build stages of the active project (1 when it authors none).</summary>
        public int ActiveProjectVisibleBuildStages { get; }

        /// <summary>D-145: display-time payoff for the active project; null when nothing is funded yet.</summary>
        public WalkGame.Application.Pacing.ProjectPayoff? ActiveProjectPayoff =>
            ActiveProjectId == null || RegionComplete
                ? (WalkGame.Application.Pacing.ProjectPayoff?)null
                : WalkGame.Application.Pacing.ProjectPayoff.For(ActiveProjectInvested, ActiveProjectCost, ActiveProjectVisibleBuildStages);

        public IReadOnlyList<QueuedRow> Queued { get; }

        public int CompletedProjects { get; }

        public int TotalProjects { get; }

        public IReadOnlyList<LandmarkRow> Landmarks { get; }

        public bool AutoAdvance { get; }

        public bool HasPendingSummary { get; }

        public string? PrimaryNextAction { get; }

        /// <summary>True when the shell should request player attention, with the reason below.</summary>
        public bool RequiresAttention { get; }

        public HomeAttentionReason AttentionReason { get; }

        /// <summary>Vitality currently unallocated because no project is active/queued; 0 otherwise.</summary>
        public long BankedVitality { get; }

        /// <summary>True once the region's closure milestone completed; never resets.</summary>
        public bool RegionComplete { get; }

        /// <summary>When the region completed, so the shell can render the ending instead of an empty screen.</summary>
        public DateTimeOffset? RegionCompletedAtUtc { get; }

        /// <summary>Completed cycles of the authored post-completion program (D-061).</summary>
        public long PostCompletionCareCycles { get; }

        /// <summary>Progress invested in the post-completion program's current cycle.</summary>
        public long PostCompletionCareInvested { get; }

        /// <summary>Authored cost of one post-completion program cycle; 0 when the region authors none.</summary>
        public long PostCompletionCareCostPerCycle { get; }

        public HomeReadModel(
            string regionTitleKey,
            long vitality, long materials, long knowledge,
            string? activeProjectId, string? activeProjectTitleKey,
            long activeProjectInvested, long activeProjectCost,
            IReadOnlyList<QueuedRow> queued,
            int completedProjects, int totalProjects,
            IReadOnlyList<LandmarkRow> landmarks,
            bool autoAdvance = true,
            bool hasPendingSummary = false,
            string? primaryNextAction = null,
            bool requiresAttention = false,
            HomeAttentionReason attentionReason = HomeAttentionReason.None,
            long bankedVitality = 0,
            bool regionComplete = false,
            DateTimeOffset? regionCompletedAtUtc = null,
            long postCompletionCareCycles = 0L,
            long postCompletionCareInvested = 0L,
            long postCompletionCareCostPerCycle = 0L,
            int activeProjectVisibleBuildStages = 1)
        {
            ActiveProjectVisibleBuildStages = activeProjectVisibleBuildStages < 1 ? 1 : activeProjectVisibleBuildStages;
            RegionTitleKey = regionTitleKey;
            Vitality = vitality;
            Materials = materials;
            Knowledge = knowledge;
            ActiveProjectId = activeProjectId;
            ActiveProjectTitleKey = activeProjectTitleKey;
            ActiveProjectInvested = activeProjectInvested;
            ActiveProjectCost = activeProjectCost;
            Queued = queued;
            CompletedProjects = completedProjects;
            TotalProjects = totalProjects;
            Landmarks = landmarks;
            AutoAdvance = autoAdvance;
            HasPendingSummary = hasPendingSummary;
            PrimaryNextAction = primaryNextAction;
            RequiresAttention = requiresAttention;
            AttentionReason = attentionReason;
            BankedVitality = bankedVitality;
            RegionComplete = regionComplete;
            RegionCompletedAtUtc = regionCompletedAtUtc;
            PostCompletionCareCycles = postCompletionCareCycles;
            PostCompletionCareInvested = postCompletionCareInvested;
            PostCompletionCareCostPerCycle = postCompletionCareCostPerCycle;
        }

        public sealed class QueuedRow
        {
            public string ProjectId { get; }
            /// <summary>Display text only (D-125); blank when the project is unknown, never the id.</summary>
            public string TitleKey { get; }

            public QueuedRow(string projectId, string titleKey)
            {
                ProjectId = projectId;
                TitleKey = titleKey;
            }
        }

        public sealed class LandmarkRow
        {
            public string LandmarkId { get; }
            public string TitleKey { get; }
            public RestorationStage Stage { get; }

            public LandmarkRow(string landmarkId, string titleKey, RestorationStage stage)
            {
                LandmarkId = landmarkId;
                TitleKey = titleKey;
                Stage = stage;
            }
        }
    }
}
