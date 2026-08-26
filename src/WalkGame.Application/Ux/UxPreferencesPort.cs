namespace WalkGame.Application.Ux
{
    /// <summary>Outcome classification for loading the local UX-preferences record.</summary>
    public enum UxPreferencesLoadOutcome
    {
        /// <summary>Preferences loaded from disk.</summary>
        Success = 0,

        /// <summary>No preferences file exists yet — documented defaults apply.</summary>
        NotFound = 1,

        /// <summary>Payload is malformed or carries an invalid schema version — documented defaults apply.</summary>
        Malformed = 2,

        /// <summary>Payload was written by a newer schema — never interpreted; defaults apply.</summary>
        FutureVersion = 3,

        /// <summary>The file could not be read (access/IO). Defaults apply for this session.</summary>
        IoFailure = 4,
    }

    /// <summary>Load outcome plus the state when successful.</summary>
    public sealed class UxPreferencesLoadResult
    {
        public UxPreferencesLoadOutcome Outcome { get; }
        public UxPreferencesState? State { get; }

        /// <summary>Bounded technical detail for support diagnostics only.</summary>
        public string? Detail { get; }

        public UxPreferencesLoadResult(UxPreferencesLoadOutcome outcome, UxPreferencesState? state = null, string? detail = null)
        {
            Outcome = outcome;
            State = state;
            Detail = detail;
        }
    }

    /// <summary>
    /// Durable local storage port for UX preferences and onboarding progress (D-042).
    /// Implementations must commit atomically and must never store canonical game state.
    /// Save failures throw IOException; load problems are returned as classified outcomes
    /// so a broken preferences file can never block gameplay boot.
    /// </summary>
    public interface IUxPreferencesStore
    {
        UxPreferencesLoadResult Load();

        void Save(UxPreferencesState state);
    }
}
