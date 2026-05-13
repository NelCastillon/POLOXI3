using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class GLAccountRepository : IGLAccountRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public GLAccountRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<GLAccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, 
    Description, ParentGLAccountId, IsActive, CreatedDateUtc 
FROM Finance.GLAccount 
WHERE GLAccountId = @Id AND IsDeleted = 0";
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<GLAccountDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<GLAccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var selectColumns = "GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, Description, ParentGLAccountId, IsActive, CreatedDateUtc";
        var searchPredicate = "AccountName LIKE '%' + @SearchTerm + '%' OR AccountCode LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.GLAccount", selectColumns, searchPredicate, "AccountCode ASC");
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<GLAccountDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<GLAccountDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateGLAccountRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.GLAccount (GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, Description, ParentGLAccountId, IsActive, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountCode, @AccountName, @AccountTypeCode, @Description, @ParentGLAccountId, @IsActive, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountCode, request.AccountName, request.AccountTypeCode, request.Description, request.ParentGLAccountId, request.IsActive, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateGLAccountRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Finance.GLAccount
SET AccountCode = @AccountCode,
    AccountName = @AccountName,
    AccountTypeCode = @AccountTypeCode,
    Description = @Description,
    ParentGLAccountId = @ParentGLAccountId,
    IsActive = @IsActive,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE GLAccountId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountCode, request.AccountName, request.AccountTypeCode, request.Description, request.ParentGLAccountId, request.IsActive, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}
