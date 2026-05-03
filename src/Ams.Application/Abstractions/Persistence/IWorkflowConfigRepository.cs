using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.WorkflowConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface IWorkflowConfigRepository
{
    Task<WorkflowConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<WorkflowConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateWorkflowConfigItemRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateWorkflowConfigItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
