using Legal.Application.Abstractions.Services;

namespace Legal.Application.Abstractions.Persistence;

// Persistence contract for the enterprise error-log store (POLOXI.Legal_ErrorLog).
public interface IErrorLogRepository
{
    /// <summary>Inserts a single error-log row.</summary>
    Task InsertAsync(ErrorLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent error-log rows within the given day window, newest first.
    /// When <paramref name="tenantId"/> is null the query is not tenant-filtered (system-admin view);
    /// otherwise rows for that tenant (and global NULL-tenant rows) are returned.
    /// </summary>
    Task<IReadOnlyList<ErrorLogListItem>> ListAsync(
        Guid? tenantId,
        int days,
        int take,
        string? module = null,
        string? severityCode = null,
        string? search = null,
        CancellationToken cancellationToken = default);
}
