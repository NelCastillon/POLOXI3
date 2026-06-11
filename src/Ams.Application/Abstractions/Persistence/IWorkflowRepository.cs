using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IWorkflowRepository
{
    Task<WorkflowInstanceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<WorkflowInstanceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(Guid tenantId, string targetEntityName, Guid targetEntityId, Guid? workflowDefinitionId, Guid? userId, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid workflowInstanceId, int statusCode, Guid? userId, CancellationToken cancellationToken = default);
    Task LogHistoryAsync(Guid tenantId, Guid workflowInstanceId, Guid? actorUserId, string actionCode, string? notes, string? previousStatusCode, string? newStatusCode, CancellationToken cancellationToken = default);
}
