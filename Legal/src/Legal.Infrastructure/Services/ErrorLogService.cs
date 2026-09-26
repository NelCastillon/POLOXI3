using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Services;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// Fail-soft enterprise error-log service used by every pipeline module.
//
// Guarantees:
//   * Never throws into the caller. If persistence fails, it falls back to ILogger and returns.
//   * Resolves the ambient tenant from IEpistemicTenantAccessor when the caller does not supply one,
//     so errors are attributed to the authenticated tenant automatically inside HTTP scopes.
//   * Uses a fresh DI scope for the repository so it can log even when raised from singleton services
//     that have no scoped connection of their own.
// ───────────────────────────────────────────────────────────────────────────────────────────────
public sealed class ErrorLogService(
    IServiceScopeFactory scopeFactory,
    ILogger<ErrorLogService> logger) : IErrorLogService
{
    public async Task LogAsync(ErrorLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();

            var tenantId = entry.TenantId
                ?? scope.ServiceProvider.GetService<IEpistemicTenantAccessor>()?.TenantId;
            var effective = tenantId == entry.TenantId ? entry : entry with { TenantId = tenantId };

            var repository = scope.ServiceProvider.GetRequiredService<IErrorLogRepository>();
            await repository.InsertAsync(effective, cancellationToken);
        }
        catch (Exception loggingFailure)
        {
            // The error log itself must never break the pipeline; degrade to the framework logger.
            logger.LogError(
                loggingFailure,
                "Failed to persist error log for module {Module} / operation {Operation}: {Message}",
                entry.Module, entry.Operation, entry.Message);
        }
    }

    public Task LogAsync(
        string module,
        Exception exception,
        string? operation = null,
        string severityCode = "Error",
        string? correlationId = null,
        string? contextJson = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new ErrorLogEntry
        {
            Module = module,
            Operation = operation,
            SeverityCode = severityCode,
            Message = exception.Message,
            ExceptionType = exception.GetType().FullName,
            StackTrace = exception.ToString(),
            Source = exception.Source,
            CorrelationId = correlationId,
            ContextJson = contextJson
        };

        return LogAsync(entry, cancellationToken);
    }
}
