using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IWorkflowReworkRequestRepository
{
    Task<WorkflowReworkRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<WorkflowReworkRequestDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
