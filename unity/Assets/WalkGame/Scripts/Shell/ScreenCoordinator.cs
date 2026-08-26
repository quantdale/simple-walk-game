using System;
using System.Collections.Generic;

namespace WalkGame.UnityShell.Shell
{
    public enum ScreenId
    {
        Home = 0,
        Projects = 1,
        Region = 2,
        Journal = 3,
        Expeditions = 4,
        Settings = 5,
        Diagnostics = 6,
    }

    public sealed class ScreenCoordinator
    {
        private readonly Stack<ScreenId> _history = new Stack<ScreenId>();

        public ScreenId Current { get; private set; } = ScreenId.Home;

        public event Action? CurrentChanged;

        public void Show(ScreenId screen)
        {
            if (screen == Current)
                return;
            _history.Push(Current);
            Current = screen;
            CurrentChanged?.Invoke();
        }

        public bool NavigateBack()
        {
            if (_history.Count == 0)
            {
                if (Current != ScreenId.Home)
                {
                    Current = ScreenId.Home;
                    CurrentChanged?.Invoke();
                    return true;
                }
                return false;
            }

            Current = _history.Pop();
            CurrentChanged?.Invoke();
            return true;
        }

        public void ResetTo(ScreenId screen)
        {
            _history.Clear();
            var changed = Current != screen;
            Current = screen;
            if (changed)
                CurrentChanged?.Invoke();
        }
    }
}
