using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Lobs;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class LineOfBusinessRepository : ILineOfBusinessRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public LineOfBusinessRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = "LobId, TenantId, LobCode, LobName, Category, Description, IsActive, CreatedDateUtc";

    public async Task<LineOfBusinessDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {SelectColumns} FROM Agency.LineOfBusiness WHERE LobId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<LineOfBusinessDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<LineOfBusinessDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Agency.LineOfBusiness",
            SelectColumns,
            "LobName LIKE '%' + @SearchTerm + '%' OR LobCode LIKE '%' + @SearchTerm + '%'",
            "LobName ASC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<LineOfBusinessDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<LineOfBusinessDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateLineOfBusinessRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Agency.LineOfBusiness
    (LobId, TenantId, LobCode, LobName, Category, Description, IsActive, CreatedDateUtc, IsDeleted)
VALUES
    (@LobId, @TenantId, @LobCode, @LobName, @Category, @Description, 1, GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            LobId = id,
            request.TenantId,
            request.LobCode,
            request.LobName,
            request.Category,
            request.Description,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateLineOfBusinessRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Agency.LineOfBusiness
SET    LobCode         = @LobCode,
       LobName         = @LobName,
       Category        = @Category,
       Description     = @Description,
       IsActive        = @IsActive,
       ModifiedDateUtc = GETUTCDATE()
WHERE  LobId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.LobCode,
            request.LobName,
            request.Category,
            request.Description,
            request.IsActive,
        }, cancellationToken: cancellationToken));
    }
}
