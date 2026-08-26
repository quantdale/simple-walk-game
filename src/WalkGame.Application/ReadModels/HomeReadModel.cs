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
            long bankedVitality = 0)
        {
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
        }

        public sealed class QueuedRow
        {
            public string ProjectId { get; }
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
