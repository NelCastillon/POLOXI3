using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PricingMarketRulesRepository : IPricingMarketRulesRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PricingMarketRulesRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<PriceClassDto>> SearchPriceClassesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT PriceClassId, TenantId, ClassCode, ClassName, LobCode, RiskTierCode, Description, BaseRate, MinPremium, MaxPremium, Priority, IsActive, CreatedDateUtc
                FROM CRM.PriceClass
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@SearchTerm IS NULL OR ClassCode LIKE '%' + @SearchTerm + '%' OR ClassName LIKE '%' + @SearchTerm + '%' OR LobCode LIKE '%' + @SearchTerm + '%' OR RiskTierCode LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY Priority, ClassName OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM CRM.PriceClass WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@SearchTerm IS NULL OR ClassCode LIKE '%' + @SearchTerm + '%' OR ClassName LIKE '%' + @SearchTerm + '%' OR LobCode LIKE '%' + @SearchTerm + '%' OR RiskTierCode LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, Args(tenantId, searchTerm, pageNumber, pageSize), cancellationToken: cancellationToken));
        return new PagedResult<PriceClassDto> { Items = (await multi.ReadAsync<PriceClassDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreatePriceClassAsync(UpsertPriceClassRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO CRM.PriceClass (PriceClassId, TenantId, ClassCode, ClassName, LobCode, RiskTierCode, Description, BaseRate, MinPremium, MaxPremium, Priority, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES (@Id, @TenantId, @ClassCode, @ClassName, @LobCode, @RiskTierCode, @Description, @BaseRate, @MinPremium, @MaxPremium, @Priority, @IsActive, SYSUTCDATETIME(), @UserId, 0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.ClassCode, request.ClassName, request.LobCode, request.RiskTierCode, request.Description, request.BaseRate, request.MinPremium, request.MaxPremium, request.Priority, request.IsActive, request.UserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdatePriceClassAsync(Guid id, UpsertPriceClassRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE CRM.PriceClass SET ClassCode=@ClassCode, ClassName=@ClassName, LobCode=@LobCode, RiskTierCode=@RiskTierCode, Description=@Description, BaseRate=@BaseRate, MinPremium=@MinPremium, MaxPremium=@MaxPremium, Priority=@Priority, IsActive=@IsActive, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId
            WHERE PriceClassId=@Id AND IsDeleted=0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.ClassCode, request.ClassName, request.LobCode, request.RiskTierCode, request.Description, request.BaseRate, request.MinPremium, request.MaxPremium, request.Priority, request.IsActive, request.UserId }, cancellationToken: cancellationToken));
    }

    public async Task DeletePriceClassAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.PriceClass SET IsDeleted=1, IsActive=0, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId WHERE PriceClassId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<MarketAppetiteDto>> SearchMarketAppetiteAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT MarketAppetiteId, TenantId, CarrierName, CarrierNaic, LobCode, AppetiteLevelCode, MinPremium, MaxPremium, StateCode, Notes, Priority, IsActive, CreatedDateUtc
                FROM CRM.MarketAppetite
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@SearchTerm IS NULL OR CarrierName LIKE '%' + @SearchTerm + '%' OR CarrierNaic LIKE '%' + @SearchTerm + '%' OR LobCode LIKE '%' + @SearchTerm + '%' OR AppetiteLevelCode LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY Priority, CarrierName OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM CRM.MarketAppetite WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@SearchTerm IS NULL OR CarrierName LIKE '%' + @SearchTerm + '%' OR CarrierNaic LIKE '%' + @SearchTerm + '%' OR LobCode LIKE '%' + @SearchTerm + '%' OR AppetiteLevelCode LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, Args(tenantId, searchTerm, pageNumber, pageSize), cancellationToken: cancellationToken));
        return new PagedResult<MarketAppetiteDto> { Items = (await multi.ReadAsync<MarketAppetiteDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateMarketAppetiteAsync(UpsertMarketAppetiteRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO CRM.MarketAppetite (MarketAppetiteId, TenantId, CarrierName, CarrierNaic, LobCode, AppetiteLevelCode, MinPremium, MaxPremium, StateCode, Notes, Priority, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES (@Id, @TenantId, @CarrierName, @CarrierNaic, @LobCode, @AppetiteLevelCode, @MinPremium, @MaxPremium, @StateCode, @Notes, @Priority, @IsActive, SYSUTCDATETIME(), @UserId, 0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CarrierName, request.CarrierNaic, request.LobCode, request.AppetiteLevelCode, request.MinPremium, request.MaxPremium, request.StateCode, request.Notes, request.Priority, request.IsActive, request.UserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateMarketAppetiteAsync(Guid id, UpsertMarketAppetiteRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE CRM.MarketAppetite SET CarrierName=@CarrierName, CarrierNaic=@CarrierNaic, LobCode=@LobCode, AppetiteLevelCode=@AppetiteLevelCode, MinPremium=@MinPremium, MaxPremium=@MaxPremium, StateCode=@StateCode, Notes=@Notes, Priority=@Priority, IsActive=@IsActive, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId
            WHERE MarketAppetiteId=@Id AND IsDeleted=0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.CarrierName, request.CarrierNaic, request.LobCode, request.AppetiteLevelCode, request.MinPremium, request.MaxPremium, request.StateCode, request.Notes, request.Priority, request.IsActive, request.UserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteMarketAppetiteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.MarketAppetite SET IsDeleted=1, IsActive=0, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId WHERE MarketAppetiteId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CarrierMappingDto>> SearchCarrierMappingsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT CarrierMappingId, TenantId, CarrierName, CarrierNaic, InternalCode, ExternalCode, LobCode, DownloadFormatCode, IntegrationKey, Notes, IsActive, LastTestedDateUtc, LastTestStatusCode, CreatedDateUtc
                FROM CRM.CarrierMapping
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@SearchTerm IS NULL OR CarrierName LIKE '%' + @SearchTerm + '%' OR CarrierNaic LIKE '%' + @SearchTerm + '%' OR InternalCode LIKE '%' + @SearchTerm + '%' OR ExternalCode LIKE '%' + @SearchTerm + '%' OR LobCode LIKE '%' + @SearchTerm + '%' OR DownloadFormatCode LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY CarrierName OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM CRM.CarrierMapping WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@SearchTerm IS NULL OR CarrierName LIKE '%' + @SearchTerm + '%' OR CarrierNaic LIKE '%' + @SearchTerm + '%' OR InternalCode LIKE '%' + @SearchTerm + '%' OR ExternalCode LIKE '%' + @SearchTerm + '%' OR LobCode LIKE '%' + @SearchTerm + '%' OR DownloadFormatCode LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, Args(tenantId, searchTerm, pageNumber, pageSize), cancellationToken: cancellationToken));
        return new PagedResult<CarrierMappingDto> { Items = (await multi.ReadAsync<CarrierMappingDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateCarrierMappingAsync(UpsertCarrierMappingRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO CRM.CarrierMapping (CarrierMappingId, TenantId, CarrierName, CarrierNaic, InternalCode, ExternalCode, LobCode, DownloadFormatCode, IntegrationKey, Notes, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES (@Id, @TenantId, @CarrierName, @CarrierNaic, @InternalCode, @ExternalCode, @LobCode, @DownloadFormatCode, @IntegrationKey, @Notes, @IsActive, SYSUTCDATETIME(), @UserId, 0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CarrierName, request.CarrierNaic, request.InternalCode, request.ExternalCode, request.LobCode, request.DownloadFormatCode, request.IntegrationKey, request.Notes, request.IsActive, request.UserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateCarrierMappingAsync(Guid id, UpsertCarrierMappingRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE CRM.CarrierMapping SET CarrierName=@CarrierName, CarrierNaic=@CarrierNaic, InternalCode=@InternalCode, ExternalCode=@ExternalCode, LobCode=@LobCode, DownloadFormatCode=@DownloadFormatCode, IntegrationKey=@IntegrationKey, Notes=@Notes, IsActive=@IsActive, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId
            WHERE CarrierMappingId=@Id AND IsDeleted=0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.CarrierName, request.CarrierNaic, request.InternalCode, request.ExternalCode, request.LobCode, request.DownloadFormatCode, request.IntegrationKey, request.Notes, request.IsActive, request.UserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteCarrierMappingAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.CarrierMapping SET IsDeleted=1, IsActive=0, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId WHERE CarrierMappingId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task TestCarrierMappingAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.CarrierMapping SET LastTestedDateUtc=SYSUTCDATETIME(), LastTestStatusCode=N'Passed', ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId WHERE CarrierMappingId=@Id AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken));
    }

    private static object Args(Guid tenantId, string? searchTerm, int pageNumber, int pageSize) => new
    {
        TenantId = tenantId,
        SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm,
        Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
        PageSize = Math.Max(pageSize, 1)
    };
}
