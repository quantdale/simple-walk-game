using System;
using System.Collections.Generic;
using System.Globalization;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Projects;
using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Screens
{
    public static class ProjectsScreen
    {
        public static VisualElement Build(AppShell shell)
        {
            var session = AppHost.Instance.Graph.Session;
            var container = Ui.Column(10);
            if (!session.HasLoadedState)
                return container;

            var model = session.GetProjects();

            var autoCard = Ui.Card();
            autoCard.Add(Ui.SectionHeader("Automation"));
            autoCard.Add(Ui.Body("Auto-advance keeps the queue moving without you."));
            autoCard.Add(Ui.ToggleButton(
                model.AutoAdvance ? "Auto-advance: On" : "Auto-advance: Off",
                model.AutoAdvance,
                () =>
                {
                    var result = session.SetAutoAdvance(!model.AutoAdvance);
                    shell.ShowFeedback(result);
                    shell.Refresh();
                }));
            container.Add(autoCard);

            var active = new List<ProjectsReadModel.ProjectRow>();
            var queued = new List<ProjectsReadModel.ProjectRow>();
            var available = new List<ProjectsReadModel.ProjectRow>();
            var locked = new List<ProjectsReadModel.ProjectRow>();
            var completed = new List<ProjectsReadModel.ProjectRow>();

            foreach (var p in model.Projects)
            {
                switch (p.Status)
                {
                    case ProjectStatus.Active: active.Add(p); break;
                    case ProjectStatus.Queued: queued.Add(p); break;
                    case ProjectStatus.Available: available.Add(p); break;
                    case ProjectStatus.Locked: locked.Add(p); break;
                    case ProjectStatus.Completed: completed.Add(p); break;
                }
            }

            if (model.Projects.Count == 0)
            {
                container.Add(Ui.Card());
                container.Add(Ui.Muted("No projects are defined yet."));
                return container;
            }

            foreach (var p in active)
                container.Add(ProjectCard(shell, session, p, "Active now", true));

            if (queued.Count > 0)
            {
                container.Add(Ui.SectionHeader("Queue"));
                for (int i = 0; i < queued.Count; i++)
                    container.Add(QueuedCard(shell, session, queued, i));
            }

            if (available.Count > 0)
            {
                container.Add(Ui.SectionHeader("Ready to plan"));
                foreach (var p in available)
                    container.Add(ProjectCard(shell, session, p, "Available", false));
            }

            if (locked.Count > 0)
            {
                container.Add(Ui.SectionHeader("Locked"));
                foreach (var p in locked)
                    container.Add(LockedCard(p));
            }

            if (completed.Count > 0)
            {
                container.Add(Ui.SectionHeader("Completed"));
                foreach (var p in completed)
                {
                    var card = Ui.Card();
                    card.Add(Ui.Body(CopyTable.Text(p.TitleKey)));
                    card.Add(Ui.StatusLine("Completed", Ui.Accent));
                    container.Add(card);
                }
            }

            return container;
        }

        private static VisualElement QueuedCard(
            AppShell shell, WalkGame.Application.GameSession session,
            List<ProjectsReadModel.ProjectRow> queued, int index)
        {
            var p = queued[index];
            var card = Ui.Card();
            card.Add(Ui.Body((index + 1) + ". " + CopyTable.Text(p.TitleKey)));

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;

            if (index > 0)
                actions.Add(MiniButton("Up", () => Reorder(shell, session, queued, index, index - 1)));
            if (index < queued.Count - 1)
                actions.Add(MiniButton("Down", () => Reorder(shell, session, queued, index, index + 1)));
            actions.Add(MiniButton("Start now", () =>
            {
                shell.ShowFeedback(session.ActivateQueuedProject(p.ProjectId));
                shell.Refresh();
            }));
            actions.Add(MiniButton("Remove", () =>
            {
                shell.ShowFeedback(session.DequeueProject(p.ProjectId));
                shell.Refresh();
            }));

            card.Add(actions);
            return card;
        }

        private static void Reorder(
            AppShell shell, WalkGame.Application.GameSession session,
            List<ProjectsReadModel.ProjectRow> queued, int from, int to)
        {
            var ids = new List<string>();
            foreach (var row in queued)
                ids.Add(row.ProjectId);
            (ids[from], ids[to]) = (ids[to], ids[from]);
            shell.ShowFeedback(session.ReorderQueue(ids));
            shell.Refresh();
        }

        private static VisualElement ProjectCard(
            AppShell shell, WalkGame.Application.GameSession session,
            ProjectsReadModel.ProjectRow p, string statusWord, bool isActive)
        {
            var card = Ui.Card();
            card.Add(Ui.Body(CopyTable.Text(p.TitleKey)));
            card.Add(Ui.StatusLine(statusWord, Ui.TextMuted));

            float frac = p.VitalityCost > 0 ? (float)p.VitalityInvested / p.VitalityCost : 0f;
            card.Add(Ui.ProgressBar(frac, out _));
            card.Add(Ui.Muted(HomeScreen.FormatUnits(p.VitalityInvested) + " / " +
                              HomeScreen.FormatUnits(p.VitalityCost) + " vitality"));

            if (!isActive)
                card.Add(Ui.GhostButton("Add to queue", () =>
                {
                    shell.ShowFeedback(session.EnqueueProject(p.ProjectId));
                    shell.Refresh();
                }));

            return card;
        }

        private static VisualElement LockedCard(ProjectsReadModel.ProjectRow p)
        {
            var card = Ui.Card();
            card.Add(Ui.Body(CopyTable.Text(p.TitleKey)));
            card.Add(Ui.StatusLine("Locked — finish earlier restorations first", Ui.Warn));
            if (p.PrerequisiteIds.Count > 0)
                card.Add(Ui.Muted(p.PrerequisiteIds.Count + " prerequisite project(s)"));
            return card;
        }

        private static Button MiniButton(string text, Action onClick)
        {
            var b = Ui.GhostButton(text, onClick);
            b.style.height = 36;
            b.style.fontSize = 13;
            b.style.marginRight = 6;
            return b;
        }
    }
}
