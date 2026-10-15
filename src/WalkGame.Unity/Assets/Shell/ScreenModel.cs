using System.Collections.Generic;

namespace WalkGame.Shell
{
    public enum ShellScreen { Onboarding, Home, ReturnSummary, Projects, Settings, DeleteConfirm, ActivityStatus, BootError }

    /// <summary>Every screen has exactly one of these states (M5 exit criterion: normal/empty/error).</summary>
    public enum ScreenState { Normal, Empty, Error }

    public sealed class ShellAction
    {
        public ShellAction(string id, string label) { Id = id; Label = label; }
        public string Id { get; }
        public string Label { get; }
    }

    /// <summary>
    /// Engine-free description of what a screen shows. Produced only from Application read
    /// models; rendered by <see cref="ShellView"/>. Keeping it engine-free is what makes every
    /// screen testable in EditMode without a scene.
    /// </summary>
    public sealed class ScreenModel
    {
        public ScreenModel(ShellScreen screen, ScreenState state, string title)
        {
            Screen = screen; State = state; Title = title;
        }
        public ShellScreen Screen { get; }
        public ScreenState State { get; }
        public string Title { get; }
        public string? Notice { get; set; }
        public List<string> Lines { get; } = new List<string>();
        public List<ShellAction> Actions { get; } = new List<ShellAction>();
    }
}
