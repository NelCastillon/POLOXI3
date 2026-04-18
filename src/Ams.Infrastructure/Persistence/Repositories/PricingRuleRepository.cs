using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PricingRules;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PricingRuleRepository : IPricingRuleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PricingRuleRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreatePricingRuleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO CRM.PricingRule
(
    PricingRuleId, TenantId, RuleCode, RuleName, RuleTypeCode,
    ServiceCode, SegmentCode, MinQuantity, MaxQuantity,
    DiscountPercent, AdjustedUnitPrice, EffectiveStartDate, EffectiveEndDate,
    RequiresApproval, Priority, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @PricingRuleId, @TenantId, @RuleCode, @RuleName, @RuleTypeCode,
    @ServiceCode, @SegmentCode, @MinQuantity, @MaxQuantity,
    @DiscountPercent, @AdjustedUnitPrice, @EffectiveStartDate, @EffectiveEndDate,
    @RequiresApproval, @Priority, 1, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PricingRuleId = id,
            request.TenantId,
            request.RuleCode,
            request.RuleName,
            request.RuleTypeCode,
            request.ServiceCode,
            request.SegmentCode,
            request.MinQuantity,
            request.MaxQuantity,
            request.DiscountPercent,
            request.AdjustedUnitPrice,
            request.EffectiveStartDate,
            request.EffectiveEndDate,
            request.RequiresApproval,
            request.Priority,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<PricingRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT PricingRuleId, TenantId, RuleCode, RuleName, RuleTypeCode,
       ServiceCode, SegmentCode, MinQuantity, MaxQuantity,
       DiscountPercent, AdjustedUnitPrice, EffectiveStartDate, EffectiveEndDate,
       RequiresApproval, Priority, IsActive, CreatedDateUtc
FROM CRM.PricingRule
WHERE PricingRuleId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PricingRuleDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PricingRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "CRM.PricingRule",
            "PricingRuleId, TenantId, RuleCode, RuleName, RuleTypeCode, ServiceCode, SegmentCode, MinQuantity, MaxQuantity, DiscountPercent, AdjustedUnitPrice, EffectiveStartDate, EffectiveEndDate, RequiresApproval, Priority, IsActive, CreatedDateUtc",
            "RuleName LIKE '%' + @SearchTerm + '%' OR RuleCode LIKE '%' + @SearchTerm + '%' OR RuleTypeCode LIKE '%' + @SearchTerm + '%'",
            "Priority ASC, CreatedDateUtc DESC",
            true);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<PricingRuleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<PricingRuleDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
