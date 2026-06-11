using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Integrations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class IntegrationRepository : IIntegrationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public IntegrationRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    // ── Catalog ──────────────────────────────────────────────────────

    private const string CatalogColumns = "IntegrationId, TenantId, Name, Category, Provider, Status, Description, LogoUrl, IsEnabled, CreatedDateUtc";

    public async Task<PagedResult<IntegrationCatalogDto>> GetCatalogAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Integration.Catalog", CatalogColumns, "Name LIKE '%' + @SearchTerm + '%' OR Provider LIKE '%' + @SearchTerm + '%'", "Name ASC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<IntegrationCatalogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<IntegrationCatalogDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IntegrationCatalogDto?> GetCatalogItemByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT IntegrationId, TenantId, Name, Category, Provider, Status, Description, LogoUrl, IsEnabled, CreatedDateUtc FROM Integration.Catalog WHERE IntegrationId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<IntegrationCatalogDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    // ── Carrier Status ────────────────────────────────────────────────

    public async Task<PagedResult<CarrierIntegrationStatusDto>> GetCarrierStatusesAsync(Guid tenantId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT IntegrationConfigItemId, TenantId, Code, Name, Category, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc
    FROM Integration.IntegrationConfigItem
    WHERE TenantId = @TenantId AND Kind = 'CarrierIntegration' AND IsDeleted = 0
)
SELECT IntegrationConfigItemId AS CarrierIntegrationId,
       TenantId,
       IntegrationConfigItemId AS CarrierId,
       Name AS CarrierName,
       CASE
           WHEN IsActive = 0 THEN 'Inactive'
           WHEN NULLIF(LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))), '') IS NULL OR LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))) = '{}' THEN 'Configured'
           ELSE 'Connected'
       END AS ConnectionStatus,
       CONVERT(nvarchar(33), CreatedDateUtc, 126) AS LastCheckedUtc,
       CASE WHEN IsActive = 1 THEN CONVERT(nvarchar(33), CreatedDateUtc, 126) ELSE NULL END AS LastSuccessUtc,
       CASE
           WHEN IsActive = 0 THEN 'Carrier integration configuration is inactive.'
           WHEN NULLIF(LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))), '') IS NULL OR LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))) = '{}' THEN 'Carrier integration is active but missing configuration JSON.'
           ELSE NULL
       END AS ErrorMessage,
       CASE WHEN IsActive = 1 THEN 1 ELSE 0 END AS SuccessCount,
       CASE WHEN IsActive = 0 THEN 1 ELSE 0 END AS ErrorCount,
       CAST(CASE
           WHEN IsActive = 0 THEN 0
           WHEN NULLIF(LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))), '') IS NULL OR LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))) = '{}' THEN 85
           ELSE 99.5
       END AS float) AS UptimePercent
FROM Cte ORDER BY SortOrder ASC, Name ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Integration.IntegrationConfigItem WHERE TenantId = @TenantId AND Kind = 'CarrierIntegration' AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CarrierIntegrationStatusDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CarrierIntegrationStatusDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<CarrierIntegrationStatusDto?> GetCarrierStatusByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT IntegrationConfigItemId AS CarrierIntegrationId,
       TenantId,
       IntegrationConfigItemId AS CarrierId,
       Name AS CarrierName,
       CASE
           WHEN IsActive = 0 THEN 'Inactive'
           WHEN NULLIF(LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))), '') IS NULL OR LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))) = '{}' THEN 'Configured'
           ELSE 'Connected'
       END AS ConnectionStatus,
       CONVERT(nvarchar(33), CreatedDateUtc, 126) AS LastCheckedUtc,
       CASE WHEN IsActive = 1 THEN CONVERT(nvarchar(33), CreatedDateUtc, 126) ELSE NULL END AS LastSuccessUtc,
       CASE
           WHEN IsActive = 0 THEN 'Carrier integration configuration is inactive.'
           WHEN NULLIF(LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))), '') IS NULL OR LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))) = '{}' THEN 'Carrier integration is active but missing configuration JSON.'
           ELSE NULL
       END AS ErrorMessage,
       CASE WHEN IsActive = 1 THEN 1 ELSE 0 END AS SuccessCount,
       CASE WHEN IsActive = 0 THEN 1 ELSE 0 END AS ErrorCount,
       CAST(CASE
           WHEN IsActive = 0 THEN 0
           WHEN NULLIF(LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))), '') IS NULL OR LTRIM(RTRIM(ISNULL(ConfigurationJson, ''))) = '{}' THEN 85
           ELSE 99.5
       END AS float) AS UptimePercent
FROM Integration.IntegrationConfigItem
WHERE IntegrationConfigItemId = @Id AND Kind = 'CarrierIntegration' AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CarrierIntegrationStatusDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    // ── Download Logs ─────────────────────────────────────────────────

    private const string DownloadLogColumns = "DownloadLogId, TenantId, CarrierId, CarrierName, FeedType, Status, RecordsReceived, RecordsProcessed, RecordsFailed, StartedUtc, CompletedUtc, ErrorMessage";

    public async Task<PagedResult<DownloadLogDto>> GetDownloadLogsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Integration.DownloadLog", DownloadLogColumns, "CarrierName LIKE '%' + @SearchTerm + '%' OR FeedType LIKE '%' + @SearchTerm + '%'", "StartedUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DownloadLogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DownloadLogDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<DownloadLogDto?> GetDownloadLogByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DownloadLogId, TenantId, CarrierId, CarrierName, FeedType, Status, RecordsReceived, RecordsProcessed, RecordsFailed, StartedUtc, CompletedUtc, ErrorMessage FROM Integration.DownloadLog WHERE DownloadLogId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DownloadLogDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    // ── Download Exceptions ───────────────────────────────────────────

    private const string ExceptionColumns = "DownloadExceptionId, TenantId, DownloadLogId, CarrierId, CarrierName, ExceptionType, Message, RawPayload, ResolutionStatus, ResolvedByUserId, OccurredUtc, ResolvedUtc";

    public async Task<PagedResult<DownloadExceptionDto>> GetDownloadExceptionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Integration.DownloadException", ExceptionColumns, "CarrierName LIKE '%' + @SearchTerm + '%' OR ExceptionType LIKE '%' + @SearchTerm + '%' OR Message LIKE '%' + @SearchTerm + '%'", "OccurredUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DownloadExceptionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DownloadExceptionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<DownloadExceptionDto?> GetDownloadExceptionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DownloadExceptionId, TenantId, DownloadLogId, CarrierId, CarrierName, ExceptionType, Message, RawPayload, ResolutionStatus, ResolvedByUserId, OccurredUtc, ResolvedUtc FROM Integration.DownloadException WHERE DownloadExceptionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DownloadExceptionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task ResolveDownloadExceptionAsync(Guid id, ResolveDownloadExceptionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Integration.DownloadException
SET    ResolutionStatus  = 'Resolved',
       ResolvedByUserId  = @ResolvedByUserId,
       ResolvedUtc       = GETUTCDATE()
WHERE  DownloadExceptionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.ResolvedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task RetryDownloadExceptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Integration.DownloadException
SET    ResolutionStatus = 'Retrying'
WHERE  DownloadExceptionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    // ── Webhooks ──────────────────────────────────────────────────────

    private const string WebhookColumns = "WebhookEndpointId, TenantId, Name, TargetUrl, IsActive, SecretHash, DeliverySuccessCount, DeliveryFailureCount, CreatedDateUtc, LastTriggeredUtc";

    public async Task<PagedResult<WebhookEndpointDto>> GetWebhooksAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Integration.WebhookEndpoint", WebhookColumns, "Name LIKE '%' + @SearchTerm + '%' OR TargetUrl LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<WebhookEndpointDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<WebhookEndpointDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<WebhookEndpointDto?> GetWebhookByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT WebhookEndpointId, TenantId, Name, TargetUrl, IsActive, SecretHash, DeliverySuccessCount, DeliveryFailureCount, CreatedDateUtc, LastTriggeredUtc FROM Integration.WebhookEndpoint WHERE WebhookEndpointId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<WebhookEndpointDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateWebhookAsync(CreateWebhookEndpointRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Integration.WebhookEndpoint
    (WebhookEndpointId, TenantId, Name, TargetUrl, IsActive, SecretHash, DeliverySuccessCount, DeliveryFailureCount, CreatedDateUtc, IsDeleted)
VALUES
    (@WebhookEndpointId, @TenantId, @Name, @TargetUrl, 1, @SecretHash, 0, 0, GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            WebhookEndpointId = id,
            request.TenantId,
            request.Name,
            request.TargetUrl,
            SecretHash = request.Secret,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateWebhookAsync(Guid id, UpdateWebhookEndpointRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Integration.WebhookEndpoint
SET    Name      = @Name,
       TargetUrl = @TargetUrl,
       IsActive  = @IsActive
WHERE  WebhookEndpointId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.Name, request.TargetUrl, request.IsActive }, cancellationToken: cancellationToken));
    }

    public async Task DeleteWebhookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Integration.WebhookEndpoint SET IsDeleted = 1 WHERE WebhookEndpointId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    // ── Automation Flows ──────────────────────────────────────────────

    private const string FlowColumns = "AutomationFlowId, TenantId, Name, Description, TriggerType, Status, IsActive, RunCount, ErrorCount, CreatedDateUtc, LastRunUtc";

    public async Task<PagedResult<AutomationFlowDto>> GetAutomationFlowsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Integration.AutomationFlow", FlowColumns, "Name LIKE '%' + @SearchTerm + '%' OR TriggerType LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AutomationFlowDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AutomationFlowDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<AutomationFlowDto?> GetAutomationFlowByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT AutomationFlowId, TenantId, Name, Description, TriggerType, Status, IsActive, RunCount, ErrorCount, CreatedDateUtc, LastRunUtc FROM Integration.AutomationFlow WHERE AutomationFlowId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AutomationFlowDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAutomationFlowAsync(CreateAutomationFlowRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Integration.AutomationFlow
    (AutomationFlowId, TenantId, Name, Description, TriggerType, Status, IsActive, RunCount, ErrorCount, CreatedDateUtc, IsDeleted)
VALUES
    (@AutomationFlowId, @TenantId, @Name, @Description, @TriggerType, 'Draft', 1, 0, 0, GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AutomationFlowId = id,
            request.TenantId,
            request.Name,
            request.Description,
            request.TriggerType,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAutomationFlowAsync(Guid id, UpdateAutomationFlowRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Integration.AutomationFlow
SET    Name        = @Name,
       Description = @Description,
       TriggerType = @TriggerType,
       IsActive    = @IsActive
WHERE  AutomationFlowId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.Name, request.Description, request.TriggerType, request.IsActive }, cancellationToken: cancellationToken));
    }

    // ── Workflow Designer ─────────────────────────────────────────────

    public async Task<WorkflowDesignDto?> GetWorkflowDesignByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT WorkflowDesignId, TenantId, Name, Version, DiagramJson, Status, CreatedDateUtc, LastModifiedUtc, LastModifiedByUserId FROM Integration.WorkflowDesign WHERE WorkflowDesignId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<WorkflowDesignDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> SaveWorkflowDesignAsync(SaveWorkflowDesignRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Integration.WorkflowDesign
    (WorkflowDesignId, TenantId, Name, Version, DiagramJson, Status, CreatedDateUtc, IsDeleted)
VALUES
    (@WorkflowDesignId, @TenantId, @Name, @Version, @DiagramJson, 'Draft', GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            WorkflowDesignId = id,
            request.TenantId,
            request.Name,
            request.Version,
            request.DiagramJson,
        }, cancellationToken: cancellationToken));
        return id;
    }
}
