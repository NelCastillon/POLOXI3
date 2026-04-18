using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class WorkflowApprovalHistoryService : IWorkflowApprovalHistoryService
{
    private readonly IWorkflowApprovalHistoryRepository _repository;

    public WorkflowApprovalHistoryService(IWorkflowApprovalHistoryRepository repository) => _repository = repository;

    public Task<WorkflowApprovalHistoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<WorkflowApprovalHistoryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
