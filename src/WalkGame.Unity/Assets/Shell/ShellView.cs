using UnityEngine.UIElements;

namespace WalkGame.Shell
{
    /// <summary>Renders a <see cref="ScreenModel"/> into UI Toolkit elements. No game logic here.</summary>
    public static class ShellView
    {
        public static void Render(VisualElement root, ScreenModel model, System.Action<string> onAction)
        {
            root.Clear();
            root.style.paddingLeft = 24; root.style.paddingRight = 24; root.style.paddingTop = 48;
            root.Add(new Label(model.Title) { name = "title" });
            if (model.Notice != null) root.Add(new Label(model.Notice) { name = model.State == ScreenState.Error ? "error" : "notice" });
            foreach (var line in model.Lines) root.Add(new Label(line));
            foreach (var a in model.Actions)
            {
                var id = a.Id;
                root.Add(new Button(() => onAction(id)) { text = a.Label, name = id });
            }
        }
    }
}
