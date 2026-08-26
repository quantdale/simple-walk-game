using WalkGame.Application.Activity;
using WalkGame.Application.Development;

namespace WalkGame.UnityShell.Development
{
    public static class DevActivityGate
    {
        public const long DefaultStepsPerDay = 8000;
        public const int DefaultWindowDays = 1;

        public static bool Enabled
        {
            get { return IsDevBuild; }
        }

        private static bool IsDevBuild
        {
            get
            {
#if WALKGAME_DEV_TOOLS
                return true;
#else
                return false;
#endif
            }
        }

        public static IActivityRecordSource? CreateSourceIfEnabled()
        {
            return Enabled ? CreateSource(DefaultStepsPerDay) : null;
        }

        public static IActivityRecordSource CreateSource(long stepsPerDay)
        {
            return new SyntheticWalkingSource(stepsPerDay);
        }
    }
}
