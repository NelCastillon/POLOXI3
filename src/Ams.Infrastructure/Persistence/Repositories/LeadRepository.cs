using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Leads;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class LeadRepository : ILeadRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public LeadRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateLeadRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO CRM.Lead
(
    LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone,
    InterestedService, StatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @LeadId, @TenantId, @LeadNumber, @AccountName, @FirstName, @LastName, @Email, @Phone,
    @InterestedService, 1, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            LeadId = id,
            request.TenantId,
            request.LeadNumber,
            request.AccountName,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.InterestedService,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode, QualifiedDate, StatusCodeId AS StatusCode, AssignedToUserId, CreatedDateUtc FROM CRM.Lead WHERE LeadId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<LeadDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<LeadDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "CRM.Lead",
            "LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode, QualifiedDate, StatusCodeId AS StatusCode, AssignedToUserId, CreatedDateUtc",
            "FirstName LIKE '%' + @SearchTerm + '%' OR LastName LIKE '%' + @SearchTerm + '%' OR Email LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%' OR LeadNumber LIKE '%' + @SearchTerm + '%'",
            "CreatedDateUtc DESC",
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

        var items = (await multi.ReadAsync<LeadDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<LeadDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<LeadScoringRuleDto>> GetScoringRulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    LeadScoringRuleId,
    TenantId,
    RuleName,
    RuleDescription,
    PointValue,
    IsActive,
    CreatedDateUtc
FROM CRM.LeadScoringRule
WHERE TenantId = @TenantId AND IsActive = 1
ORDER BY PointValue DESC, RuleName";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rules = await cn.QueryAsync<LeadScoringRuleDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rules.ToList();
    }
}
