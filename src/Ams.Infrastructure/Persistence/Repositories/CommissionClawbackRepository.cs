using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionClawbackRepository : ICommissionClawbackRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionClawbackRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionClawbackDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT ClawbackId, TenantId, PayeeId, OriginalTransactionId, ClawbackDate, Amount, ReasonCode, Notes, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc FROM Commission.CommissionClawback WHERE ClawbackId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionClawbackDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionClawbackDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Commission.CommissionClawback",
            "ClawbackId, TenantId, PayeeId, OriginalTransactionId, ClawbackDate, Amount, ReasonCode, Notes, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc",
            "ReasonCode LIKE '%' + @SearchTerm + '%'",
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

        var items = (await multi.ReadAsync<CommissionClawbackDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionClawbackDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
