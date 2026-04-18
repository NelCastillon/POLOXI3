using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public WorkflowRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<WorkflowInstanceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT WorkflowInstanceId, TenantId, TargetEntityName, TargetEntityId, StatusCodeId AS StatusCode, SubmittedDateUtc FROM Workflow.WorkflowInstance WHERE WorkflowInstanceId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<WorkflowInstanceDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<WorkflowInstanceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Workflow.WorkflowInstance",
            "WorkflowInstanceId, TenantId, TargetEntityName, TargetEntityId, StatusCodeId AS StatusCode, SubmittedDateUtc",
            "TargetEntityName LIKE '%' + @SearchTerm + '%'",
            "CreatedDateUtc DESC",
            false);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<WorkflowInstanceDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<WorkflowInstanceDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
