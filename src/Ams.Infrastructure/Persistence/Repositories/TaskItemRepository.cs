using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TaskItemRepository : ITaskItemRepository
{
    private const string SelectColumns = @"TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode,
        RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CompletedDate,
        CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted";

    private readonly ISqlConnectionFactory _connectionFactory;
    public TaskItemRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<TaskItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM OPS.TaskItem WHERE TaskItemId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TaskItemDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TaskItemDto>> SearchAsync(Guid tenantId, string? searchTerm, string? stageCode, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = $@"
;WITH Cte AS (
    SELECT {SelectColumns}
    FROM OPS.TaskItem
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@StageCode IS NULL OR @StageCode = '' OR StageCode = @StageCode)
      AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TaskNumber LIKE '%' + @SearchTerm + '%' OR Title LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR TaskTypeCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY DueDate ASC, CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1)
FROM OPS.TaskItem
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@StageCode IS NULL OR @StageCode = '' OR StageCode = @StageCode)
  AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TaskNumber LIKE '%' + @SearchTerm + '%' OR Title LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR TaskTypeCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            StageCode = stageCode,
            StatusCode = statusCode,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TaskItemDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TaskItemDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateTaskItemRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.TaskItem
    (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode,
     RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CompletedDate,
     CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
    (@TaskItemId, @TenantId, @TaskNumber, @Title, @Description, @TaskTypeCode, @StageCode, @PriorityCode, @StatusCode,
     @RelatedEntityName, @RelatedEntityId, @AccountId, @AssignedToUserId, @DueDate, NULL,
     SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TaskItemId = id, request.TenantId, request.TaskNumber, request.Title, request.Description, request.TaskTypeCode, request.StageCode, request.PriorityCode, request.StatusCode, request.RelatedEntityName, request.RelatedEntityId, request.AccountId, request.AssignedToUserId, request.DueDate, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateTaskItemRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.TaskItem
SET Title = @Title,
    Description = @Description,
    TaskTypeCode = @TaskTypeCode,
    StageCode = @StageCode,
    PriorityCode = @PriorityCode,
    StatusCode = @StatusCode,
    RelatedEntityName = @RelatedEntityName,
    AssignedToUserId = @AssignedToUserId,
    DueDate = @DueDate,
    CompletedDate = @CompletedDate,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TaskItemId = @TaskItemId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TaskItemId = id, request.Title, request.Description, request.TaskTypeCode, request.StageCode, request.PriorityCode, request.StatusCode, request.RelatedEntityName, request.AssignedToUserId, request.DueDate, request.CompletedDate, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE OPS.TaskItem SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE TaskItemId = @TaskItemId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TaskItemId = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
