using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IWorkflowDefinitionService
{
    Task<WorkflowDefinitionDto?> GetByIdAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default);
    Task<PagedResult<WorkflowDefinitionDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
