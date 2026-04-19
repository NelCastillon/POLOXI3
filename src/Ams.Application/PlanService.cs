using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Plans;

namespace Ams.Application;

public sealed class PlanService : IPlanService
{
    private readonly IPlanRepository _repository;
    public PlanService(IPlanRepository repository) => _repository = repository;

    public Task<PlanDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<PlanDto>> SearchAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task CloneAsync(Guid id, string newPlanCode, string newPlanName, CancellationToken cancellationToken = default)
        => _repository.CloneAsync(id, newPlanCode, newPlanName, cancellationToken);

    public Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
        => _repository.SetActiveAsync(id, isActive, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken);
}
