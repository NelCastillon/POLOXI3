using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.IntegrationConfig;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class IntegrationConfigRepository : IIntegrationConfigRepository
{
    private readonly ISqlConnectionFactory _cf;
    public IntegrationConfigRepository(ISqlConnectionFactory cf) => _cf = cf;

    private const string Cols = "IntegrationConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc";

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Integration') EXEC(N'CREATE SCHEMA Integration');

IF OBJECT_ID(N'Integration.IntegrationConfigItem', N'U') IS NULL
BEGIN
    CREATE TABLE Integration.IntegrationConfigItem
    (
        IntegrationConfigItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_IntegrationConfigItem PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Kind NVARCHAR(80) NOT NULL,
        Code NVARCHAR(80) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Category NVARCHAR(120) NULL,
        Description NVARCHAR(500) NULL,
        ConfigurationJson NVARCHAR(4000) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_IntegrationConfigItem_IsActive DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_IntegrationConfigItem_SortOrder DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_IntegrationConfigItem_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_IntegrationConfigItem_IsDeleted DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'Category') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD Category NVARCHAR(120) NULL;
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'Description') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD Description NVARCHAR(500) NULL;
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'ConfigurationJson') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD ConfigurationJson NVARCHAR(4000) NULL;
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'IsActive') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD IsActive BIT NOT NULL CONSTRAINT DF_IntegrationConfigItem_IsActive_Legacy DEFAULT 1;
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'SortOrder') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD SortOrder INT NOT NULL CONSTRAINT DF_IntegrationConfigItem_SortOrder_Legacy DEFAULT 0;
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'CreatedDateUtc') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_IntegrationConfigItem_CreatedDateUtc_Legacy DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'CreatedByUserId') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'ModifiedDateUtc') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'ModifiedByUserId') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Integration.IntegrationConfigItem', N'IsDeleted') IS NULL ALTER TABLE Integration.IntegrationConfigItem ADD IsDeleted BIT NOT NULL CONSTRAINT DF_IntegrationConfigItem_IsDeleted_Legacy DEFAULT 0;
END;";

        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: ct));

        if (tenantId is null) return;

        const string seedSql = @"
DECLARE @Defaults TABLE (Kind NVARCHAR(80), Code NVARCHAR(80), Name NVARCHAR(200), Category NVARCHAR(120), Description NVARCHAR(500), ConfigurationJson NVARCHAR(4000), SortOrder INT);

INSERT INTO @Defaults (Kind, Code, Name, Category, Description, ConfigurationJson, SortOrder)
VALUES
(N'PaymentGateway', N'STRIPE', N'Stripe Payments', N'Stripe', N'Stripe card and ACH gateway for tenant payment collection, settlement, webhook, and retry workflows.', N'{""providerType"":""Stripe"",""environment"":""Sandbox"",""status"":""Testing"",""settlementMode"":""Daily Batch"",""webhookStatus"":""Not Configured"",""webhookUrl"":"""",""supportsCard"":true,""supportsAch"":true,""autoRetry"":true,""openExceptions"":0,""successRate"":99.1}', 10),
(N'PaymentGateway', N'AUTHNET', N'Authorize.Net', N'Authorize.Net', N'Authorize.Net gateway configuration for card payments, ACH support, merchant settlement, and webhook operations.', N'{""providerType"":""Authorize.Net"",""environment"":""Sandbox"",""status"":""Testing"",""settlementMode"":""Daily Batch"",""webhookStatus"":""Not Configured"",""webhookUrl"":"""",""supportsCard"":true,""supportsAch"":true,""autoRetry"":true,""openExceptions"":0,""successRate"":98.7}', 20),
(N'PaymentGateway', N'ACH', N'Bank ACH Processor', N'ACH Processor', N'Bank ACH processor used for agency bill drafts, payment plan withdrawals, settlement batches, and retry controls.', N'{""providerType"":""ACH Processor"",""environment"":""Production"",""status"":""Connected"",""settlementMode"":""Daily Batch"",""webhookStatus"":""Healthy"",""webhookUrl"":"""",""supportsCard"":false,""supportsAch"":true,""autoRetry"":true,""openExceptions"":0,""successRate"":99.4}', 30);

INSERT INTO Integration.IntegrationConfigItem (IntegrationConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, d.Kind, d.Code, d.Name, d.Category, d.Description, d.ConfigurationJson, 1, d.SortOrder, SYSUTCDATETIME(), NULL, 0
FROM @Defaults d
WHERE NOT EXISTS
(
    SELECT 1
    FROM Integration.IntegrationConfigItem existing
    WHERE existing.TenantId = @TenantId
      AND existing.Kind = d.Kind
      AND existing.Code = d.Code
);
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: ct));
    }

    public async Task<IntegrationConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(ct: ct);
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<IntegrationConfigItemDto>(new CommandDefinition($"SELECT {Cols} FROM Integration.IntegrationConfigItem WHERE IntegrationConfigItemId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<IntegrationConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, ct);
        const string sql = @"
SELECT IntegrationConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc
FROM Integration.IntegrationConfigItem
WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0
  AND (@SearchTerm='' OR Name LIKE '%'+@SearchTerm+'%' OR Code LIKE '%'+@SearchTerm+'%' OR Category LIKE '%'+@SearchTerm+'%')
ORDER BY SortOrder ASC, Name ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Integration.IntegrationConfigItem
WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0
  AND (@SearchTerm='' OR Name LIKE '%'+@SearchTerm+'%' OR Code LIKE '%'+@SearchTerm+'%' OR Category LIKE '%'+@SearchTerm+'%');";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm ?? string.Empty, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        return new() { Items = (await multi.ReadAsync<IntegrationConfigItemDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateIntegrationConfigItemRequest r, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(r.TenantId, ct);
        const string sql = @"INSERT INTO Integration.IntegrationConfigItem (IntegrationConfigItemId,TenantId,Kind,Code,Name,Category,Description,ConfigurationJson,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@Kind,@Code,@Name,@Category,@Description,@ConfigurationJson,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.Kind, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateIntegrationConfigItemRequest r, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(ct: ct);
        const string sql = @"UPDATE Integration.IntegrationConfigItem SET Code=@Code,Name=@Name,Category=@Category,Description=@Description,ConfigurationJson=@ConfigurationJson,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE IntegrationConfigItemId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(ct: ct);
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Integration.IntegrationConfigItem SET IsDeleted=1 WHERE IntegrationConfigItemId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}
