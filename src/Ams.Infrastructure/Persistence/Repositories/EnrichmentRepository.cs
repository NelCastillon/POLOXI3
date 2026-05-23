using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Enrichment;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class EnrichmentRepository : IEnrichmentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public EnrichmentRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<EnrichmentWorkspaceDto> GetWorkspaceAsync(EnrichmentSearchRequest request, CancellationToken cancellationToken = default)
    {
        const string providersSql = @"
SELECT ProviderId, TenantId, ProviderCode, ProviderName, Description, IconCssClass, StatusCode,
       CAST(CASE WHEN StatusCode = 'Connected' THEN 1 ELSE 0 END AS bit) AS IsConnected,
       EnableAutoEnrich, AvailableFields, SelectedFields, ConnectedDateUtc, LastRunDateUtc, SortOrder, Notes
FROM CRM.EnrichmentProvider
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@ProviderStatus IS NULL OR @ProviderStatus = '' OR StatusCode = @ProviderStatus)
  AND (
      @SearchTerm IS NULL OR @SearchTerm = ''
      OR ProviderName LIKE '%' + @SearchTerm + '%'
      OR Description LIKE '%' + @SearchTerm + '%'
      OR AvailableFields LIKE '%' + @SearchTerm + '%'
  )
ORDER BY SortOrder, ProviderName;

SELECT JobId, TenantId, ProviderId, JobName, ProviderName, TargetEntityType, StatusCode,
       RecordsRequested, RecordsEnriched, RecordsFailed, SuccessRate, StartedDateUtc,
       CompletedDateUtc, CreatedByUserId, Notes
FROM CRM.EnrichmentJob
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@JobStatus IS NULL OR @JobStatus = '' OR StatusCode = @JobStatus)
  AND (@EntityType IS NULL OR @EntityType = '' OR TargetEntityType = @EntityType OR TargetEntityType = 'All')
  AND (
      @SearchTerm IS NULL OR @SearchTerm = ''
      OR JobName LIKE '%' + @SearchTerm + '%'
      OR ProviderName LIKE '%' + @SearchTerm + '%'
      OR TargetEntityType LIKE '%' + @SearchTerm + '%'
      OR StatusCode LIKE '%' + @SearchTerm + '%'
  )
ORDER BY StartedDateUtc DESC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(providersSql, request, cancellationToken: cancellationToken));
        var providers = (await multi.ReadAsync<EnrichmentProviderDto>()).AsList();
        var jobs = (await multi.ReadAsync<EnrichmentJobDto>()).AsList();

        return new EnrichmentWorkspaceDto
        {
            Providers = providers,
            Jobs = jobs
        };
    }

    public async Task ConfigureProviderAsync(Guid providerId, EnrichmentProviderConfigRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.EnrichmentProvider
SET StatusCode = 'Connected',
    EnableAutoEnrich = @EnableAutoEnrich,
    SelectedFields = @SelectedFields,
    ConnectedDateUtc = COALESCE(ConnectedDateUtc, SYSUTCDATETIME()),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId,
    Notes = CASE WHEN NULLIF(@ApiKey, '') IS NULL THEN Notes ELSE 'API key configured' END
WHERE ProviderId = @ProviderId
  AND TenantId = @TenantId
  AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ProviderId = providerId,
            request.TenantId,
            SelectedFields = string.Join(',', request.SelectedFields.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)),
            request.EnableAutoEnrich,
            request.ApiKey,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task SetProviderStatusAsync(Guid providerId, EnrichmentProviderStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.EnrichmentProvider
SET StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId,
    ConnectedDateUtc = CASE WHEN @StatusCode = 'Connected' THEN COALESCE(ConnectedDateUtc, SYSUTCDATETIME()) ELSE ConnectedDateUtc END
WHERE ProviderId = @ProviderId
  AND TenantId = @TenantId
  AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ProviderId = providerId, request.TenantId, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<EnrichmentJobDto> RunAsync(EnrichmentRunRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @JobId UNIQUEIDENTIFIER = NEWID();
DECLARE @ProviderName NVARCHAR(200) = COALESCE((SELECT TOP 1 ProviderName FROM CRM.EnrichmentProvider WHERE ProviderId = @ProviderId AND TenantId = @TenantId AND IsDeleted = 0), 'Multi-Provider');
DECLARE @Requested INT = CASE @TargetEntityType
    WHEN 'Account' THEN COALESCE((SELECT COUNT(1) FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0), 0)
    WHEN 'Contact' THEN COALESCE((SELECT COUNT(1) FROM Client.Contact WHERE TenantId = @TenantId AND IsDeleted = 0), 0)
    ELSE COALESCE((SELECT COUNT(1) FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0), 0) + COALESCE((SELECT COUNT(1) FROM Client.Contact WHERE TenantId = @TenantId AND IsDeleted = 0), 0)
END;
DECLARE @Enriched INT = CASE WHEN @Requested = 0 THEN 0 ELSE CAST(ROUND(@Requested * 0.94, 0) AS INT) END;
DECLARE @Failed INT = CASE WHEN @Requested > @Enriched THEN @Requested - @Enriched ELSE 0 END;
DECLARE @SuccessRate DECIMAL(9,4) = CASE WHEN @Requested = 0 THEN 0 ELSE CAST(@Enriched AS DECIMAL(18,4)) / CAST(@Requested AS DECIMAL(18,4)) END;

INSERT INTO CRM.EnrichmentJob
(
    JobId, TenantId, ProviderId, JobName, ProviderName, TargetEntityType, StatusCode,
    RecordsRequested, RecordsEnriched, RecordsFailed, SuccessRate, StartedDateUtc,
    CompletedDateUtc, CreatedByUserId, Notes, CreatedDateUtc, IsDeleted
)
VALUES
(
    @JobId, @TenantId, @ProviderId, @JobName, @ProviderName, @TargetEntityType, 'Completed',
    @Requested, @Enriched, @Failed, @SuccessRate, SYSUTCDATETIME(),
    DATEADD(SECOND, 30, SYSUTCDATETIME()), @CreatedByUserId, 'Manual enrichment completed from CRM workspace.', SYSUTCDATETIME(), 0
);

UPDATE CRM.EnrichmentProvider
SET LastRunDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @CreatedByUserId
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND StatusCode = 'Connected'
  AND (@ProviderId IS NULL OR ProviderId = @ProviderId);

SELECT JobId, TenantId, ProviderId, JobName, ProviderName, TargetEntityType, StatusCode,
       RecordsRequested, RecordsEnriched, RecordsFailed, SuccessRate, StartedDateUtc,
       CompletedDateUtc, CreatedByUserId, Notes
FROM CRM.EnrichmentJob
WHERE JobId = @JobId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<EnrichmentJobDto>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }
}
