using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountingPeriodRepository : IAccountingPeriodRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountingPeriodRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AccountingPeriodDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    AccountingPeriodId, TenantId, PeriodCode, PeriodName, StartDate, EndDate, 
    StatusCode, CreatedDateUtc 
FROM Finance.AccountingPeriod 
WHERE AccountingPeriodId = @Id AND IsDeleted = 0";
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccountingPeriodDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccountingPeriodDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var selectColumns = "AccountingPeriodId, TenantId, PeriodCode, PeriodName, StartDate, EndDate, StatusCode, CreatedDateUtc";
        var searchPredicate = "PeriodName LIKE '%' + @SearchTerm + '%' OR PeriodCode LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.AccountingPeriod", selectColumns, searchPredicate, "StartDate DESC");
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AccountingPeriodDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AccountingPeriodDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateAccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.AccountingPeriod (AccountingPeriodId, TenantId, PeriodCode, PeriodName, StartDate, EndDate, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @PeriodCode, @PeriodName, @StartDate, @EndDate, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.PeriodCode, request.PeriodName, request.StartDate, request.EndDate, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateAccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Finance.AccountingPeriod
SET PeriodCode = @PeriodCode,
    PeriodName = @PeriodName,
    StartDate = @StartDate,
    EndDate = @EndDate,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE AccountingPeriodId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.PeriodCode, request.PeriodName, request.StartDate, request.EndDate, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}
