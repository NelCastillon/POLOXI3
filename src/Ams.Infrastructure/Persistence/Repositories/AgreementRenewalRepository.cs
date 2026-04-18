using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AgreementRenewalRepository : IAgreementRenewalRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public AgreementRenewalRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AgreementRenewalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT RenewalId, TenantId, AgreementId, RenewalNumber, NewStartDate, NewEndDate, TotalContractValue, StatusCode, ProcessedByUserId, ProcessedDateUtc, CreatedDateUtc FROM OPS.AgreementRenewal WHERE RenewalId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AgreementRenewalDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AgreementRenewalDto>> SearchAsync(Guid tenantId, Guid? agreementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (SELECT RenewalId, TenantId, AgreementId, RenewalNumber, NewStartDate, NewEndDate, TotalContractValue, StatusCode, ProcessedByUserId, ProcessedDateUtc, CreatedDateUtc FROM OPS.AgreementRenewal WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AgreementId IS NULL OR AgreementId = @AgreementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR RenewalNumber LIKE '%' + @SearchTerm + '%'))
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM OPS.AgreementRenewal WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AgreementId IS NULL OR AgreementId = @AgreementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR RenewalNumber LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, AgreementId = agreementId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AgreementRenewalDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AgreementRenewalDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateAgreementRenewalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.AgreementRenewal (RenewalId, TenantId, AgreementId, RenewalNumber, NewStartDate, NewEndDate, TotalContractValue, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@RenewalId, @TenantId, @AgreementId, @RenewalNumber, @NewStartDate, @NewEndDate, @TotalContractValue, 'Pending', SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { RenewalId = id, request.TenantId, request.AgreementId, request.RenewalNumber, request.NewStartDate, request.NewEndDate, request.TotalContractValue, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }
}
