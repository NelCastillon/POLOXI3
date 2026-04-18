using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class WorkflowSlaRuleService : IWorkflowSlaRuleService
{
    private readonly IWorkflowSlaRuleRepository _repository;

    public WorkflowSlaRuleService(IWorkflowSlaRuleRepository repository) => _repository = repository;

    public Task<WorkflowSlaRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<WorkflowSlaRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
