using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class WorkflowDefinitionRepository : IWorkflowDefinitionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public WorkflowDefinitionRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<WorkflowDefinitionDto?> GetByIdAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT WorkflowDefinitionId, TenantId, WorkflowCode, WorkflowName, Description,
                   TargetEntityName, TriggerTypeCode, ThresholdAmount, IsActive,
                   IsSystemDefined, Version, CreatedDateUtc, ModifiedDateUtc
            FROM Workflow.WorkflowDefinition
            WHERE WorkflowDefinitionId = @WorkflowDefinitionId AND IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<WorkflowDefinitionDto>(
            new CommandDefinition(sql, new { WorkflowDefinitionId = workflowDefinitionId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<WorkflowDefinitionDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT WorkflowDefinitionId, TenantId, WorkflowCode, WorkflowName, Description,
                       TargetEntityName, TriggerTypeCode, ThresholdAmount, IsActive,
                       IsSystemDefined, Version, CreatedDateUtc, ModifiedDateUtc
                FROM Workflow.WorkflowDefinition
                WHERE IsDeleted = 0
                  AND (@SearchTerm IS NULL OR WorkflowName     LIKE '%' + @SearchTerm + '%'
                                          OR WorkflowCode     LIKE '%' + @SearchTerm + '%'
                                          OR TargetEntityName LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY IsSystemDefined DESC, WorkflowName ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Workflow.WorkflowDefinition
            WHERE IsDeleted = 0
              AND (@SearchTerm IS NULL OR WorkflowName     LIKE '%' + @SearchTerm + '%'
                                      OR WorkflowCode     LIKE '%' + @SearchTerm + '%'
                                      OR TargetEntityName LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<WorkflowDefinitionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<WorkflowDefinitionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
