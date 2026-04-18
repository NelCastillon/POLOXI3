using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class WorkflowApprovalRouteRepository : IWorkflowApprovalRouteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public WorkflowApprovalRouteRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<WorkflowApprovalRouteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT RouteId, TenantId, WorkflowDefinitionId, StepOrder, StepName, ApproverUserId, ApproverRoleCode, ThresholdMinAmount, ThresholdMaxAmount, RequireAllApprovers, IsActive, CreatedDateUtc FROM Workflow.WorkflowApprovalRoute WHERE RouteId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<WorkflowApprovalRouteDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<WorkflowApprovalRouteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Workflow.WorkflowApprovalRoute",
            "RouteId, TenantId, WorkflowDefinitionId, StepOrder, StepName, ApproverUserId, ApproverRoleCode, ThresholdMinAmount, ThresholdMaxAmount, RequireAllApprovers, IsActive, CreatedDateUtc",
            "StepName LIKE '%' + @SearchTerm + '%' OR ApproverRoleCode LIKE '%' + @SearchTerm + '%'",
            "CreatedDateUtc DESC",
            true);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<WorkflowApprovalRouteDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<WorkflowApprovalRouteDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
