using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Appetite;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AppetiteRuleRepository : IAppetiteRuleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public AppetiteRuleRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = "AppetiteRuleId, TenantId, RuleName, LobCode, CarrierNaic, RuleJson, AppetiteLevel, Priority, IsActive, CreatedDateUtc";

    public async Task<AppetiteRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {SelectColumns} FROM Agency.AppetiteRule WHERE AppetiteRuleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AppetiteRuleDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AppetiteRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Agency.AppetiteRule",
            SelectColumns,
            "RuleName LIKE '%' + @SearchTerm + '%' OR LobCode LIKE '%' + @SearchTerm + '%'",
            "Priority ASC, RuleName ASC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AppetiteRuleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AppetiteRuleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateAppetiteRuleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Agency.AppetiteRule
    (AppetiteRuleId, TenantId, RuleName, LobCode, CarrierNaic, RuleJson, AppetiteLevel, Priority, IsActive, CreatedDateUtc, IsDeleted)
VALUES
    (@AppetiteRuleId, @TenantId, @RuleName, @LobCode, @CarrierNaic, @RuleJson, @AppetiteLevel, @Priority, 1, GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AppetiteRuleId = id,
            request.TenantId,
            request.RuleName,
            request.LobCode,
            request.CarrierNaic,
            request.RuleJson,
            request.AppetiteLevel,
            request.Priority,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateAppetiteRuleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Agency.AppetiteRule
SET    RuleName        = @RuleName,
       LobCode         = @LobCode,
       CarrierNaic     = @CarrierNaic,
       RuleJson        = @RuleJson,
       AppetiteLevel   = @AppetiteLevel,
       Priority        = @Priority,
       IsActive        = @IsActive,
       ModifiedDateUtc = GETUTCDATE()
WHERE  AppetiteRuleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.RuleName,
            request.LobCode,
            request.CarrierNaic,
            request.RuleJson,
            request.AppetiteLevel,
            request.Priority,
            request.IsActive,
        }, cancellationToken: cancellationToken));
    }
}
