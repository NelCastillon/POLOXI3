using Dapper;
using Legal.Application.Abstractions.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Legal.Infrastructure.Intelligence;

/// <summary>
/// Surfaces the silent "corpus-to-decision" bridge gap: uploaded Matter Corpus documents are
/// projected into the live search index (<c>AI.Legal_SearchDocument</c>) as
/// <c>LEGAL_DOCUMENT_PASSAGE</c>, but the live POLOXI grounding path only retrieves evidence for
/// entity types that exist as an active <c>POLOXI.Legal_Capability</c> with
/// <c>ExecutionHandlerCode = N'AUTHORIZED_SEARCH_DOCUMENT'</c>. If passages exist without that
/// capability, every decision run reports "Documents: AVAILABLE / Evidence: NONE" and no error is
/// raised. This health check makes that condition visible (Degraded) instead of failing silently.
/// </summary>
public sealed class PoloxiDocumentPassageBridgeHealthCheck(ISqlConnectionFactory connectionFactory) : IHealthCheck
{
    private const string EntityTypeCode = "LEGAL_DOCUMENT_PASSAGE";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF OBJECT_ID(N'AI.Legal_SearchDocument',N'U') IS NULL OR OBJECT_ID(N'POLOXI.Legal_Capability',N'U') IS NULL
    SELECT CONVERT(bit,0) SchemaReady,CONVERT(int,0) PassageCount,CONVERT(int,0) CapabilityCount;
ELSE
    SELECT CONVERT(bit,1) SchemaReady,
    (SELECT COUNT_BIG(1) FROM AI.Legal_SearchDocument WHERE EntityTypeCode=@EntityTypeCode AND IsDeleted=0) PassageCount,
    (SELECT COUNT_BIG(1) FROM POLOXI.Legal_Capability WHERE EntityTypeCode=@EntityTypeCode AND ExecutionHandlerCode=N'AUTHORIZED_SEARCH_DOCUMENT' AND IsActive=1 AND IsDeleted=0) CapabilityCount;
""";

        try
        {
            using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var row = await connection.QuerySingleAsync<BridgeStatusRow>(new CommandDefinition(sql, new { EntityTypeCode }, cancellationToken: cancellationToken));

            if (!row.SchemaReady)
                return HealthCheckResult.Healthy("POLOXI document-passage bridge not applicable: search index or capability catalog not yet provisioned.");

            var data = new Dictionary<string, object>
            {
                ["passageCount"] = row.PassageCount,
                ["capabilityCount"] = row.CapabilityCount,
                ["entityTypeCode"] = EntityTypeCode,
            };

            if (row.PassageCount > 0 && row.CapabilityCount == 0)
                return HealthCheckResult.Degraded(
                    $"{row.PassageCount} '{EntityTypeCode}' passage(s) are indexed for search, but no active POLOXI capability with ExecutionHandlerCode='AUTHORIZED_SEARCH_DOCUMENT' exists. Uploaded Matter Corpus documents cannot contribute evidence to POLOXI decisions until this capability is seeded (see migration 0346).",
                    data: data);

            return HealthCheckResult.Healthy(
                row.CapabilityCount > 0
                    ? $"POLOXI document-passage bridge active: {row.CapabilityCount} capability, {row.PassageCount} indexed passage(s)."
                    : "POLOXI document-passage bridge idle: no indexed passages awaiting grounding.",
                data: data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Failed to evaluate the POLOXI document-passage bridge health check.", exception);
        }
    }

    private sealed record BridgeStatusRow(bool SchemaReady, long PassageCount, long CapabilityCount);
}
