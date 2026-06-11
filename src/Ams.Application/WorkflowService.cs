using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class WorkflowService : IWorkflowService
{
    private const int PendingStatus = 1;
    private const int ApprovedStatus = 2;
    private const int RejectedStatus = 3;
    private const int ReturnedStatus = 4;
    private readonly IWorkflowRepository _repository;

    public WorkflowService(IWorkflowRepository repository)
    {
        _repository = repository;
    }

    public Task<WorkflowInstanceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<WorkflowInstanceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public async Task<Guid> InitiateAsync(Guid tenantId, string targetEntityName, Guid targetEntityId, Guid? workflowDefinitionId, Guid? userId, string? notes, CancellationToken cancellationToken = default)
    {
        var id = await _repository.CreateAsync(tenantId, targetEntityName, targetEntityId, workflowDefinitionId, userId, cancellationToken);
        await _repository.LogHistoryAsync(tenantId, id, userId, "Initiate", notes, null, PendingStatus.ToString(), cancellationToken);
        return id;
    }

    public Task ApproveAsync(Guid workflowInstanceId, Guid? userId, string? notes, CancellationToken cancellationToken = default)
        => SetStatusAsync(workflowInstanceId, ApprovedStatus, userId, "Approve", notes, cancellationToken);

    public Task RejectAsync(Guid workflowInstanceId, Guid? userId, string? reason, CancellationToken cancellationToken = default)
        => SetStatusAsync(workflowInstanceId, RejectedStatus, userId, "Reject", reason, cancellationToken);

    public Task ReturnAsync(Guid workflowInstanceId, Guid? userId, string? reason, CancellationToken cancellationToken = default)
        => SetStatusAsync(workflowInstanceId, ReturnedStatus, userId, "Return", reason, cancellationToken);

    private async Task SetStatusAsync(Guid workflowInstanceId, int statusCode, Guid? userId, string actionCode, string? notes, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(workflowInstanceId, cancellationToken);
        if (item is null)
        {
            throw new InvalidOperationException("Workflow instance was not found.");
        }

        await _repository.UpdateStatusAsync(workflowInstanceId, statusCode, userId, cancellationToken);
        await _repository.LogHistoryAsync(item.TenantId, workflowInstanceId, userId, actionCode, notes, item.StatusCode.ToString(), statusCode.ToString(), cancellationToken);
    }
}
