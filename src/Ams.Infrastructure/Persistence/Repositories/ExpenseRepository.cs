using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public ExpenseRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ExpenseEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ExpenseId, TenantId, EngagementId, AccountId, UserId, ExpenseDate, CategoryCode, Amount, Description, IsBillable, StatusCode, InvoiceId, CreatedDateUtc FROM Billing.ExpenseEntry WHERE ExpenseId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ExpenseEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ExpenseEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.ExpenseEntry", "ExpenseId, TenantId, EngagementId, AccountId, UserId, ExpenseDate, CategoryCode, Amount, Description, IsBillable, StatusCode, InvoiceId, CreatedDateUtc", "Description LIKE '%' + @SearchTerm + '%' OR CategoryCode LIKE '%' + @SearchTerm + '%'", "ExpenseDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ExpenseEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ExpenseEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateExpenseEntryRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Billing.ExpenseEntry (ExpenseId, TenantId, EngagementId, AccountId, UserId, ExpenseDate, CategoryCode, Amount, Description, IsBillable, StatusCode, InvoiceId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @EngagementId, @AccountId, @UserId, @ExpenseDate, @CategoryCode, @Amount, @Description, @IsBillable, @StatusCode, @InvoiceId, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.EngagementId, request.AccountId, request.UserId, request.ExpenseDate, request.CategoryCode, request.Amount, request.Description, request.IsBillable, request.StatusCode, request.InvoiceId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateExpenseEntryRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Billing.ExpenseEntry
SET EngagementId = @EngagementId,
    AccountId = @AccountId,
    UserId = @UserId,
    ExpenseDate = @ExpenseDate,
    CategoryCode = @CategoryCode,
    Amount = @Amount,
    Description = @Description,
    IsBillable = @IsBillable,
    StatusCode = @StatusCode,
    InvoiceId = @InvoiceId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ExpenseId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.EngagementId, request.AccountId, request.UserId, request.ExpenseDate, request.CategoryCode, request.Amount, request.Description, request.IsBillable, request.StatusCode, request.InvoiceId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Billing.ExpenseEntry SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE ExpenseId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
