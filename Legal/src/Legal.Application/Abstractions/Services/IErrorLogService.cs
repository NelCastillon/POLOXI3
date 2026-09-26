namespace Legal.Application.Abstractions.Services;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// Enterprise error-logging surface. Every pipeline module (API request filter, IntelligenceWide2
// decision pipeline, LegalRetriever, OfficialLegalAuthorityRetriever, workers, ...) captures errors
// through this single contract, so detailed messages and full stack traces land in one DB-backed store.
//
// Contract guarantee: implementations MUST be fail-soft. A logging failure must never propagate into
// the calling operation. Callers should therefore never wrap LogAsync in their own try/catch.
// ───────────────────────────────────────────────────────────────────────────────────────────────
public interface IErrorLogService
{
    /// <summary>Records an error/exception. Never throws; swallows its own failures.</summary>
    Task LogAsync(ErrorLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Convenience overload that captures an exception's type, message and stack trace.</summary>
    Task LogAsync(
        string module,
        Exception exception,
        string? operation = null,
        string severityCode = "Error",
        string? correlationId = null,
        string? contextJson = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Write model for a single error-log record. Tenant/user attribution is resolved by the service.</summary>
public sealed record ErrorLogEntry
{
    public required string Module { get; init; }
    public string? Operation { get; init; }
    public string SeverityCode { get; init; } = "Error";
    public required string Message { get; init; }
    public string? ExceptionType { get; init; }
    public string? StackTrace { get; init; }
    public string? Source { get; init; }
    public string? CorrelationId { get; init; }
    public string? ContextJson { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? UserId { get; init; }
}

/// <summary>Read model returned to the admin dashboard.</summary>
public sealed record ErrorLogListItem(
    Guid ErrorLogId,
    string Module,
    string? Operation,
    string SeverityCode,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? Source,
    string? CorrelationId,
    string? ContextJson,
    Guid? TenantId,
    Guid? CreatedByUserId,
    DateTime CreatedDateUtc);

/// <summary>No-op error log used by hosts/tests that do not persist errors. Always succeeds silently.</summary>
public sealed class NullErrorLogService : IErrorLogService
{
    public static readonly NullErrorLogService Instance = new();

    public Task LogAsync(ErrorLogEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task LogAsync(
        string module,
        Exception exception,
        string? operation = null,
        string severityCode = "Error",
        string? correlationId = null,
        string? contextJson = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
