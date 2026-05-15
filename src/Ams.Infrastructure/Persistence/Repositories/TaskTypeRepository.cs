using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TaskTypeRepository : ITaskTypeRepository
{
    private const string SelectColumns = @"TaskTypeId, TenantId, TaskTypeCode, TaskTypeName, Description, SortOrder, IsActive,
        CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted";

    private readonly ISqlConnectionFactory _connectionFactory;

    public TaskTypeRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<TaskTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM OPS.TaskType WHERE TaskTypeId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TaskTypeDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TaskTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var sql = $@"
;WITH Cte AS (
    SELECT {SelectColumns}
    FROM OPS.TaskType
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TaskTypeCode LIKE '%' + @SearchTerm + '%' OR TaskTypeName LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY SortOrder ASC, TaskTypeName ASC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1)
FROM OPS.TaskType
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TaskTypeCode LIKE '%' + @SearchTerm + '%' OR TaskTypeName LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TaskTypeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TaskTypeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateTaskTypeRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.TaskType
    (TaskTypeId, TenantId, TaskTypeCode, TaskTypeName, Description, SortOrder, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@TaskTypeId, @TenantId, @TaskTypeCode, @TaskTypeName, @Description, @SortOrder, @IsActive, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TaskTypeId = id, request.TenantId, request.TaskTypeCode, request.TaskTypeName, request.Description, request.SortOrder, request.IsActive, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateTaskTypeRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.TaskType
SET TaskTypeCode = @TaskTypeCode,
    TaskTypeName = @TaskTypeName,
    Description = @Description,
    SortOrder = @SortOrder,
    IsActive = @IsActive,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TaskTypeId = @TaskTypeId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TaskTypeId = id, request.TaskTypeCode, request.TaskTypeName, request.Description, request.SortOrder, request.IsActive, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.TaskType
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TaskTypeId = @TaskTypeId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TaskTypeId = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
