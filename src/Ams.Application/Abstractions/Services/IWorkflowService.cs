using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IWorkflowService
{
    Task<WorkflowInstanceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<WorkflowInstanceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> InitiateAsync(Guid tenantId, string targetEntityName, Guid targetEntityId, Guid? workflowDefinitionId, Guid? userId, string? notes, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid workflowInstanceId, Guid? userId, string? notes, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid workflowInstanceId, Guid? userId, string? reason, CancellationToken cancellationToken = default);
    Task ReturnAsync(Guid workflowInstanceId, Guid? userId, string? reason, CancellationToken cancellationToken = default);
}
