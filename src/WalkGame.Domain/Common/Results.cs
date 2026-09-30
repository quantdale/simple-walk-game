namespace WalkGame.Domain.Common
{
    /// <summary>Stable error codes surfaced through <see cref="DomainError"/>.</summary>
    public static class ErrorCodes
    {
        public const string UnknownProject = "project.unknown";
        public const string PrerequisiteNotMet = "project.prerequisite-not-met";
        public const string AlreadyQueued = "project.already-queued";
        public const string NotQueued = "project.not-queued";
        public const string AlreadyCompleted = "project.already-completed";
        public const string ProjectAlreadyActive = "project.already-active";
        public const string InvalidQueueOrder = "queue.invalid-order";

        /// <summary>
        /// The project's secondary-resource requirement is not met (D-054). Distinct from
        /// a Vitality shortfall, which is not an error at all: Vitality banks until it is
        /// affordable. A secondary requirement is a gate the player must choose to satisfy.
        /// </summary>
        public const string ResourceRequirementNotMet = "project.resource-requirement-not-met";
        public const string DiscoveryUnknown = "discovery.unknown";
        public const string DiscoveryNotUnlocked = "discovery.not-unlocked";

        // D-057: `NotAvailable`, `QueueEmpty` and `InvalidArgument` were removed. Each was
        // declared here and returned by nothing, so a reader of this file reasonably
        // concluded the system distinguished those conditions somewhere. None of their
        // STRING values was ever persisted, transmitted, or used as a localisation key —
        // verified by grep across docs/, scripts/ and the save/JSON paths before removal.
        //
        // `ErrorCodeCoverageTests` now fails the build if a declared code is returned by
        // nothing, so this class cannot silently accumulate dead conditions again.
    }

    public sealed class DomainError
    {
        public string Code { get; }
        public string Message { get; }

        public DomainError(string code, string message)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString() => $"{Code}: {Message}";
    }

    public readonly struct DomainResult
    {
        public bool IsSuccess { get; }
        public DomainError? Error { get; }

        private DomainResult(bool isSuccess, DomainError? error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public static DomainResult Ok() => new DomainResult(true, null);

        public static DomainResult Fail(string code, string message) =>
            new DomainResult(false, new DomainError(code, message));

        public static DomainResult Fail(DomainError error) => new DomainResult(false, error);

        public static DomainResult From(bool ok, string errorCode, string message) =>
            ok ? Ok() : Fail(errorCode, message);
    }

    // D-057: `DomainResult<T>` was removed. It had zero production and tool references; the
    // only uses were two lines in its own unit test. It read as a supported shape for a
    // value-returning use case that does not exist, which is a small lie about the
    // contract. When a value-returning use case actually arrives it will choose its result
    // type against a real requirement — see D-057.
}
