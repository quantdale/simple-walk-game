using System;
using System.Collections.Generic;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Domain.Validation
{
    /// <summary>
    /// Static validation for content definitions. Invalid content must fail validation,
    /// never appear as silent runtime corruption.
    /// </summary>
    public static class ContentValidator
    {
        public static List<string> Validate(RegionDefinition content)
        {
            var violations = new List<string>();
            if (content == null)
            {
                violations.Add("Region definition is null.");
                return violations;
            }

            var projectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in content.Projects)
            {
                if (!projectIds.Add(project.Id.Value))
                    violations.Add($"Duplicate project ID '{project.Id}'.");
                if (project.VitalityCost <= 0L)
                    violations.Add($"Project '{project.Id}' has non-positive cost.");
                foreach (var prerequisite in project.Prerequisites)
                    if (!projectIds.Contains(prerequisite.Value))
                        violations.Add($"Project '{project.Id}' references missing prerequisite '{prerequisite}'.");
            }

            var landmarkIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var landmark in content.Landmarks)
            {
                if (!landmarkIds.Add(landmark.Id.Value))
                    violations.Add($"Duplicate landmark ID '{landmark.Id}'.");

                RestorationStage previous = RestorationStage.Ruined;
                foreach (var stage in landmark.Stages)
                {
                    if (!projectIds.Contains(stage.UnlockedByProjectId))
                        violations.Add($"Landmark '{landmark.Id}' stage {stage.Stage} references missing project '{stage.UnlockedByProjectId}'.");
                    if (stage.Stage < previous)
                        violations.Add($"Landmark '{landmark.Id}' stages are not ascending.");
                    previous = stage.Stage;
                }
            }

            var producerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var producer in content.Producers)
            {
                if (!producerIds.Add(producer.Id.Value))
                    violations.Add($"Duplicate producer ID '{producer.Id}'.");
                if (!projectIds.Contains(producer.UnlockedByProjectId))
                    violations.Add($"Producer '{producer.Id}' references missing unlock project '{producer.UnlockedByProjectId}'.");
            }

            bool hasReachableEntry = false;
            foreach (var project in content.Projects)
            {
                if (project.Prerequisites.Count != 0)
                    continue;
                hasReachableEntry = true;
                break;
            }
            if (!hasReachableEntry)
                violations.Add("Region has no entry project (all projects have prerequisites).");

            var cycleRoot = FindAnyCycle(content);
            if (cycleRoot != null)
                violations.Add($"Project prerequisite chain contains a cycle at '{cycleRoot}'.");

            return violations;
        }

        private static string? FindAnyCycle(Regions.RegionDefinition content)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack = new HashSet<string>(StringComparer.Ordinal);

            bool Visit(string projectId)
            {
                if (visited.Contains(projectId))
                    return false;
                if (!stack.Add(projectId))
                    return true;

                var definition = content.FindProject(projectId);
                if (definition != null)
                {
                    foreach (var prerequisite in definition.Prerequisites)
                        if (Visit(prerequisite.Value))
                            return true;
                }

                stack.Remove(projectId);
                visited.Add(projectId);
                return false;
            }

            foreach (var project in content.Projects)
                if (Visit(project.Id.Value))
                    return project.Id.Value;
            return null;
        }
    }
}
