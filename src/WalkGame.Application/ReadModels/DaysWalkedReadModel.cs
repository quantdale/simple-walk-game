namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Days walked (D-103). <see cref="IsLowerBound"/> is true for migrated legacy saves and
    /// whenever a walk arrived without a local date; the shell then says "at least".
    /// </summary>
    public sealed class DaysWalkedReadModel
    {
        public DaysWalkedReadModel(int days, bool isLowerBound)
        {
            Days = days;
            IsLowerBound = isLowerBound;
        }

        public int Days { get; }

        public bool IsLowerBound { get; }
    }
}
