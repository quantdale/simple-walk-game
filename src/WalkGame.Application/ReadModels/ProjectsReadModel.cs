using System.Collections.Generic;
using WalkGame.Domain.Projects;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Purpose-built read model for the Projects management screen: every project with
    /// status, effort, prerequisites and queue position, plus the persisted automation
    /// switch. Presentation renders this snapshot; it never inspects domain graphs.
    /// </summary>
    public sealed class ProjectsReadModel
    {
        public bool AutoAdvance { get; }

        public string? ActiveProjectId { get; }

        public IReadOnlyList<ProjectRow> Projects { get; }

        public ProjectsReadModel(bool autoAdvance, string? activeProjectId, IReadOnlyList<ProjectRow> projects)
        {
            AutoAdvance = autoAdvance;
            ActiveProjectId = activeProjectId;
            Projects = projects;
        }

        public sealed class ProjectRow
        {
            public string ProjectId { get; }
            public string TitleKey { get; }
            public long VitalityCost { get; }
            public long VitalityInvested { get; }
            public ProjectStatus Status { get; }

            /// <summary>Zero-based position in the ordered queue; null when not queued.</summary>
            public int? QueuedPosition { get; }

            public IReadOnlyList<string> PrerequisiteIds { get; }

            public ProjectRow(
                string projectId,
                string titleKey,
                long vitalityCost,
                long vitalityInvested,
                ProjectStatus status,
                int? queuedPosition,
                IReadOnlyList<string> prerequisiteIds)
            {
                ProjectId = projectId;
                TitleKey = titleKey;
                VitalityCost = vitalityCost;
                VitalityInvested = vitalityInvested;
                Status = status;
                QueuedPosition = queuedPosition;
                PrerequisiteIds = prerequisiteIds;
            }
        }
    }
}
