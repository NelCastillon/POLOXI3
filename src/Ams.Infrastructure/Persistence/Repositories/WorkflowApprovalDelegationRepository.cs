using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class WorkflowApprovalDelegationRepository : IWorkflowApprovalDelegationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public WorkflowApprovalDelegationRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<WorkflowApprovalDelegationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT DelegationId, TenantId, DelegatorUserId, DelegateUserId, WorkflowDefinitionId, DelegationStartDateUtc, DelegationEndDateUtc, Reason, IsActive, CreatedDateUtc FROM Workflow.WorkflowApprovalDelegation WHERE DelegationId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<WorkflowApprovalDelegationDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<WorkflowApprovalDelegationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Workflow.WorkflowApprovalDelegation",
            "DelegationId, TenantId, DelegatorUserId, DelegateUserId, WorkflowDefinitionId, DelegationStartDateUtc, DelegationEndDateUtc, Reason, IsActive, CreatedDateUtc",
            "Reason LIKE '%' + @SearchTerm + '%'",
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

        var items = (await multi.ReadAsync<WorkflowApprovalDelegationDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<WorkflowApprovalDelegationDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
