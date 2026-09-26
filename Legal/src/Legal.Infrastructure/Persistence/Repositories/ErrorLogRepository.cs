using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;

namespace Legal.Infrastructure.Persistence.Repositories;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// Dapper persistence for the enterprise error-log store (POLOXI.Legal_ErrorLog).
// Insert is best-effort (the caller — ErrorLogService — is fail-soft). Read is tenant-scoped and
// clamps its day-window and take count so the admin surface cannot request an unbounded scan.
// ───────────────────────────────────────────────────────────────────────────────────────────────
public sealed class ErrorLogRepository(ISqlConnectionFactory connectionFactory) : IErrorLogRepository
{
    public async Task InsertAsync(ErrorLogEntry entry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO POLOXI.Legal_ErrorLog
                (ErrorLogId, Module, Operation, SeverityCode, Message, ExceptionType, StackTrace,
                 Source, CorrelationId, ContextJson, TenantId, CreatedByUserId)
            VALUES
                (NEWID(), @Module, @Operation, @SeverityCode, @Message, @ExceptionType, @StackTrace,
                 @Source, @CorrelationId, @ContextJson, @TenantId, @UserId);
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                entry.Module,
                entry.Operation,
                entry.SeverityCode,
                entry.Message,
                entry.ExceptionType,
                entry.StackTrace,
                entry.Source,
                entry.CorrelationId,
                entry.ContextJson,
                entry.TenantId,
                entry.UserId
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ErrorLogListItem>> ListAsync(
        Guid? tenantId,
        int days,
        int take,
        string? module = null,
        string? severityCode = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var clampedDays = Math.Clamp(days, 1, 365);
        var clampedTake = Math.Clamp(take, 1, 1000);

        const string sql = """
            SELECT TOP (@Take)
                ErrorLogId, Module, Operation, SeverityCode, Message, ExceptionType, StackTrace,
                Source, CorrelationId, ContextJson, TenantId, CreatedByUserId, CreatedDateUtc
            FROM POLOXI.Legal_ErrorLog
            WHERE IsDeleted = 0
              AND CreatedDateUtc >= DATEADD(DAY, -@Days, SYSUTCDATETIME())
              AND (@TenantId IS NULL OR TenantId = @TenantId OR TenantId IS NULL)
              AND (@Module IS NULL OR Module = @Module)
              AND (@SeverityCode IS NULL OR SeverityCode = @SeverityCode)
              AND (@Search IS NULL
                   OR Message LIKE '%' + @Search + '%'
                   OR ExceptionType LIKE '%' + @Search + '%'
                   OR Operation LIKE '%' + @Search + '%'
                   OR CorrelationId LIKE '%' + @Search + '%')
            ORDER BY CreatedDateUtc DESC;
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ErrorLogListItem>(new CommandDefinition(
            sql,
            new
            {
                Take = clampedTake,
                Days = clampedDays,
                TenantId = tenantId,
                Module = string.IsNullOrWhiteSpace(module) ? null : module,
                SeverityCode = string.IsNullOrWhiteSpace(severityCode) ? null : severityCode,
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()
            },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
