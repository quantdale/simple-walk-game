using System.Collections.Generic;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.ReadModels
{
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

        public HomeReadModel(
            string regionTitleKey,
            long vitality, long materials, long knowledge,
            string? activeProjectId, string? activeProjectTitleKey,
            long activeProjectInvested, long activeProjectCost,
            IReadOnlyList<QueuedRow> queued,
            int completedProjects, int totalProjects,
            IReadOnlyList<LandmarkRow> landmarks)
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
