using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IWorkflowDefinitionRepository
{
    Task<WorkflowDefinitionDto?> GetByIdAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default);
    Task<PagedResult<WorkflowDefinitionDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
