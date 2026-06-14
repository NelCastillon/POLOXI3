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

    private const string DownloadLogColumns = "DownloadLogId, TenantId, CarrierId, CarrierName, FeedType, Status, RecordsReceived, RecordsProcessed, RecordsFailed, StartedUtc, CompletedUtc, FileName, RawStorageUri, ErrorMessage";

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
        const string sql = "SELECT DownloadLogId, TenantId, CarrierId, CarrierName, FeedType, Status, RecordsReceived, RecordsProcessed, RecordsFailed, StartedUtc, CompletedUtc, FileName, RawStorageUri, ErrorMessage FROM Integration.DownloadLog WHERE DownloadLogId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DownloadLogDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<CarrierDownloadDashboardDto> GetCarrierDownloadDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COUNT(1) AS TotalBatches,
       SUM(CASE WHEN Status = 'Received' THEN 1 ELSE 0 END) AS ReceivedBatches,
       SUM(CASE WHEN Status IN ('Parsing', 'Parsed', 'Matching', 'Processing') THEN 1 ELSE 0 END) AS ProcessingBatches,
       SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) AS CompletedBatches,
       SUM(CASE WHEN Status = 'CompletedWithErrors' THEN 1 ELSE 0 END) AS CompletedWithErrorsBatches,
       SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) AS FailedBatches
FROM Integration.CarrierDownloadBatch
WHERE TenantId = @TenantId AND IsDeleted = 0;

SELECT COUNT(1) AS TotalItems,
       SUM(CASE WHEN MatchStatus = 'AutoMatched' THEN 1 ELSE 0 END) AS AutoMatchedItems,
       SUM(CASE WHEN MatchStatus = 'Exception' THEN 1 ELSE 0 END) AS ExceptionItems
FROM Integration.CarrierDownloadItem
WHERE TenantId = @TenantId AND IsDeleted = 0;

SELECT COUNT(1) AS OpenExceptions,
       SUM(CASE WHEN Severity = 'High' THEN 1 ELSE 0 END) AS HighSeverityExceptions
FROM Integration.CarrierDownloadException
WHERE TenantId = @TenantId AND Status IN ('Open', 'Retrying') AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var batch = await multi.ReadSingleAsync<CarrierDownloadDashboardDto>();
        var item = await multi.ReadSingleAsync<CarrierDownloadDashboardDto>();
        var exceptions = await multi.ReadSingleAsync<CarrierDownloadDashboardDto>();

        batch.TotalItems = item.TotalItems;
        batch.AutoMatchedItems = item.AutoMatchedItems;
        batch.ExceptionItems = item.ExceptionItems;
        batch.OpenExceptions = exceptions.OpenExceptions;
        batch.HighSeverityExceptions = exceptions.HighSeverityExceptions;
        return batch;
    }

    private const string CarrierDownloadItemColumns = @"i.CarrierDownloadItemId, i.TenantId, i.CarrierDownloadBatchId, b.CarrierName, i.TransactionType, i.CarrierPolicyNumber, i.NamedInsured, i.EffectiveDate, i.ExpirationDate, i.LineOfBusiness, i.Premium, i.Commission, i.RawPayload, i.NormalizedPayload, i.MatchStatus, i.ProcessingStatus, i.ErrorMessage, i.CreatedDateUtc";

    public async Task<PagedResult<CarrierDownloadItemDto>> GetCarrierDownloadItemsAsync(Guid tenantId, Guid? batchId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT i.CarrierDownloadItemId, i.TenantId, i.CarrierDownloadBatchId, b.CarrierName, i.TransactionType, i.CarrierPolicyNumber, i.NamedInsured, i.EffectiveDate, i.ExpirationDate, i.LineOfBusiness, i.Premium, i.Commission, i.RawPayload, i.NormalizedPayload, i.MatchStatus, i.ProcessingStatus, i.ErrorMessage, i.CreatedDateUtc
    FROM Integration.CarrierDownloadItem i
    JOIN Integration.CarrierDownloadBatch b ON b.CarrierDownloadBatchId = i.CarrierDownloadBatchId
    WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND b.IsDeleted = 0
      AND (@BatchId IS NULL OR i.CarrierDownloadBatchId = @BatchId)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR i.CarrierPolicyNumber LIKE '%' + @SearchTerm + '%' OR i.NamedInsured LIKE '%' + @SearchTerm + '%' OR i.TransactionType LIKE '%' + @SearchTerm + '%' OR i.LineOfBusiness LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Integration.CarrierDownloadItem i
JOIN Integration.CarrierDownloadBatch b ON b.CarrierDownloadBatchId = i.CarrierDownloadBatchId
WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND b.IsDeleted = 0
  AND (@BatchId IS NULL OR i.CarrierDownloadBatchId = @BatchId)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR i.CarrierPolicyNumber LIKE '%' + @SearchTerm + '%' OR i.NamedInsured LIKE '%' + @SearchTerm + '%' OR i.TransactionType LIKE '%' + @SearchTerm + '%' OR i.LineOfBusiness LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, BatchId = batchId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CarrierDownloadItemDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CarrierDownloadItemDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<CarrierDownloadItemDto?> GetCarrierDownloadItemByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {CarrierDownloadItemColumns} FROM Integration.CarrierDownloadItem i JOIN Integration.CarrierDownloadBatch b ON b.CarrierDownloadBatchId = i.CarrierDownloadBatchId WHERE i.CarrierDownloadItemId = @Id AND i.IsDeleted = 0 AND b.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CarrierDownloadItemDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateCarrierDownloadBatchAsync(CreateCarrierDownloadBatchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Integration.CarrierDownloadBatch
    (CarrierDownloadBatchId, TenantId, CarrierId, CarrierName, SourceType, FileName, RawStorageUri, Status, TotalRecords, ProcessedRecords, FailedRecords, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@Id, @TenantId, @CarrierId, @CarrierName, @SourceType, @FileName, @RawStorageUri, 'Received', 0, 0, 0, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CarrierId, request.CarrierName, request.SourceType, request.FileName, request.RawStorageUri, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> CreateCarrierDownloadItemAsync(CreateCarrierDownloadItemRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Integration.CarrierDownloadItem
    (CarrierDownloadItemId, TenantId, CarrierDownloadBatchId, TransactionType, CarrierPolicyNumber, NamedInsured, EffectiveDate, ExpirationDate, LineOfBusiness, Premium, Commission, RawPayload, NormalizedPayload, MatchStatus, ProcessingStatus, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT @Id, @TenantId, @CarrierDownloadBatchId, @TransactionType, @CarrierPolicyNumber, @NamedInsured, @EffectiveDate, @ExpirationDate, @LineOfBusiness, @Premium, @Commission, @RawPayload, @NormalizedPayload, 'Pending', 'Pending', SYSUTCDATETIME(), @CreatedByUserId, 0
WHERE EXISTS (SELECT 1 FROM Integration.CarrierDownloadBatch WHERE CarrierDownloadBatchId = @CarrierDownloadBatchId AND TenantId = @TenantId AND IsDeleted = 0);

UPDATE Integration.CarrierDownloadBatch
SET TotalRecords = TotalRecords + 1,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE CarrierDownloadBatchId = @CarrierDownloadBatchId AND TenantId = @TenantId AND IsDeleted = 0;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CarrierDownloadBatchId, request.TransactionType, request.CarrierPolicyNumber, request.NamedInsured, request.EffectiveDate, request.ExpirationDate, request.LineOfBusiness, request.Premium, request.Commission, request.RawPayload, request.NormalizedPayload, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateCarrierDownloadItemStatusAsync(Guid id, UpdateCarrierDownloadItemStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Integration.CarrierDownloadItem
SET MatchStatus = @MatchStatus,
    ProcessingStatus = @ProcessingStatus,
    ErrorMessage = @ErrorMessage,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CarrierDownloadItemId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.MatchStatus, request.ProcessingStatus, request.ErrorMessage, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task CompleteCarrierDownloadBatchAsync(Guid id, CompleteCarrierDownloadBatchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Integration.CarrierDownloadBatch
SET Status = @Status,
    TotalRecords = @TotalRecords,
    ProcessedRecords = @ProcessedRecords,
    FailedRecords = @FailedRecords,
    ErrorMessage = @ErrorMessage,
    CompletedDateUtc = CASE WHEN @Status IN ('Completed', 'CompletedWithErrors', 'Failed') THEN SYSUTCDATETIME() ELSE CompletedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CarrierDownloadBatchId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.Status, request.TotalRecords, request.ProcessedRecords, request.FailedRecords, request.ErrorMessage, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    // ── Download Exceptions ───────────────────────────────────────────

    private const string ExceptionColumns = "DownloadExceptionId, TenantId, DownloadLogId, CarrierDownloadItemId, CarrierId, CarrierName, CarrierPolicyNumber, NamedInsured, TransactionType, LineOfBusiness, EffectiveDate, Premium, ExceptionType, Severity, Message, RawPayload, ResolutionStatus, AssignedToUserId, ResolvedByUserId, OccurredUtc, ResolvedUtc, ResolutionNotes";

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
        const string sql = "SELECT DownloadExceptionId, TenantId, DownloadLogId, CarrierDownloadItemId, CarrierId, CarrierName, CarrierPolicyNumber, NamedInsured, TransactionType, LineOfBusiness, EffectiveDate, Premium, ExceptionType, Severity, Message, RawPayload, ResolutionStatus, AssignedToUserId, ResolvedByUserId, OccurredUtc, ResolvedUtc, ResolutionNotes FROM Integration.DownloadException WHERE DownloadExceptionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DownloadExceptionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateCarrierDownloadExceptionAsync(CreateCarrierDownloadExceptionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Integration.CarrierDownloadException
    (CarrierDownloadExceptionId, TenantId, CarrierDownloadItemId, ExceptionType, Severity, AssignedToUserId, Status, ResolutionNotes, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@Id, @TenantId, @CarrierDownloadItemId, @ExceptionType, @Severity, @AssignedToUserId, 'Open', @ResolutionNotes, SYSUTCDATETIME(), @CreatedByUserId, 0);

UPDATE Integration.CarrierDownloadItem
SET MatchStatus = 'Exception',
    ProcessingStatus = 'NeedsReview',
    ErrorMessage = COALESCE(@ResolutionNotes, @ExceptionType),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE CarrierDownloadItemId = @CarrierDownloadItemId AND IsDeleted = 0;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CarrierDownloadItemId, request.ExceptionType, request.Severity, request.AssignedToUserId, request.ResolutionNotes, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ManualMatchCarrierDownloadExceptionAsync(Guid id, ManualCarrierDownloadMatchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @ItemId UNIQUEIDENTIFIER;
SELECT @ItemId = CarrierDownloadItemId FROM Integration.CarrierDownloadException WHERE CarrierDownloadExceptionId = @Id AND IsDeleted = 0;

IF @ItemId IS NOT NULL
BEGIN
    INSERT INTO Integration.CarrierDownloadMatch
        (CarrierDownloadMatchId, TenantId, CarrierDownloadItemId, MatchedAccountId, MatchedPolicyId, MatchedContactId, MatchScore, MatchMethod, ReviewedByUserId, ReviewedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @ItemId, @MatchedAccountId, @MatchedPolicyId, @MatchedContactId, @MatchScore, @MatchMethod, @ReviewedByUserId, SYSUTCDATETIME(), SYSUTCDATETIME(), @ReviewedByUserId, 0);

    UPDATE Integration.CarrierDownloadItem
    SET MatchStatus = CASE WHEN @MatchScore >= 90 THEN 'AutoMatched' ELSE 'Matched' END,
        ProcessingStatus = 'Staged',
        ErrorMessage = NULL,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ReviewedByUserId
    WHERE CarrierDownloadItemId = @ItemId AND IsDeleted = 0;

    UPDATE Integration.CarrierDownloadException
    SET Status = 'Resolved',
        ResolutionNotes = @ResolutionNote,
        ResolvedByUserId = @ReviewedByUserId,
        ResolvedDateUtc = SYSUTCDATETIME(),
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ReviewedByUserId
    WHERE CarrierDownloadExceptionId = @Id AND IsDeleted = 0;
END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.MatchedAccountId, request.MatchedPolicyId, request.MatchedContactId, request.MatchScore, request.MatchMethod, request.ReviewedByUserId, request.ResolutionNote }, cancellationToken: cancellationToken));
    }

    public async Task ResolveDownloadExceptionAsync(Guid id, ResolveDownloadExceptionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Integration.CarrierDownloadException
SET    Status            = 'Resolved',
       ResolutionNotes   = @ResolutionNote,
       ResolvedByUserId  = @ResolvedByUserId,
       ResolvedDateUtc   = SYSUTCDATETIME(),
       ModifiedDateUtc   = SYSUTCDATETIME(),
       ModifiedByUserId  = @ResolvedByUserId
WHERE  CarrierDownloadExceptionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.ResolvedByUserId, request.ResolutionNote }, cancellationToken: cancellationToken));
    }

    public async Task RetryDownloadExceptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Integration.CarrierDownloadException
SET    Status = 'Retrying',
       ModifiedDateUtc = SYSUTCDATETIME()
WHERE  CarrierDownloadExceptionId = @Id AND IsDeleted = 0;";
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
