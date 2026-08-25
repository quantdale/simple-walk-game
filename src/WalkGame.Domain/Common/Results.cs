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
        public const string NotAvailable = "project.not-available";
        public const string ProjectAlreadyActive = "project.already-active";
        public const string QueueEmpty = "queue.empty";
        public const string InvalidQueueOrder = "queue.invalid-order";
        public const string InvalidArgument = "domain.invalid-argument";
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

    public readonly struct DomainResult<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public DomainError? Error { get; }

        private DomainResult(T value)
        {
            IsSuccess = true;
            Value = value;
            Error = null;
        }

        private DomainResult(DomainError error)
        {
            IsSuccess = false;
            Value = default;
            Error = error;
        }

        public static DomainResult<T> Ok(T value) => new DomainResult<T>(value);

        public static DomainResult<T> Fail(string code, string message) =>
            new DomainResult<T>(new DomainError(code, message));

        public static DomainResult<T> Fail(DomainError error) => new DomainResult<T>(error);
    }
}
