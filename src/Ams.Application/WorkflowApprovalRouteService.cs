using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class WorkflowApprovalRouteService : IWorkflowApprovalRouteService
{
    private readonly IWorkflowApprovalRouteRepository _repository;

    public WorkflowApprovalRouteService(IWorkflowApprovalRouteRepository repository) => _repository = repository;

    public Task<WorkflowApprovalRouteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<WorkflowApprovalRouteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
