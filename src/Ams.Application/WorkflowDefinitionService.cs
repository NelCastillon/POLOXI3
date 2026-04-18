using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IWorkflowDefinitionRepository _repository;

    public WorkflowDefinitionService(IWorkflowDefinitionRepository repository)
        => _repository = repository;

    public Task<WorkflowDefinitionDto?> GetByIdAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(workflowDefinitionId, cancellationToken);

    public Task<PagedResult<WorkflowDefinitionDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken);
}
