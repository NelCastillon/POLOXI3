using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
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
        if (!await TableExistsAsync(cancellationToken)) return null;

        const string sql = "SELECT DeferredRevenueScheduleId, TenantId, AccountId, InvoiceId, AgreementId, TotalAmount, RecognizedAmount, RemainingAmount, StartDate, EndDate, FrequencyCode, StatusCode, GLAccountId, DeferredGLAccountId, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted FROM Finance.DeferredRevenueSchedule WHERE DeferredRevenueScheduleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DeferredRevenueScheduleDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DeferredRevenueScheduleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            return new PagedResult<DeferredRevenueScheduleDto> { Items = [], TotalCount = 0, PageNumber = pageNumber, PageSize = pageSize };

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DeferredRevenueScheduleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DeferredRevenueScheduleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateDeferredRevenueScheduleRequest request, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            throw new InvalidOperationException("Finance.DeferredRevenueSchedule does not exist in the current database schema. Deferred Revenue is unavailable until the database schema includes this table.");

        var id = Guid.NewGuid();
        var remainingAmount = request.TotalAmount - request.RecognizedAmount;
        const string sql = @"
INSERT INTO Finance.DeferredRevenueSchedule (DeferredRevenueScheduleId, TenantId, AccountId, InvoiceId, AgreementId, TotalAmount, RecognizedAmount, RemainingAmount, StartDate, EndDate, FrequencyCode, StatusCode, GLAccountId, DeferredGLAccountId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountId, @InvoiceId, @AgreementId, @TotalAmount, @RecognizedAmount, @RemainingAmount, @StartDate, @EndDate, @FrequencyCode, @StatusCode, @GLAccountId, @DeferredGLAccountId, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountId, request.InvoiceId, request.AgreementId, request.TotalAmount, request.RecognizedAmount, RemainingAmount = remainingAmount, request.StartDate, request.EndDate, request.FrequencyCode, request.StatusCode, request.GLAccountId, request.DeferredGLAccountId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateDeferredRevenueScheduleRequest request, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            throw new InvalidOperationException("Finance.DeferredRevenueSchedule does not exist in the current database schema. Deferred Revenue is unavailable until the database schema includes this table.");

        var remainingAmount = request.TotalAmount - request.RecognizedAmount;
        const string sql = @"
UPDATE Finance.DeferredRevenueSchedule
SET AccountId = @AccountId,
    InvoiceId = @InvoiceId,
    AgreementId = @AgreementId,
    TotalAmount = @TotalAmount,
    RecognizedAmount = @RecognizedAmount,
    RemainingAmount = @RemainingAmount,
    StartDate = @StartDate,
    EndDate = @EndDate,
    FrequencyCode = @FrequencyCode,
    StatusCode = @StatusCode,
    GLAccountId = @GLAccountId,
    DeferredGLAccountId = @DeferredGLAccountId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE DeferredRevenueScheduleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountId, request.InvoiceId, request.AgreementId, request.TotalAmount, request.RecognizedAmount, RemainingAmount = remainingAmount, request.StartDate, request.EndDate, request.FrequencyCode, request.StatusCode, request.GLAccountId, request.DeferredGLAccountId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    private async Task<bool> TableExistsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(N'Finance.DeferredRevenueSchedule', N'U') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<bool>(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
