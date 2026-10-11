using System.Data;
using System.Text.Json;
using Dapper;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;

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
}
