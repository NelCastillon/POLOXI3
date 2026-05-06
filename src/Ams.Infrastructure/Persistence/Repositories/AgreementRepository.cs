using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AgreementRepository : IAgreementRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AgreementRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AgreementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT AgreementId, TenantId, AgreementNumber, AccountId, OpportunityId, AgreementStatusCodeId AS StatusCode, CreatedDateUtc FROM Sales.Agreement WHERE AgreementId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AgreementDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AgreementDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Sales.Agreement",
            "AgreementId, TenantId, AgreementNumber, AccountId, OpportunityId, AgreementStatusCodeId AS StatusCode, CreatedDateUtc",
            "AgreementNumber LIKE '%' + @SearchTerm + '%'",
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

        var items = (await multi.ReadAsync<AgreementDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<AgreementDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateAgreementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Sales.Agreement (AgreementId, TenantId, AgreementNumber, AccountId, OpportunityId, AgreementStatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@AgreementId, @TenantId, @AgreementNumber, @AccountId, NULL, 1, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { AgreementId = id, request.TenantId, request.AgreementNumber, request.AccountId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }
}
