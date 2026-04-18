using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BillingAdjustmentRepository : IBillingAdjustmentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public BillingAdjustmentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<BillingAdjustmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT AdjustmentId, TenantId, InvoiceId, AccountId, AdjustmentTypeCode, AdjustmentDate, Amount, Reason, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc FROM Finance.BillingAdjustment WHERE AdjustmentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<BillingAdjustmentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BillingAdjustmentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Finance.BillingAdjustment", "AdjustmentId, TenantId, InvoiceId, AccountId, AdjustmentTypeCode, AdjustmentDate, Amount, Reason, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc", "Reason LIKE '%' + @SearchTerm + '%' OR AdjustmentTypeCode LIKE '%' + @SearchTerm + '%'", "AdjustmentDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<BillingAdjustmentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BillingAdjustmentDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
