using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DeferredRevenueScheduleRepository : IDeferredRevenueScheduleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.DeferredRevenueSchedule",
        "DeferredRevenueScheduleId, TenantId, AccountId, InvoiceId, AgreementId, TotalAmount, RecognizedAmount, RemainingAmount, StartDate, EndDate, FrequencyCode, StatusCode, GLAccountId, DeferredGLAccountId, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted",
        "FrequencyCode LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%'",
        "StartDate DESC");

    public DeferredRevenueScheduleRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<DeferredRevenueScheduleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DeferredRevenueScheduleId, TenantId, AccountId, InvoiceId, AgreementId, TotalAmount, RecognizedAmount, RemainingAmount, StartDate, EndDate, FrequencyCode, StatusCode, GLAccountId, DeferredGLAccountId, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted FROM Finance.DeferredRevenueSchedule WHERE DeferredRevenueScheduleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DeferredRevenueScheduleDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DeferredRevenueScheduleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DeferredRevenueScheduleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DeferredRevenueScheduleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
