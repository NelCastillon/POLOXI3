using System.Data;
using System.Text.Json;
using Dapper;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// Read side of the full POLOXI Wide (Decision2) result capture. Reconstructs the exact WideSearchResponse
// from the lossless DetailJson persisted per analysis run, so Analysis History can reopen the entire
// Decision2 Display read-only. Tenant-scoped. The normalized child tables remain the queryable store;
// DetailJson guarantees verbatim fidelity of the rendered display including all nested composer objects.
public sealed class WideRunResultRepository(ISqlConnectionFactory connectionFactory) : IWideRunResultRepository
{
    public async Task<WideSearchResponse?> GetByAnalysisRunAsync(
        Guid tenantId, Guid analysisRunId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var detailJson = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            SELECT TOP 1 r.DetailJson
            FROM POLOXI.Legal_WideRunResult r
            WHERE r.TenantId = @TenantId AND r.AnalysisRunId = @AnalysisRunId AND r.IsDeleted = 0
            ORDER BY r.CapturedUtc DESC;
            """,
            new { TenantId = tenantId, AnalysisRunId = analysisRunId },
            cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(detailJson))
            return null;

        return JsonSerializer.Deserialize<WideSearchResponse>(detailJson);
    }

    // Persists the Next Best Action result as JSON onto the latest captured Wide run row for the analysis
    // run. Reopening the saved run then serves the NBA panel from the DB with no fresh LLM call.
    public async Task SaveNextBestActionAsync(
        Guid tenantId, Guid analysisRunId, NextBestActionResult nextBestAction,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var json = JsonSerializer.Serialize(nextBestAction);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE r
            SET r.NextBestActionJson = @NextBestActionJson
            FROM POLOXI.Legal_WideRunResult r
            WHERE r.WideRunResultId =
            (
                SELECT TOP 1 r2.WideRunResultId
                FROM POLOXI.Legal_WideRunResult r2
                WHERE r2.TenantId = @TenantId AND r2.AnalysisRunId = @AnalysisRunId AND r2.IsDeleted = 0
                ORDER BY r2.CapturedUtc DESC
            );
            """,
            new { TenantId = tenantId, AnalysisRunId = analysisRunId, NextBestActionJson = json },
            cancellationToken: cancellationToken));
    }

    // Reads the persisted Next Best Action result for one analysis run, or null when none was captured.
    public async Task<NextBestActionResult?> GetNextBestActionByAnalysisRunAsync(
        Guid tenantId, Guid analysisRunId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var json = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            SELECT TOP 1 r.NextBestActionJson
            FROM POLOXI.Legal_WideRunResult r
            WHERE r.TenantId = @TenantId AND r.AnalysisRunId = @AnalysisRunId AND r.IsDeleted = 0
            ORDER BY r.CapturedUtc DESC;
            """,
            new { TenantId = tenantId, AnalysisRunId = analysisRunId },
            cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<NextBestActionResult>(json);
    }
}
