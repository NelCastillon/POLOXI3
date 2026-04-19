using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Tenants;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public TenantRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = @"
        TenantId, TenantCode, TenantName, StatusCode, PlanCode,
        RegionCode, IsolationMode, PrimaryDomain, ActiveUsers,
        IsActive, Locale, CurrencyCode,
        TimeZoneId, CreatedDateUtc, GoLiveDateUtc";

    public async Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM Core.Tenant WHERE TenantId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TenantDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TenantDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = $@"
;WITH Cte AS (
    SELECT {SelectColumns}
    FROM Core.Tenant
    WHERE IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR TenantName LIKE '%' + @SearchTerm + '%'
           OR TenantCode LIKE '%' + @SearchTerm + '%'))
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Core.Tenant
WHERE IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR TenantName LIKE '%' + @SearchTerm + '%'
       OR TenantCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TenantDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TenantDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Core.Tenant
    (TenantId, TenantCode, TenantName, StatusCode, PlanCode,
     RegionCode, IsolationMode, PrimaryDomain,
     Locale, CurrencyCode, TimeZoneId, IsActive,
     CreatedDateUtc, GoLiveDateUtc, IsDeleted)
VALUES
    (@TenantId, @TenantCode, @TenantName, 'Active', @PlanCode,
     @RegionCode, @IsolationMode, @PrimaryDomain,
     @Locale, @CurrencyCode, @TimeZoneId, 1,
     GETUTCDATE(), @GoLiveDateUtc, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId      = id,
            request.TenantCode,
            request.TenantName,
            request.PlanCode,
            request.RegionCode,
            request.IsolationMode,
            request.PrimaryDomain,
            request.Locale,
            request.CurrencyCode,
            request.TimeZoneId,
            request.GoLiveDateUtc,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Tenant SET
    TenantName    = @TenantName,
    PlanCode      = @PlanCode,
    RegionCode    = @RegionCode,
    IsolationMode = @IsolationMode,
    PrimaryDomain = @PrimaryDomain,
    Locale        = @Locale,
    CurrencyCode  = @CurrencyCode,
    TimeZoneId    = @TimeZoneId,
    GoLiveDateUtc = @GoLiveDateUtc
WHERE TenantId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id            = id,
            request.TenantName,
            request.PlanCode,
            request.RegionCode,
            request.IsolationMode,
            request.PrimaryDomain,
            request.Locale,
            request.CurrencyCode,
            request.TimeZoneId,
            request.GoLiveDateUtc,
        }, cancellationToken: cancellationToken));
    }

    public async Task SetStatusAsync(Guid id, string statusCode, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Tenant SET
    StatusCode = @StatusCode,
    IsActive   = CASE WHEN @StatusCode = 'Active' THEN 1 ELSE 0 END
WHERE TenantId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, StatusCode = statusCode }, cancellationToken: cancellationToken));
    }
}
