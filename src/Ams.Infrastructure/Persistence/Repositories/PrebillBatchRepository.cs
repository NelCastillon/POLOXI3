using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PrebillBatchRepository : IPrebillBatchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public PrebillBatchRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PrebillBatchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT PrebillBatchId, TenantId, BatchNumber, AccountId, BillingPeriodStart, BillingPeriodEnd, TotalAmount, StatusCode, ReviewedByUserId, ReviewedDateUtc, ApprovedByUserId, ApprovedDateUtc, Notes, CreatedDateUtc FROM Billing.PrebillBatch WHERE PrebillBatchId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PrebillBatchDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PrebillBatchDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.PrebillBatch", "PrebillBatchId, TenantId, BatchNumber, AccountId, BillingPeriodStart, BillingPeriodEnd, TotalAmount, StatusCode, ReviewedByUserId, ReviewedDateUtc, ApprovedByUserId, ApprovedDateUtc, Notes, CreatedDateUtc", "BatchNumber LIKE '%' + @SearchTerm + '%'", "BillingPeriodStart DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<PrebillBatchDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PrebillBatchDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
