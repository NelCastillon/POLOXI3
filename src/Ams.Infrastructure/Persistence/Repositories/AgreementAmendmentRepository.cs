using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AgreementAmendmentRepository : IAgreementAmendmentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public AgreementAmendmentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AgreementAmendmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT AmendmentId, TenantId, AgreementId, AmendmentNumber, AmendmentTypeCode, EffectiveDate, Description, StatusCode, CreatedDateUtc FROM OPS.AgreementAmendment WHERE AmendmentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AgreementAmendmentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AgreementAmendmentDto>> SearchAsync(Guid tenantId, Guid? agreementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (SELECT AmendmentId, TenantId, AgreementId, AmendmentNumber, AmendmentTypeCode, EffectiveDate, Description, StatusCode, CreatedDateUtc FROM OPS.AgreementAmendment WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AgreementId IS NULL OR AgreementId = @AgreementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR AmendmentNumber LIKE '%' + @SearchTerm + '%' OR AmendmentTypeCode LIKE '%' + @SearchTerm + '%'))
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM OPS.AgreementAmendment WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AgreementId IS NULL OR AgreementId = @AgreementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR AmendmentNumber LIKE '%' + @SearchTerm + '%' OR AmendmentTypeCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, AgreementId = agreementId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AgreementAmendmentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AgreementAmendmentDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateAgreementAmendmentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.AgreementAmendment (AmendmentId, TenantId, AgreementId, AmendmentNumber, AmendmentTypeCode, EffectiveDate, Description, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@AmendmentId, @TenantId, @AgreementId, @AmendmentNumber, @AmendmentTypeCode, @EffectiveDate, @Description, 'Draft', SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { AmendmentId = id, request.TenantId, request.AgreementId, request.AmendmentNumber, request.AmendmentTypeCode, request.EffectiveDate, request.Description, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }
}
